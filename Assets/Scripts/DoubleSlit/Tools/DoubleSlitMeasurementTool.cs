using UnityEngine;

[AddComponentMenu("DoubleSlit/Double Slit Measurement Tool")]
public class DoubleSlitMeasurementTool : MonoBehaviour
{
    [Header("── 测量参数 ──")]
    public float totalRangeMm = 40f;

    [Header("── 目镜外观 ──")]
    public Color reticleColor = new Color(0.1f, 0.9f, 0.3f, 0.95f);

    [Header("── 运行时（只读）──")]
    [SerializeField] private bool _isMeasuring;
    [SerializeField] private float _handwheelPos = 0.5f;
    [SerializeField] private float _posA1;
    [SerializeField] private float _posA2;
    [SerializeField] private bool _hasA1;
    [SerializeField] private bool _hasA2;
    [SerializeField] private string _infoText;

    private DoubleSlitSimpleController _ctrl;
    private Renderer _screenRenderer;
    private Vector3 _screenOriginPos;
    private float _worldPerMm;

    private Texture2D _eyeTex;
    private float _diameter;
    private Vector2 _center;
    private Rect _circleRect;
    private static Texture2D _texBlack;

    void Start()
    {
        _ctrl = FindObjectOfType<DoubleSlitSimpleController>();
        FindScreen();
        MakeCircleTex();
        _infoText = "切换至正面视角 → 点击「开始」";
    }

    private void FindScreen()
    {
        var lut = FindObjectOfType<DoubleSlitLUTGenerator>();
        if (lut != null) _screenRenderer = lut.interferenceRenderer;
        if (_screenRenderer == null)
            foreach (var r in FindObjectsOfType<Renderer>())
                if (r.sharedMaterial?.shader?.name == "Custom/DoubleSlit")
                { _screenRenderer = r; break; }

        if (_screenRenderer != null)
        {
            _screenOriginPos = _screenRenderer.transform.position;
            Vector3 s = _screenRenderer.transform.localScale;
            float halfW = Mathf.Max(s.x, s.z) * 0.5f;
            float halfMm = lut != null ? lut.screenHalfWidthMm : 20f;
            _worldPerMm = halfW / halfMm;
        }
    }

    void OnDestroy()
    {
        if (_screenRenderer != null) _screenRenderer.transform.position = _screenOriginPos;
    }

    void Update()
    {
        if (!_isMeasuring || _screenRenderer == null) return;

        if (_ctrl == null || !_ctrl.IsFrontView)
        {
            _screenRenderer.transform.position = _screenOriginPos;
            return;
        }

        float target = (_handwheelPos - 0.5f) * totalRangeMm * _worldPerMm;
        Vector3 p = _screenOriginPos;
        p.x += target;
        _screenRenderer.transform.position = p;
    }

    void OnGUI()
    {
        if (!_isMeasuring || _ctrl == null || !_ctrl.IsFrontView) return;

        DrawEyeFrame();
        DrawCrosshair();
        DrawReadout();
        DrawHandwheelBar();
        HandleKeyboard();
        DrawButtons();
        DrawInfo();
    }

    public void StartMeasurement()
    {
        _isMeasuring = true;
        _hasA1 = false;
        _hasA2 = false;
        _handwheelPos = 0.5f;
        _posA1 = _posA2 = 0f;
        if (_screenRenderer != null) _screenOriginPos = _screenRenderer.transform.position;

        _infoText = _ctrl != null && _ctrl.IsFrontView
            ? "转动手轮，使绿线对准亮纹中心\n点击「记录 A₁」"
            : "先点击「正面观察干涉图样」";
    }

    public void StopMeasurement()
    {
        _isMeasuring = false;
        if (_screenRenderer != null) _screenRenderer.transform.position = _screenOriginPos;
    }

    public bool IsMeasuring => _isMeasuring;

    // ──────────── 目镜遮罩 ────────────

    private void MakeCircleTex()
    {
        if (_eyeTex != null) return;
        int S = 512;
        _eyeTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        _eyeTex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[S * S];
        float cx = S * 0.5f, cy = S * 0.4f;
        float ir = S * 0.44f, or = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                px[y * S + x] = d <= ir ? Color.clear
                    : d <= or ? Color.Lerp(Color.clear, Color.black, (d - ir) / (or - ir))
                    : Color.black;
            }
        _eyeTex.SetPixels(px);
        _eyeTex.Apply(false);
    }

    private void DrawEyeFrame()
    {
        float maxD = Mathf.Min(Screen.width - 320f, Screen.height) * 0.7f;
        _diameter = Mathf.Min(maxD, 520f);
        _center = new Vector2((Screen.width - 320f) * 0.44f, Screen.height * 0.44f);
        _circleRect = new Rect(_center.x - _diameter * 0.5f, _center.y - _diameter * 0.5f, _diameter, _diameter);

        if (_texBlack == null) _texBlack = Make1px(Color.black);
        float r = _diameter * 0.5f;
        float l = _center.x - r, re = _center.x + r;
        float t = _center.y - r, b = _center.y + r;
        float rLim = Screen.width - 320f;

        var bs = new GUIStyle { normal = { background = _texBlack } };
        GUI.Box(new Rect(0, 0, rLim, t), GUIContent.none, bs);
        GUI.Box(new Rect(0, b, rLim, Screen.height - b), GUIContent.none, bs);
        GUI.Box(new Rect(0, t, l, b - t), GUIContent.none, bs);
        GUI.Box(new Rect(re, t, rLim - re, b - t), GUIContent.none, bs);

        GUI.DrawTexture(_circleRect, _eyeTex);
    }

    // ──────────── 固定绿色十字（圆心） ────────────

    private void DrawCrosshair()
    {
        GUI.color = reticleColor;
        float len = _diameter * 0.38f, w = 2f;
        var bs = new GUIStyle { normal = { background = Make1px(reticleColor) } };
        GUI.Box(new Rect(_center.x - w * 0.5f, _center.y - len * 0.5f, w, len), GUIContent.none, bs);
        float hLen = len * 0.45f;
        GUI.Box(new Rect(_center.x - hLen * 0.5f, _center.y - w * 0.5f, hLen, w), GUIContent.none, bs);
        GUI.color = Color.white;
    }

    // ──────────── 读数 ────────────

    private void DrawReadout()
    {
        float val = CurrentMm;
        var s = new GUIStyle(GUI.skin.label)
        { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(0.1f, 0.9f, 0.3f) } };
        GUI.Label(new Rect(_center.x - 100f, _circleRect.yMax + 6f, 200f, 30f),
                  $"A = {(val >= 0f ? "+" : "")}{val:F4} mm", s);
        if (_hasA1)
        {
            var s2 = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.MiddleCenter,
              normal = { textColor = new Color(0.7f, 0.8f, 1f, 0.8f) } };
            GUI.Label(new Rect(_center.x - 100f, _circleRect.yMax + 34f, 200f, 18f),
                      $"A₁ = {_posA1:+0.0000;-0.0000} mm", s2);
        }
        if (_hasA2)
        {
            var s2 = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.MiddleCenter,
              normal = { textColor = new Color(0.7f, 0.8f, 1f, 0.8f) } };
            GUI.Label(new Rect(_center.x - 100f, _circleRect.yMax + 54f, 200f, 18f),
                      $"A₂ = {_posA2:+0.0000;-0.0000} mm", s2);
        }
    }

    // ──────────── 手轮 ────────────

    private void DrawHandwheelBar()
    {
        float y = _circleRect.yMax + 66f;
        float w = Mathf.Min(_diameter * 0.65f, 360f);
        float x = _center.x - w * 0.5f;
        float newPos = GUI.HorizontalSlider(new Rect(x, y, w, 16f), _handwheelPos, 0f, 1f);
        if (newPos != _handwheelPos) _handwheelPos = newPos;
        var lb = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.6f, 0.7f, 0.9f, 0.6f) } };
        GUI.Label(new Rect(x - 36f, y - 1f, 34f, 18f), "◀ 手轮", lb);
        GUI.Label(new Rect(x + w + 2f, y - 1f, 16f, 18f), "▶", lb);
    }

    private void HandleKeyboard()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            float step = 0.001f;
            if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.UpArrow)
            { _handwheelPos = Mathf.Clamp01(_handwheelPos + step); e.Use(); }
            else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.DownArrow)
            { _handwheelPos = Mathf.Clamp01(_handwheelPos - step); e.Use(); }
        }
    }

    // ──────────── 按钮 ────────────

    private void DrawButtons()
    {
        float y = _circleRect.yMax + 100f;
        float bw = 120f, gap = 8f, x0 = _center.x - (bw * 3f + gap * 2f) * 0.5f;

        Color savedColor = GUI.color;
        Color savedBg = GUI.backgroundColor;

        // A₁
        if (!_hasA1)
        {
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.95f);
            if (GUI.Button(new Rect(x0, y, bw, 30f), "📌 记录 A₁"))
            {
                _posA1 = CurrentMm;
                _hasA1 = true;
                _infoText = $"A₁ = {_posA1:+0.0000} mm\n转动手轮，移动数个条纹\n然后点击「记录 A₂」";
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            GUI.Button(new Rect(x0, y, bw, 30f), "✅ A₁ 已记录");
        }

        // A₂
        if (!_hasA1)
        {
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            GUI.Button(new Rect(x0 + bw + gap, y, bw, 30f), "⏳ 请先记录 A₁");
        }
        else if (!_hasA2)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.1f);
            if (GUI.Button(new Rect(x0 + bw + gap, y, bw, 30f), "📌 记录 A₂"))
            {
                _posA2 = CurrentMm;
                _hasA2 = true;
                float d = Mathf.Abs(_posA2 - _posA1);
                Debug.Log($"[测量工具] A₂ 已记录: A₁={_posA1:+0.0000}, A₂={_posA2:+0.0000}, Δ={d:F4}");
                _infoText = $"━━━ 测量结果 ━━━\n"
                          + $"A₁ = {_posA1:+0.0000} mm\n"
                          + $"A₂ = {_posA2:+0.0000} mm\n"
                          + $"Δ = |A₂ - A₁| = {d:F4} mm\n"
                          + $"学生自行计算 Δx = Δ ÷ N\n"
                          + $"并在右侧面板中输入 Δx";
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            GUI.Button(new Rect(x0 + bw + gap, y, bw, 30f), "✅ A₂ 已记录");
        }

        // 重测
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUI.Button(new Rect(x0 + (bw + gap) * 2f, y, bw, 30f), "🔄 重测"))
        {
            _hasA1 = false;
            _hasA2 = false;
            _handwheelPos = 0.5f;
            if (_screenRenderer != null) _screenRenderer.transform.position = _screenOriginPos;
            _infoText = "已重置\n对准亮纹后点击「记录 A₁」";
        }

        // 退出
        float ey = y + 42f;
        GUI.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
        if (GUI.Button(new Rect(_center.x - 50f, ey, 100f, 26f), "✕ 退出测量"))
        {
            StopMeasurement();
            if (_ctrl != null) _ctrl.ResetExperiment();
        }

        GUI.color = savedColor;
        GUI.backgroundColor = savedBg;
    }

    // ──────────── 提示 ────────────

    private void DrawInfo()
    {
        if (string.IsNullOrEmpty(_infoText)) return;
        var s = new GUIStyle(GUI.skin.label)
        { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperCenter,
          normal = { textColor = new Color(0.85f, 0.92f, 1f) } };
        float yOff = _hasA2 ? 40f : (_hasA1 ? 20f : 0f);
        float y = _circleRect.yMax + 156f + yOff;
        GUI.Label(new Rect(_center.x - _diameter * 0.38f, y, _diameter * 0.76f, 120f), _infoText, s);
    }

    // ──────────── 工具 ────────────

    private float CurrentMm => (_handwheelPos - 0.5f) * totalRangeMm;

    private static Texture2D Make1px(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
