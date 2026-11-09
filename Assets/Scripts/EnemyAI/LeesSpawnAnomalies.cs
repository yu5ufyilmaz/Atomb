using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lees odada belirince küçük bir tekinsizlik: her seferinde rastgele biri (bazen iki), seçenek varken art arda aynısı yok.
// Sahne bağlantısı yok; hedefler o an sahnede aranır, ışık efekti Lees'in kendi üzerinde koşar.
public static class LeesSpawnAnomalies
{
    public const int Turing = 0, LightSpike = 1, Oscilloscope = 2, Count = 3;
    public const string GlitchWord = "LEESSEEL"; // tekerlerdeki ve ekrandaki bozuk işaret, çevrilecek bir cümle değil
    private const float SecondChance = .25f; // bu ihtimalle ikinci, farklı bir anomali de oynar
    private const float NearbyMeters = 12f; // makine ya da osiloskop bundan uzaktaysa oyuncu fark etmez, aday sayılmaz
    private const float TuringHoldSeconds = 2.5f; // tekerler kelimede bu kadar durur (yazma + dönüşle ~4.2 sn)
    private const float ScopeSeconds = 2f;
    private const float SpikeMultiplier = 5f;

    private static int lastMask;
    private static InteractableTuringMachine turing;
    private static WaveformGenerator scope;
    private static Coroutine spikeRoutine;
    private static readonly List<Light> spikedLights = new List<Light>();
    private static readonly List<float> spikedBase = new List<float>();

    public static bool IsSpiking => spikeRoutine != null;

    public static void Play(LeesEnemyAI lees) => Play(lees, -1);

    // Test kancası: forceIndex >= 0 o anomaliyi (uygunsa) zorla oynatır, rastgelelik ve tekrar yasağı atlanır.
    public static bool Play(LeesEnemyAI lees, int forceIndex)
    {
        if (lees == null || lees.IsPracticeEncounter) return false;
        if (GlobalEnemyManager.Instance != null && GlobalEnemyManager.Instance.stopAllEnemies) return false;
        if (forceIndex >= 0) return IsAvailable(lees, forceIndex) && Run(lees, forceIndex);

        var choices = new List<int>(Count);
        for (int i = 0; i < Count; i++)
            if (IsAvailable(lees, i)) choices.Add(i);
        if (choices.Count == 0) { lastMask = 0; return false; }
        // Tekrar yasağı yalnız seçenek varken: tek aday kaldıysa doğum sessiz geçmesin, o oynar.
        if (choices.Count > 1 && choices.Exists(i => (lastMask & (1 << i)) == 0))
            choices.RemoveAll(i => (lastMask & (1 << i)) != 0);

        int mask = 0;
        int plays = choices.Count > 1 && Random.value < SecondChance ? 2 : 1;
        for (int n = 0; n < plays; n++)
        {
            int pick = Random.Range(0, choices.Count);
            if (Run(lees, choices[pick])) mask |= 1 << choices[pick];
            choices.RemoveAt(pick);
        }
        lastMask = mask;
        return mask != 0;
    }

    // Lees kaybolur ya da kapanırsa her şey hemen ilk haline döner.
    public static void Cancel(LeesEnemyAI lees)
    {
        if (turing != null) turing.StopSpellingLees();
        if (scope != null) scope.Disturb(0);
        if (spikeRoutine != null && lees != null) lees.StopCoroutine(spikeRoutine);
        spikeRoutine = null;
        RestoreLights();
    }

    private static bool IsAvailable(LeesEnemyAI lees, int index)
    {
        Vector3 player = Camera.main != null ? Camera.main.transform.position : lees.transform.position;
        switch (index)
        {
            case Turing:
                turing = Object.FindFirstObjectByType<InteractableTuringMachine>();
                return turing != null && turing.CanSpellLees
                    && Vector3.Distance(turing.transform.position, player) < NearbyMeters;
            case LightSpike:
                return spikeRoutine == null && !PoweredDevice.PowerOut && CollectRoomLights(lees.CurrentRoom, null) > 0;
            case Oscilloscope:
                scope = Object.FindFirstObjectByType<WaveformGenerator>();
                return scope != null && !scope.IsDisturbed && !PoweredDevice.PowerOut
                    && Vector3.Distance(scope.transform.position, player) < NearbyMeters;
        }
        return false;
    }

    private static bool Run(LeesEnemyAI lees, int index)
    {
        switch (index)
        {
            case Turing: return turing.TrySpellLees(GlitchWord, TuringHoldSeconds);
            case LightSpike:
                CollectRoomLights(lees.CurrentRoom, spikedLights);
                spikedBase.Clear();
                foreach (Light light in spikedLights) spikedBase.Add(light.intensity);
                spikeRoutine = lees.StartCoroutine(Spike());
                return true;
            case Oscilloscope: scope.Disturb(ScopeSeconds); return true;
        }
        return false;
    }

    // Odanın yanan, gerçek zamanlı aydınlatan lambaları; bake edilmiş ışığın parlaklığı oyunda değişmez.
    private static int CollectRoomLights(RoomManager room, List<Light> into)
    {
        into?.Clear();
        if (room == null) return 0;
        int count = 0;
        foreach (ControllableLight control in room.roomLights)
        {
            if (control == null || !control.IsOn || control.Lights == null) continue;
            foreach (Light light in control.Lights)
            {
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0
                    || light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;
                count++;
                into?.Add(light);
            }
        }
        return count;
    }

    private static IEnumerator Spike()
    {
        // Ani parlama, tam karanlık bir an (göz kopmayı fark etsin), sonra toparlanan 3 kırpışma; toplam ~1.4 sn.
        float[] multipliers = { SpikeMultiplier, 0f, 2.2f, .3f, 1.6f, .5f };
        float[] seconds = { .15f, .08f, .06f, .12f, .07f, .14f };
        for (int step = 0; step < multipliers.Length; step++)
        {
            SetLights(multipliers[step]);
            yield return new WaitForSeconds(seconds[step]);
        }
        for (float t = 0; t < 1; t += Time.deltaTime / .8f)
        {
            SetLights(Mathf.Lerp(.5f, 1f, t));
            yield return null;
        }
        spikeRoutine = null;
        RestoreLights();
    }

    private static void SetLights(float multiplier)
    {
        for (int i = 0; i < spikedLights.Count; i++)
            if (spikedLights[i] != null) spikedLights[i].intensity = spikedBase[i] * multiplier;
    }

    private static void RestoreLights()
    {
        SetLights(1);
        spikedLights.Clear();
        spikedBase.Clear();
    }
}
