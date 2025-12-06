#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LeesEnemyAI))]
public class LeesEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LeesEnemyAI ai = (LeesEnemyAI)target;

        // --- STİLLER ---
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) 
            { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        // --- BAŞLIK ---
        EditorGUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        EditorGUI.LabelField(rect, "LEES AI KONTROL PANELİ", titleStyle);
        EditorGUILayout.Space(5);

        // --- CANLI TAKİP PANELİ ---
        // --- COOLDOWN DURUMU (YENİ EKLENECEK KISIM) ---
// Eğer oyun çalışıyorsa, Lees gizliyse VE Cooldown sayacı 0'dan büyükse göster
if (Application.isPlaying && ai.currentState == LeesEnemyAI.LeesState.Hidden && ai.debugCooldownTimer > 0)
{
    EditorGUILayout.LabelField("--- DİNLENME MODU (COOLDOWN) ---", EditorStyles.boldLabel);
    
    float cooldownRatio = ai.debugCooldownTimer / ai.spawnCooldownAfterDespawn;
    string label = $"Geri Dönüş Sayacı: {ai.debugCooldownTimer:F1}sn";

    // Mavi Bar
    GUI.color = Color.cyan;
    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 25), cooldownRatio, label);
    GUI.color = Color.white;

    EditorGUILayout.HelpBox("Lees şu an bekleme süresinde. Şans artmaz, spawn olmaz.", MessageType.Info);
    EditorGUILayout.Space(10);
    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Çizgi çek
}
        if (Application.isPlaying && ai.currentState == LeesEnemyAI.LeesState.Active)
        {
            EditorGUILayout.LabelField("--- CANLI SENARYO TAKİBİ ---", EditorStyles.boldLabel);

            // 1. GÖRÜŞ DURUMU
            EditorGUILayout.BeginHorizontal("box");
            if (ai.debugIsVisible)
            {
                GUI.backgroundColor = Color.red;
                GUILayout.Button("GÖRÜYOR (EYE CONTACT)", GUILayout.Height(25));
            }
            else
            {
                GUI.backgroundColor = Color.green;
                GUILayout.Button("GÖRMÜYOR (HIDDEN)", GUILayout.Height(25));
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 2. BARLAR
            // A) SESSİZ BEKLEYİŞ (Ignorance)
            if (!ai.debugHasBeenSpotted)
            {
                float ignoranceRatio = ai.debugIgnoranceTimer / ai.maxIgnoranceTime;
                string label = $"Fark Edilmedi: {ai.maxIgnoranceTime - ai.debugIgnoranceTimer:F1}sn kaldı";
                
                GUI.color = new Color(0.8f, 0.2f, 1f); // Mor
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 25), ignoranceRatio, label);
                GUI.color = Color.white;
            }
            // B) FARK EDİLDİ (Reaction & Survival)
            else
            {
                // Reaction (C)
                float reactionRatio = ai.debugReactionTimer / ai.maxReactionTime;
                GUI.color = Color.Lerp(Color.yellow, Color.red, reactionRatio);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), reactionRatio, $"Tepki Süresi: {ai.debugReactionTimer:F2}s / {ai.maxReactionTime}s");
                GUI.color = Color.white;

                if (ai.debugIsVisible) 
                    EditorGUILayout.HelpBox("Oyuncu hala bakıyor! Süre dolarsa ÖLÜR.", MessageType.Warning);

                EditorGUILayout.Space(2);

                // Survival (D)
                if (!ai.debugIsVisible)
                {
                    float survivalRatio = ai.debugSurvivalTimer / ai.survivalWaitTime;
                    GUI.color = Color.green;
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), survivalRatio, $"Kurtuluş: %{survivalRatio * 100:F0}");
                    GUI.color = Color.white;
                }
            }

            // 3. HIZ UYARISI
            float speed = ai.debugPlayerSpeed;
            if (ai.debugHasBeenSpotted && !ai.debugIsVisible && speed > ai.movementTolerance)
            {
                GUI.backgroundColor = Color.red;
                EditorGUILayout.HelpBox($"KRİTİK HATA: Hareket Ediliyor! ({speed:F2}) -> ÖLÜM SEBEBİ", MessageType.Error);
            }
            GUI.backgroundColor = Color.white;
        }
        else if (Application.isPlaying)
        {
             EditorGUILayout.HelpBox("Lees şu an sahnede değil (Hidden State).", MessageType.Info);
        }

        EditorGUILayout.Space(10);
        
        // --- TEST BUTONLARI ---
        EditorGUILayout.LabelField("TEST EDİTÖRÜ", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("SPAWN (Çağır)", GUILayout.Height(30))) ai.SpawnLeesInRoom();
        if (GUILayout.Button("DESPAWN (Gönder)", GUILayout.Height(30))) ai.DespawnLees();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // --- GERİ GELEN JUMPSCARE BUTONU ---
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Kırmızı
        if (GUILayout.Button("☠ TEST JUMPSCARE (ÖLDÜR) ☠", GUILayout.Height(30)))
        {
            if(Application.isPlaying) 
            {
                ai.TriggerDeath("DEBUG: Editör Butonuyla Öldürüldü");
            }
            else
            {
                Debug.LogWarning("Bu buton sadece oyun çalışırken (Play Mode) çalışır.");
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);
        DrawDefaultInspector();
    }
}
#endif