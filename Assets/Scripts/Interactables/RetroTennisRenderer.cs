using UnityEngine;

public class RetroTennisRenderer : MonoBehaviour
{
    [Header("Bağlantı")]
    [Tooltip("Ana makine scriptini (InteractableRetroOscilloscope) buraya sürükle")]
    public InteractableRetroOscilloscope logic;

    [Header("Görsel Ayarlar")]
    public float lineWidth = 0.015f; // Ekranı küçülttüğümüz için çizgi kalınlığını da biraz incelttik
    public int paddleResolution = 20;
    public int ballResolution = 12;

    [Header("Derinlik ve Kavis (CRT)")]
    public float zOffset = 0f;

    [Tooltip(
        "Ekranın ortasındaki dışa doğru bombe miktarı. (Topun camın içine girmemesi için bunu ayarla)"
    )]
    public float curveDepth = 0.1f; // Eski WaveformGenerator'daki kavis mantığı

    [Header("Materyal (Mor Kare Çözümü)")]
    [Tooltip("Eğer çizgiler mor çıkıyorsa, parlak yeşil CRT materyalini BURAYA sürükleyip bırak!")]
    public Material crtMaterial;

    private LineRenderer paddleLine;
    private LineRenderer ballLine;
    private LineRenderer targetLine;

    void Start()
    {
        if (logic == null)
            logic = GetComponentInParent<InteractableRetroOscilloscope>();

        var oldWave = GetComponent<WaveformGenerator>();
        if (oldWave != null)
            oldWave.enabled = false;

        // Materyal atanmamışsa eskisinden çalmayı dene
        if (crtMaterial == null)
        {
            LineRenderer originalLr = GetComponent<LineRenderer>();
            if (originalLr != null)
            {
                crtMaterial = originalLr.sharedMaterial;
                originalLr.enabled = false;
            }
        }

        paddleLine = CreateLine("Paddle_Line", crtMaterial);
        ballLine = CreateLine("Ball_Line", crtMaterial);
        targetLine = CreateLine("Target_Line", crtMaterial);
    }

    LineRenderer CreateLine(string objName, Material mat)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(this.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = this.gameObject.layer;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        if (mat != null)
            lr.material = mat;

        return lr;
    }

    // --- YENİ EKLENEN: BOMBE MATEMATİĞİ ---
    // Ekranın merkezinde topu kameraya yaklaştırır (kavisi taklit eder), kenarlarda geriye yaslar.
    float GetZ(float x, float y)
    {
        if (logic == null)
            return zOffset;
        float dist = Mathf.Sqrt(x * x + y * y);
        float normalizedDist = Mathf.Clamp01(dist / logic.screenRadius);

        // Merkezde 1, kenarlarda 0 veren kosinüs dalgası
        float bulge = Mathf.Cos(normalizedDist * Mathf.PI / 2f);

        // Eksi değer kameraya (camın dışına) doğru itecektir
        return zOffset - (bulge * curveDepth);
    }

    void Update()
    {
        if (logic == null || logic.IsBroken || logic.IsSolved || !logic.GameActive)
        {
            if (paddleLine != null)
                paddleLine.positionCount = 0;
            if (ballLine != null)
                ballLine.positionCount = 0;
            if (targetLine != null)
                targetLine.positionCount = 0;
            return;
        }

        DrawPaddle();
        DrawBall();
        DrawTarget();
        SimulatePhosphorFade();
    }

    void DrawPaddle()
    {
        paddleLine.positionCount = paddleResolution + 1;

        float startAngle = (logic.paddleAngle - (logic.paddleWidth / 2f)) * Mathf.Deg2Rad;
        float endAngle = (logic.paddleAngle + (logic.paddleWidth / 2f)) * Mathf.Deg2Rad;

        for (int i = 0; i <= paddleResolution; i++)
        {
            float t = (float)i / paddleResolution;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            float x = Mathf.Cos(currentAngle) * logic.screenRadius;
            float y = Mathf.Sin(currentAngle) * logic.screenRadius;

            // Yeni GetZ fonksiyonu ile derinliği alıyoruz
            paddleLine.SetPosition(i, new Vector3(x, y, GetZ(x, y)));
        }
    }

    void DrawBall()
    {
        // Topu 12 noktadan oluşan bir çember (yuvarlak) olarak çiziyoruz
        ballLine.positionCount = ballResolution + 1;
        float r = logic.screenRadius * 0.06f; // Topun yarıçapı

        for (int i = 0; i <= ballResolution; i++)
        {
            float t = (float)i / ballResolution;
            float currentAngle = t * Mathf.PI * 2f; // 0 ile 360 derece arası (radyan)

            float x = logic.ballPosition.x + Mathf.Cos(currentAngle) * r;
            float y = logic.ballPosition.y + Mathf.Sin(currentAngle) * r;

            ballLine.SetPosition(i, new Vector3(x, y, GetZ(x, y)));
        }
    }

    void DrawTarget()
    {
        targetLine.positionCount = 5;

        // YENİ: Hedef boyutu da ekran boyutuna orantılı
        float r = logic.screenRadius * 0.12f;

        targetLine.SetPosition(
            0,
            new Vector3(
                logic.targetPosition.x,
                logic.targetPosition.y + r,
                GetZ(logic.targetPosition.x, logic.targetPosition.y + r)
            )
        );
        targetLine.SetPosition(
            1,
            new Vector3(
                logic.targetPosition.x + r,
                logic.targetPosition.y,
                GetZ(logic.targetPosition.x + r, logic.targetPosition.y)
            )
        );
        targetLine.SetPosition(
            2,
            new Vector3(
                logic.targetPosition.x,
                logic.targetPosition.y - r,
                GetZ(logic.targetPosition.x, logic.targetPosition.y - r)
            )
        );
        targetLine.SetPosition(
            3,
            new Vector3(
                logic.targetPosition.x - r,
                logic.targetPosition.y,
                GetZ(logic.targetPosition.x - r, logic.targetPosition.y)
            )
        );
        targetLine.SetPosition(
            4,
            new Vector3(
                logic.targetPosition.x,
                logic.targetPosition.y + r,
                GetZ(logic.targetPosition.x, logic.targetPosition.y + r)
            )
        );
    }

    void SimulatePhosphorFade()
    {
        float currentWidth = lineWidth;
        if (Random.value > 0.9f)
        {
            currentWidth *= Random.Range(0.6f, 1.0f);
        }

        paddleLine.widthMultiplier = currentWidth;
        ballLine.widthMultiplier = currentWidth;
        targetLine.widthMultiplier = currentWidth;
    }
}
