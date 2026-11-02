using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GameEndingChecks
{
    [MenuItem("Senzora/Doğrulama/Oyun Sonu Kilitleri")]
    public static void Run()
    {
        var game = GameManager.Instance;
        Require(Application.isPlaying && game != null && game.isGameStarted && !game.IsGameplayOver,
            "Oyuna girip Play'de çalıştır; test oturumu bitirir, sonra Play'den çık.");
        game.StartCoroutine(Flow(game));
    }

    private static IEnumerator Flow(GameManager game)
    {
        Require(game.BeginEnding(GameEnding.Final) && game.Ending == GameEnding.Final, "Final başlatılamadı.");
        Require(!game.BeginEnding(GameEnding.Death) && game.Ending == GameEnding.Final, "Final sürerken ölüm başlayabildi.");
        Require(GameInput.Blocked && GameInput.Scroll == 0, "Oyun sonunda girdiler açık kaldı.");
        Require(!PlayerInteraction.Instance.CanInteractWithWorld, "Oyun sonunda dünya etkileşimi açık kaldı.");
        Require(GlobalEnemyManager.Instance.stopAllEnemies && !PressureSystemManager.Instance.isSystemActive && !BreakerBox.Instance.isSystemActive,
            "Oyun sonunda düşman, basınç veya şalter çalışmaya devam ediyor.");
        var inputs = Object.FindFirstObjectByType<StarterAssets.StarterAssetsInputs>();
        Require(!inputs.enabled && Object.FindFirstObjectByType<StarterAssets.CharacterController>().IsFrozen, "Oyuncu oyun sonunda hareket edebiliyor.");
        inputs.pause = true;
        yield return null;
        yield return null;
        Require(!inputs.pause && !game.isGamePaused && Time.timeScale == 1, "Oyun sonunda duraklatma menüsü açıldı.");
        DeathUIManager.Instance.ShowDeathScreen("Oyun sonu kontrolü");
        Require(Time.timeScale == 1, "Final sırasında ölüm ekranı açıldı.");
        Debug.Log("GAME ENDING PASS: first ending wins; input, world interaction, pause, enemies, pressure and breaker stay closed.");
    }

    // Düşman önce ölümü başlatır, sonra JumpscareManager'a gelir; ikinci kapı jumpscare'i ve ölüm ekranını yutmamalı.
    [MenuItem("Senzora/Doğrulama/Düşman Ölümü Jumpscare")]
    public static void EnemyDeath()
    {
        Require(Application.isPlaying && GameManager.Instance != null && SaveManager.Instance != null, "Laboratuvarda Play gerekli.");
        SaveManager.Instance.sessionOnly = true; // gerçek Yeni Oyun yolu kayıt dosyasını silmesin
        GameManager.Instance.StartCoroutine(EnemyDeathFlow());
    }

    private static IEnumerator EnemyDeathFlow()
    {
        yield return ResearchGuidanceChecks.EnterNormalGame();
        var adam = Object.FindFirstObjectByType<AdamAI>(FindObjectsInactive.Include);
        Require(adam != null && JumpscareManager.Instance != null && DeathUIManager.Instance != null, "Adam, JumpscareManager veya DeathUI yok.");
        if (GlobalEnemyManager.Instance != null) GlobalEnemyManager.Instance.stopAllEnemies = false;
        adam.KillPlayer();
        Require(GameManager.Instance.Ending == GameEnding.Death, "Adam ölümü başlatmadı.");
        float end = Time.realtimeSinceStartup + 15;
        while (Time.timeScale != 0 && Time.realtimeSinceStartup < end) yield return null;
        Require(Time.timeScale == 0, "Jumpscare bitti ama ölüm ekranı açılmadı (oyuncu kilitli kaldı).");
        Debug.Log("ENEMY DEATH PASS: Adam KillPlayer -> jumpscare -> death screen.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
