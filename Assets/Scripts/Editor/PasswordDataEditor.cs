#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PasswordData))]
public class PasswordDataEditor : Editor
{
    private bool isDragging = false;
    private Vector2 startPos;
    private Vector2 currentPos;

    public override void OnInspectorGUI()
    {
        PasswordData data = (PasswordData)target;

        // Standart değişkenleri çiz
        DrawDefaultInspector();

        EditorGUILayout.Space(15);

        // --- GÜVENLİK KONTROLLERİ ---
        if (data.pageTexture == null)
        {
            EditorGUILayout.HelpBox("Lütfen bir 'Page Texture' atayın.", MessageType.Warning);
            return;
        }
        if (data.totalPages < 1)
            data.totalPages = 1; // 0'a bölünme hatasını önle
        if (data.passwordPage >= data.totalPages)
            data.passwordPage = data.totalPages - 1;
        if (data.passwordPage < 0)
            data.passwordPage = 0;

        EditorGUILayout.LabelField($"SAYFA {data.passwordPage} EDİTÖRÜ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Alanı belirlemek için aşağıda GÖRÜNTÜLENEN SAYFA üzerinde sürükleyip bırakın.",
            MessageType.Info
        );

        // --- SAYFA BOYUT HESAPLAMALARI ---
        // Texture'ın tamamının değil, tek bir sayfanın en/boy oranını buluyoruz.
        // Texture yatay şerit (Atlas) olduğu için: Genişlik / SayfaSayısı
        float singlePageTextureWidth = data.pageTexture.width / (float)data.totalPages;
        float singlePageTextureHeight = data.pageTexture.height;
        float singlePageAspectRatio = singlePageTextureWidth / singlePageTextureHeight;

        // Editörde çizilecek alanın genişliği
        float displayWidth = EditorGUIUtility.currentViewWidth - 40;
        float displayHeight = displayWidth / singlePageAspectRatio;

        // Editörde yer ayır (Rect)
        Rect displayRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);

        // --- TEXTURE'IN SADECE İLGİLİ KISMINI ÇİZ (CROP) ---
        // UV koordinatlarında (0 ile 1 arası) hangi dilimi göstereceğimizi hesaplıyoruz.
        float uvWidthPerPage = 1.0f / data.totalPages;
        float uvStartX = data.passwordPage * uvWidthPerPage;

        // Rect(x, y, width, height) -> UV uzayında
        Rect uvCropRect = new Rect(uvStartX, 0f, uvWidthPerPage, 1.0f);

        // Sadece o dilimi ekrana çiz
        GUI.DrawTextureWithTexCoords(displayRect, data.pageTexture, uvCropRect);

        // --- HOTSPOT ÇİZİMİ (YEŞİL KUTU) ---
        // Kaydedilen veri artık sayfa-lokal olduğu için (0-1), direkt displayRect ile çarpabiliriz.
        Rect currentHotspotScreenRect = new Rect(
            displayRect.x + (data.passwordHotspotUV.x * displayRect.width),
            // Y ekseni Unity GUI'de yukarıdan başlar, o yüzden ters çeviriyoruz
            displayRect.y
                + (
                    (1f - (data.passwordHotspotUV.y + data.passwordHotspotUV.height))
                    * displayRect.height
                ),
            data.passwordHotspotUV.width * displayRect.width,
            data.passwordHotspotUV.height * displayRect.height
        );

        Handles.DrawSolidRectangleWithOutline(
            currentHotspotScreenRect,
            new Color(0, 1, 0, 0.25f),
            Color.green
        );

        // --- MOUSE ETKİLEŞİMİ ---
        HandleInput(displayRect, data);

        // --- BİLGİ GÖSTERİMİ ---
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Kaydedilen Lokal UV:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            $"X: {data.passwordHotspotUV.x:F3}  Y: {data.passwordHotspotUV.y:F3}",
            EditorStyles.miniLabel
        );
        EditorGUILayout.LabelField(
            $"W: {data.passwordHotspotUV.width:F3}  H: {data.passwordHotspotUV.height:F3}",
            EditorStyles.miniLabel
        );
    }

    private void HandleInput(Rect bounds, PasswordData data)
    {
        Event e = Event.current;

        // Mouse bu alanın içinde mi veya sürükleme işlemi devam mı ediyor?
        if (bounds.Contains(e.mousePosition) || isDragging)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isDragging = true;
                startPos = e.mousePosition;
                currentPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isDragging)
            {
                currentPos = e.mousePosition;

                // Sürüklerken mavi taslak çiz
                Rect dragRect = GetRectFromPoints(startPos, currentPos);
                dragRect = ClampRect(dragRect, bounds); // Dışarı taşmayı engelle
                Handles.DrawSolidRectangleWithOutline(
                    dragRect,
                    new Color(0, 0.5f, 1, 0.2f),
                    Color.cyan
                );

                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && isDragging)
            {
                isDragging = false;

                // Son Rect'i al
                Rect finalRect = GetRectFromPoints(startPos, e.mousePosition);
                finalRect = ClampRect(finalRect, bounds);

                // --- EKRAN KOORDİNATLARINI LOCAL UV'YE ÇEVİR ---
                // Buradaki matematik artık tüm Atlas'a göre değil, sadece GÖSTERİLEN KUTUYA göre çalışır.
                float localUvX = (finalRect.x - bounds.x) / bounds.width;
                float localUvW = finalRect.width / bounds.width;
                float localUvH = finalRect.height / bounds.height;
                // Y'yi ters çevir (GUI vs Texture koordinat farkı)
                float localUvY = 1f - ((finalRect.y - bounds.y) / bounds.height) - localUvH;

                Undo.RecordObject(data, "Set Password Hotspot");
                data.passwordHotspotUV = new Rect(localUvX, localUvY, localUvW, localUvH);
                EditorUtility.SetDirty(data);

                e.Use();
            }
        }
    }

    private Rect GetRectFromPoints(Vector2 p1, Vector2 p2)
    {
        return new Rect(
            Mathf.Min(p1.x, p2.x),
            Mathf.Min(p1.y, p2.y),
            Mathf.Abs(p1.x - p2.x),
            Mathf.Abs(p1.y - p2.y)
        );
    }

    private Rect ClampRect(Rect r, Rect bounds)
    {
        float x = Mathf.Max(r.x, bounds.x);
        float y = Mathf.Max(r.y, bounds.y);
        float xMax = Mathf.Min(r.xMax, bounds.xMax);
        float yMax = Mathf.Min(r.yMax, bounds.yMax);
        return new Rect(x, y, xMax - x, yMax - y);
    }
}
#endif
