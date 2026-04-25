using UnityEngine;

/// <summary>
/// 双缝干涉实验 IMGUI 测试器
/// 位于屏幕右侧，分阶段引导，按钮与流程联动
/// </summary>
[AddComponentMenu("DoubleSlit/Double Slit Simple Tester")]
public class DoubleSlitSimpleTester : MonoBehaviour
{
    [Header("组件引用")]
    public DoubleSlitSimpleController experimentController;
    [Range(260, 400)] public int panelWidth = 280;

    [Header("参数默认值")]
    [Range(380f, 780f)] public float wavelength = 632.8f;
    [Range(0.01f, 1f)] public float slitDistance = 0.1f;
    [Range(0.1f, 5f)] public float screenDistance = 1f;
    [Range(0.01f, 5f)] public float measuredDeltaX = 0.63f;

    // ── 运行时 ─────────────────────────────────────────────────
    private string _log = "";
    private Vector2 _scroll;

    // ── 样式（懒加载）─────────────────────────────────────────
    private bool _built;
    private GUIStyle _sTitle, _sStep, _sLabel, _sBold, _sLog, _sBtn, _sDiv;
    private Texture2D _tPanel, _tBtn, _tBtnH, _tBtnA, _tLog;

    static readonly Color CB = new Color(0.07f, 0.09f, 0.17f, 0.95f);
    static readonly Color CAc = new Color(0.25f, 0.60f, 1.00f);
    static readonly Color CDm = new Color(0.45f, 0.55f, 0.72f);
    static readonly Color CTx = new Color(0.88f, 0.92f, 1.00f);
    static readonly Color CBt = new Color(0.12f, 0.24f, 0.48f);
    static readonly Color CBH = new Color(0.20f, 0.40f, 0.70f);
    static readonly Color CBA = new Color(0.06f, 0.14f, 0.32f);
    static readonly Color CDv = new Color(0.22f, 0.32f, 0.52f, 0.6f);

    // ══════════════════════════════════════════════════════════
    void Start()
    {
        if (experimentController == null)
            experimentController = FindObjectOfType<DoubleSlitSimpleController>();
        Refresh();
    }

    void OnGUI()
    {
        Build();

        int px = Screen.width - panelWidth - 10;
        int ph = Screen.height - 20;

        // 背景
        GUI.color = CB;
        GUI.Box(new Rect(px - 4, 8, panelWidth + 8, ph + 4), GUIContent.none,
                new GUIStyle { normal = { background = _tPanel } });
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px, 10, panelWidth, ph));
        _scroll = GUILayout.BeginScrollView(_scroll, false, false,
                    GUILayout.Width(panelWidth), GUILayout.Height(ph));

        // ── 标题 ──────────────────────────────────────────────
        GUILayout.Space(6);
        GUILayout.Label("🔬  双缝干涉实验", _sTitle);
        Div();

        var step = Ctrl()?.CurrentStep ?? DoubleSlitSimpleController.ExperimentStep.Setup;

        bool hasCtrl = Ctrl() != null;
        bool hasHintUI = Ctrl() != null && Ctrl().hintUI != null;
        bool hasLUT = Ctrl() != null && Ctrl().lutGenerator != null;
        bool hasRenderer = Ctrl() != null && Ctrl().interferenceRenderer != null;

        if (!hasCtrl || !hasHintUI || !hasLUT || !hasRenderer)
        {
            GUI.color = new Color(1f, 0.5f, 0.2f);
            string warn = "⚠ 组件缺失:";
            if (!hasCtrl) warn += " [控制器]";
            if (!hasHintUI) warn += " [HintUI]";
            if (!hasLUT) warn += " [LUTGen]";
            if (!hasRenderer) warn += " [光屏Renderer]";
            GUILayout.Label(warn, _sBold);
            GUI.color = Color.white;
            Div();
        }

        // ── 步骤 1 ─────────────────────────────────────────────
        StepHead("① 参数设置", step == DoubleSlitSimpleController.ExperimentStep.Setup);

        GUILayout.Label($"波长  λ = {wavelength:F0} nm", _sBold);
        wavelength = HSlider(wavelength, 380f, 780f);

        GUILayout.Label($"缝距  d = {slitDistance:F3} mm", _sBold);
        slitDistance = HSlider(slitDistance, 0.01f, 1f);

        GUILayout.Label($"屏距  L = {screenDistance:F2} m", _sBold);
        screenDistance = HSlider(screenDistance, 0.1f, 5f);

        float preview = slitDistance > 0f
            ? (wavelength * 1e-9f * screenDistance / (slitDistance * 1e-3f)) * 1000f
            : 0f;
        GUI.color = CDm;
        GUILayout.Label($"  预测 Δx ≈ {preview:F3} mm", _sLabel);
        GUI.color = Color.white;
        GUILayout.Space(4);

        GUI.enabled = hasCtrl;
        if (Btn("应用参数"))
        {
            Ctrl().SetParameters(wavelength, slitDistance, screenDistance);
            Refresh();
        }

        bool canConfirm = hasCtrl && Ctrl().IsParametersValid;
        GUI.enabled = canConfirm;
        GUI.color = canConfirm ? CAc : Color.gray;
        if (Btn("确认参数  →  进入观察 ▶"))
        {
            Ctrl().ConfirmParameters();
            Refresh();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        Div();

        // ── 步骤 2 ─────────────────────────────────────────────
        StepHead("② 观察图样", step == DoubleSlitSimpleController.ExperimentStep.Observe);

        if (hasCtrl)
        {
            GUI.color = CDm;
            GUILayout.Label($"  理论 Δx = {Ctrl().TheoreticalDeltaX:F3} mm", _sLabel);
            GUI.color = Color.white;
        }

        bool isObserve = hasCtrl && step == DoubleSlitSimpleController.ExperimentStep.Observe;
        if (isObserve)
        {
            bool hasFrontCam = Ctrl().frontViewCamera != null;
            bool isFront = Ctrl().IsFrontView;

            GUI.enabled = hasFrontCam;
            GUI.color = hasFrontCam
                ? (isFront ? new Color(1f, 0.6f, 0.2f) : new Color(0.2f, 0.8f, 1f))
                : Color.gray;
            if (Btn(isFront ? "🔙 返回全景视角" : "📷 正面观察干涉图样"))
            {
                Ctrl().ToggleFrontViewCamera();
                Refresh();
            }
            GUI.color = Color.white;
            GUI.enabled = true;
        }
        GUILayout.Space(4);

        bool canMeasure = hasCtrl && step == DoubleSlitSimpleController.ExperimentStep.Observe;
        GUI.enabled = canMeasure;
        GUI.color = canMeasure ? CAc : Color.gray;
        if (Btn("开始测量  →"))
        {
            Ctrl().StartMeasurement();
            Refresh();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        Div();

        // ── 步骤 3 ─────────────────────────────────────────────
        StepHead("③ 测量条纹", step == DoubleSlitSimpleController.ExperimentStep.Measure);

        GUILayout.Label($"Δx = {measuredDeltaX:F3} mm", _sBold);
        measuredDeltaX = HSlider(measuredDeltaX, 0.01f, 5f);

        if (hasCtrl)
        {
            float th = Ctrl().TheoreticalDeltaX;
            float err = th > 0f ? Mathf.Abs(measuredDeltaX - th) / th * 100f : 0f;
            GUI.color = err < 5f ? new Color(0.3f, 1f, 0.5f)
                      : err < 15f ? new Color(1f, 0.85f, 0.2f)
                      : new Color(1f, 0.3f, 0.3f);
            GUILayout.Label($"  实时误差估算：{err:F1}%", _sLabel);
            GUI.color = Color.white;
        }
        GUILayout.Space(4);

        bool canRecord = hasCtrl && step == DoubleSlitSimpleController.ExperimentStep.Measure;
        GUI.enabled = canRecord;
        GUI.color = canRecord ? new Color(0.3f, 1f, 0.5f) : Color.gray;
        if (Btn("提交测量结果  ✓"))
        {
            Ctrl().RecordMeasurement(measuredDeltaX);
            Refresh();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        Div();

        // ── 控制 ──────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        if (Btn("🔄 重置"))
        {
            if (hasCtrl) { Ctrl().ResetExperiment(); Refresh(); }
        }
        GUILayout.Space(6);
        if (Btn("⚡ 快速测试"))
            QuickTest();
        GUILayout.EndHorizontal();

        Div();

        // ── 日志 ──────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_log))
            GUILayout.Label(_log, _sLog);

        GUILayout.Space(8);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ══════════════════════════════════════════════════════════

    void QuickTest()
    {
        if (Ctrl() == null) return;
        wavelength = 632.8f; slitDistance = 0.1f; screenDistance = 1f;
        // 理论值 = 632.8e-9 * 1 / 0.1e-3 * 1000 = 6.328mm
        measuredDeltaX = 6.328f;
        Ctrl().ResetExperiment();
        Ctrl().SetParameters(wavelength, slitDistance, screenDistance);
        Ctrl().ConfirmParameters();
        Ctrl().StartMeasurement();
        Ctrl().RecordMeasurement(measuredDeltaX);
        Refresh();
    }

    void Refresh()
    {
        if (Ctrl() == null) { _log = "❌ 控制器未找到"; return; }
        _log = $"阶段：{StepName(Ctrl().CurrentStep)}\n"
             + $"参数：{(Ctrl().IsParametersValid ? "✓ 有效" : "✗ 未设置")}\n"
             + $"理论 Δx = {Ctrl().TheoreticalDeltaX:F3} mm\n"
             + $"测量 Δx = {Ctrl().MeasuredDeltaX:F3} mm\n"
             + $"误差    = {Ctrl().CurrentError:F1}%";
    }

    // ── 小工具 ────────────────────────────────────────────────

    DoubleSlitSimpleController Ctrl() => experimentController;

    bool Btn(string label) => GUILayout.Button(label, _sBtn);

    float HSlider(float v, float min, float max)
        => GUILayout.HorizontalSlider(v, min, max);

    void StepHead(string label, bool active)
    {
        GUI.color = active ? CAc : CDm;
        GUILayout.Label(label, _sStep);
        GUI.color = Color.white;
    }

    void Div()
    {
        GUILayout.Space(4);
        GUI.color = CDv;
        GUILayout.Box(GUIContent.none, _sDiv, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        GUI.color = Color.white;
        GUILayout.Space(4);
    }

    static string StepName(DoubleSlitSimpleController.ExperimentStep s) => s switch
    {
        DoubleSlitSimpleController.ExperimentStep.Setup => "参数设置",
        DoubleSlitSimpleController.ExperimentStep.Observe => "观察图样",
        DoubleSlitSimpleController.ExperimentStep.Measure => "测量条纹",
        _ => "未知"
    };

    // ── 样式构建 ──────────────────────────────────────────────

    void Build()
    {
        if (_built) return;
        _built = true;
        _tPanel = Tex(CB);
        _tBtn = Tex(CBt);
        _tBtnH = Tex(CBH);
        _tBtnA = Tex(CBA);
        _tLog = Tex(new Color(0.04f, 0.08f, 0.16f, 0.9f));

        _sTitle = S(17, FontStyle.Bold, Color.white);
        _sStep = S(13, FontStyle.Bold, CAc);
        _sLabel = S(12, FontStyle.Normal, CTx, wordWrap: true);
        _sBold = S(13, FontStyle.Bold, CTx);
        _sDiv = new GUIStyle { normal = { background = Tex(CDv) } };
        _sLog = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            padding = new RectOffset(8, 8, 6, 6),
            normal = { textColor = CDm, background = _tLog }
        };
        _sBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(6, 6, 8, 8),
            margin = new RectOffset(0, 0, 2, 2),
            normal = { textColor = Color.white, background = _tBtn },
            hover = { textColor = Color.white, background = _tBtnH },
            active = { textColor = Color.white, background = _tBtnA }
        };
    }

    static GUIStyle S(int size, FontStyle fs, Color c, bool wordWrap = false)
        => new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, wordWrap = wordWrap, normal = { textColor = c } };

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c); t.Apply(); return t;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        wavelength = Mathf.Clamp(wavelength, 380f, 780f);
        slitDistance = Mathf.Clamp(slitDistance, 0.01f, 1f);
        screenDistance = Mathf.Clamp(screenDistance, 0.1f, 5f);
        measuredDeltaX = Mathf.Clamp(measuredDeltaX, 0.01f, 5f);
    }
#endif
}