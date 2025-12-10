#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    // Katlanabilir menü durumları
    private static bool showBreakerSection = true;
    private static bool showPressureSection = true;
    private static bool showLightsSection = true;
    private static bool showPasswordSection = true;

    public override void OnInspectorGUI()
    {
        GameManager gm = (GameManager)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
        };
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = Color.white },
        };
        GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            fontStyle = FontStyle.Italic,
        };

        // BAŞLIK
        EditorGUILayout.Space(10);
        Rect rect = EditorGUILayout.GetControlRect(false, 40);
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
        EditorGUI.LabelField(rect, "⚡ SİSTEM YÖNETİM PANELİ ⚡", titleStyle);
        EditorGUILayout.Space(10);

        if (GUILayout.Button("🔄 Sahne Taraması Yap (Verileri Yenile)", GUILayout.Height(25)))
        {
            gm.RefreshReferences();
            EditorUtility.SetDirty(gm);
        }
        EditorGUILayout.Space(10);

        // =================================================================
        // 1. ELEKTRİK SİSTEMİ (Breaker)
        // =================================================================
        showBreakerSection = EditorGUILayout.BeginFoldoutHeaderGroup(
            showBreakerSection,
            "🔌 ELEKTRİK & SİGORTA"
        );
        if (showBreakerSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (gm.breakerBox != null)
            {
                BreakerBox bb = gm.breakerBox;
                // BreakerBox editörde her zaman "Kapalı" (IsTripped = false) başlar varsayılan olarak.
                bool isTripped = Application.isPlaying && bb.IsTripped;

                GUI.backgroundColor = isTripped
                    ? new Color(1f, 0.3f, 0.3f)
                    : new Color(0.4f, 0.8f, 0.4f);
                GUILayout.Box(
                    isTripped ? "⚠️ SİGORTA ATTI!" : "✅ SİSTEM AKTİF",
                    GUILayout.Height(25),
                    GUILayout.ExpandWidth(true)
                );
                GUI.backgroundColor = Color.white;

                if (Application.isPlaying && !isTripped)
                {
                    float risk = bb.GetCurrentRiskPercentage();
                    DrawBar(
                        risk,
                        $"Anlık Atma Riski: %{risk * 100:F1}",
                        Color.Lerp(Color.green, Color.red, risk)
                    );
                    EditorGUILayout.LabelField(
                        $"Aktif Işık: {bb.GetActiveLightCountPublic()}/{bb.GetTotalLightCount()} | Döngü: {bb.GetCycleCount()}",
                        EditorStyles.miniLabel
                    );
                }
                else if (Application.isPlaying && isTripped)
                {
                    if (GUILayout.Button("🛠️ Şarteli Kaldır", GUILayout.Height(25)))
                        bb.Interact();
                }
            }
            else
                EditorGUILayout.HelpBox(
                    "BreakerBox Yok! 'Sahne Taraması' yapın.",
                    MessageType.Error
                );
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(5);

        // =================================================================
        // 2. BASINÇ SİSTEMİ (VANA)
        // =================================================================
        showPressureSection = EditorGUILayout.BeginFoldoutHeaderGroup(
            showPressureSection,
            "🔥 BASINÇ SİSTEMİ (VANA)"
        );
        if (showPressureSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (gm.pressureManager != null)
            {
                PressureSystemManager pm = gm.pressureManager;

                if (Application.isPlaying)
                {
                    float pressure = pm.GetPressure();
                    float threshold = pm.GetWarningThreshold();
                    bool isCritical = pm.IsWarningActive();

                    // Durum Kutusu
                    GUI.backgroundColor = isCritical
                        ? Color.red
                        : (pressure > 50 ? Color.yellow : Color.green);
                    string status = isCritical
                        ? "🚨 KRİTİK SEVİYE! (%90+)"
                        : (pressure > 50 ? "⚠️ YÜKSELİYOR" : "✅ STABİL");
                    GUILayout.Box(status, GUILayout.Height(25), GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = Color.white;

                    // Basınç Barı
                    Rect r = EditorGUILayout.GetControlRect(false, 25);
                    EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
                    float fillWidth = r.width * (pressure / 100f);
                    EditorGUI.DrawRect(
                        new Rect(r.x, r.y, fillWidth, r.height),
                        Color.Lerp(Color.green, Color.red, pressure / 100f)
                    );

                    // Uyarı Çizgisi (Threshold)
                    float thresholdX = r.x + (r.width * (threshold / 100f));
                    EditorGUI.DrawRect(new Rect(thresholdX, r.y, 2, r.height), Color.yellow);

                    EditorGUI.LabelField(
                        r,
                        $"Basınç: %{pressure:F1}",
                        new GUIStyle(EditorStyles.boldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.white },
                        }
                    );

                    // Manuel Müdahale (Test İçin)
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Basıncı Düşür (-15)", GUILayout.Height(25)))
                        pm.ReducePressure(15f);
                    if (GUILayout.Button("Basıncı Arttır (+10)", GUILayout.Height(25)))
                        pm.currentPressure += 10f; // Hile butonu
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("Veriler oyun başlayınca yüklenir.", MessageType.Info);
                }
            }
            else
                EditorGUILayout.HelpBox("PressureSystemManager Yok!", MessageType.Error);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(5);

        // =================================================================
        // 3. ODA & IŞIK KONTROLÜ (GÜNCELLENDİ)
        // =================================================================
        showLightsSection = EditorGUILayout.BeginFoldoutHeaderGroup(
            showLightsSection,
            "💡 ODA & IŞIK KONTROLÜ"
        );
        if (showLightsSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var room in gm.allRooms)
            {
                if (room == null)
                    continue;
                EditorGUILayout.LabelField($"🏠 {room.roomName}", subHeaderStyle);

                if (room.roomLights.Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    foreach (var light in room.roomLights)
                    {
                        if (light == null)
                            continue;

                        // Editörde de durumu görebilmek için IsOn özelliğini kullanıyoruz
                        bool isOn = light.IsOn;
                        GUI.backgroundColor = isOn ? new Color(0.4f, 1f, 0.4f) : Color.gray;

                        if (
                            GUILayout.Button(
                                $"{light.gameObject.name}\n{(isOn ? "ON" : "OFF")}",
                                GUILayout.Height(35),
                                GUILayout.Width(80)
                            )
                        )
                        {
                            // OYUN AÇIKSA:
                            if (Application.isPlaying)
                            {
                                light.Interact();
                            }
                            // OYUN KAPALIYSA (EDİTÖR):
                            else
                            {
                                // Undo sistemine kaydet (Ctrl+Z ile geri alınabilsin diye)
                                Undo.RecordObject(light, "Toggle Light");
                                light.ToggleLightEditor(); // Yeni fonksiyonu çağır
                                EditorUtility.SetDirty(light); // Değişikliği kaydet
                            }
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                    EditorGUILayout.LabelField("   (Işık Yok)", EditorStyles.miniLabel);
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(5);

        // =================================================================
        // 4. BULMACA DURUMU
        // =================================================================
        showPasswordSection = EditorGUILayout.BeginFoldoutHeaderGroup(
            showPasswordSection,
            "🧩 BULMACA DURUMU"
        );
        if (showPasswordSection)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (gm.passwordManager != null)
            {
                PasswordManager pm = gm.passwordManager;
                if (Application.isPlaying)
                {
                    int total = pm.GetTotalRequiredCount();
                    int validated = pm.GetValidatedCount();
                    float progress = (float)validated / Mathf.Max(1, total);

                    DrawBar(progress, $"İlerleme: {validated}/{total}", Color.cyan);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("📝 Bulunan Şifreler:", EditorStyles.boldLabel);
                    var clues = pm.GetFoundPasswordsList();
                    if (clues.Count > 0)
                    {
                        foreach (var clue in clues)
                            EditorGUILayout.LabelField($"   - {clue}", EditorStyles.miniLabel);
                    }
                    else
                        EditorGUILayout.LabelField("   (Yok)", EditorStyles.miniLabel);
                }
                else
                    EditorGUILayout.HelpBox("Veriler oyun başlayınca yüklenir.", MessageType.Info);
            }
            else
                EditorGUILayout.HelpBox("PasswordManager Yok!", MessageType.Error);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (Application.isPlaying)
            Repaint();
    }

    void DrawBar(float value, string label, Color color)
    {
        Rect r = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), color);
        EditorGUI.LabelField(
            r,
            label,
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            }
        );
    }
}
#endif
