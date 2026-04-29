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
    public float measuredDeltaX = 0.63f;
    public float playerTheoryDeltaX = 0f;
    private string _measureInput = "0.63";
    private string _theoryInput = "";

    // ── 运行时 ─────────────────────────────────────────────────
    private string _log = "";
    private Vector2 _scroll;

    // ── 样式（懒加载）─────────────────────────────────────────
    private bool _built;
    private GUIStyle _sTitle, _sStep, _sLabel, _sBold, _sLog, _sBtn, _sDiv, _sTextField;
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

        if (hasCtrl)
        {
            float actualL = Ctrl().benchManager != null && Ctrl().benchManager.doubleSlit != null && Ctrl().benchManager.screen != null
                ? Ctrl().benchManager.GetDistanceAlongBench(Ctrl().benchManager.doubleSlit, Ctrl().benchManager.screen)
                : 0f;
            GUI.color = CDm;
            GUILayout.Label($"屏距  L = {actualL:F2} m (实测)", _sLabel);
            GUI.color = Color.white;
        }
        GUILayout.Space(4);

        GUI.enabled = hasCtrl;
        if (Btn("应用参数"))
        {
            Ctrl().SetParameters(wavelength, slitDistance);
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
        StepHead("③ 理论计算 & 测量验证", step == DoubleSlitSimpleController.ExperimentStep.Measure);

        if (hasCtrl)
        {
            float th = Ctrl().TheoreticalDeltaX;

            GUI.color = new Color(0.3f, 0.9f, 1f);
            GUILayout.Label("📐 理论计算（使用 Δx = λL/d 计算理论值）", _sBold);
            GUI.color = Color.white;
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            GUILayout.Label("理论 Δx = ", _sBold, GUILayout.Width(72));
            string prevTheory = _theoryInput;
            _theoryInput = GUILayout.TextField(_theoryInput, _sTextField, GUILayout.Width(70));
            if (_theoryInput != prevTheory)
            {
                if (float.TryParse(_theoryInput, out float val))
                    playerTheoryDeltaX = Mathf.Clamp(val, 0.01f, 100f);
            }
            GUILayout.Label(" mm", _sBold);
            GUILayout.EndHorizontal();

            GUI.color = CDm;
            GUILayout.Label($"  正确理论值 ≈ {th:F3} mm", _sLabel);
            GUI.color = Color.white;

            GUILayout.Space(8);

            GUI.color = new Color(0.3f, 0.9f, 1f);
            GUILayout.Label("📏 实验测量（使用读数显微镜测量条纹间距）", _sBold);
            GUI.color = Color.white;
            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            GUILayout.Label("测量 Δx = ", _sBold, GUILayout.Width(72));
            string prevInput = _measureInput;
            _measureInput = GUILayout.TextField(_measureInput, _sTextField, GUILayout.Width(70));
            if (_measureInput != prevInput)
            {
                if (float.TryParse(_measureInput, out float val))
                    measuredDeltaX = Mathf.Clamp(val, 0.01f, 20f);
            }
            GUILayout.Label(" mm", _sBold);
            GUILayout.EndHorizontal();

            float measErr = th > 0f ? Mathf.Abs(measuredDeltaX - th) / th * 100f : 0f;
            GUI.color = measErr < 5f ? new Color(0.3f, 1f, 0.5f)
                      : measErr < 15f ? new Color(1f, 0.85f, 0.2f)
                      : new Color(1f, 0.3f, 0.3f);
            GUILayout.Label($"  误差 {measErr:F1}%（需 ≤15%）", _sLabel);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(1f, 0.6f, 0.2f);
            GUILayout.Label("⚠ 控制器未连接", _sBold);
            GUI.color = Color.white;
        }
        GUILayout.Space(6);

        bool canValidate = hasCtrl && step == DoubleSlitSimpleController.ExperimentStep.Measure;
        GUI.enabled = canValidate;
        GUI.color = canValidate ? new Color(0.3f, 1f, 0.5f) : Color.gray;
        if (Btn("验证并完成实验  🎯"))
        {
            Ctrl().ValidateForSuccess(playerTheoryDeltaX, measuredDeltaX);
            Refresh();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        Div();

        // ── 步骤 4：成功 ──────────────────────────────────────
        if (step == DoubleSlitSimpleController.ExperimentStep.Success)
        {
            GUILayout.Space(8);
            GUI.color = new Color(0.3f, 1f, 0.5f);
            GUILayout.Label("🎉  恭喜实验成功！", _sTitle);
            GUI.color = Color.white;
            GUILayout.Space(6);

            float th = hasCtrl ? Ctrl().TheoreticalDeltaX : 0f;
            float theoryErr = th > 0f ? Mathf.Abs(playerTheoryDeltaX - th) / th * 100f : 0f;
            float measErr = th > 0f ? Mathf.Abs(measuredDeltaX - th) / th * 100f : 0f;

            GUI.color = new Color(0.3f, 1f, 0.5f);
            GUILayout.Label("✅ 理论计算通过", _sBold);
            GUI.color = Color.white;
            GUILayout.Label($"  你的答案：{playerTheoryDeltaX:F3} mm", _sLabel);
            GUILayout.Label($"  正确值：{th:F3} mm", _sLabel);
            GUILayout.Label($"  误差：{theoryErr:F1}%", _sLabel);
            GUILayout.Space(4);

            GUI.color = new Color(0.3f, 1f, 0.5f);
            GUILayout.Label("✅ 实验测量通过", _sBold);
            GUI.color = Color.white;
            GUILayout.Label($"  测量结果：{measuredDeltaX:F3} mm", _sLabel);
            GUILayout.Label($"  正确值：{th:F3} mm", _sLabel);
            GUILayout.Label($"  误差：{measErr:F1}%", _sLabel);
            GUILayout.Space(8);

            GUI.color = CAc;
            GUILayout.Label("💡 双缝干涉公式：Δx = λL / d", _sLabel);
            if (hasCtrl)
            {
                GUILayout.Label($"  其中 λ={Ctrl().CurrentWavelength:F0}nm, d={Ctrl().CurrentSlitDistance:F3}mm", _sLabel);
            }
            GUI.color = Color.white;

            GUILayout.Space(10);

            GUI.enabled = hasCtrl;
            GUI.color = new Color(1f, 0.6f, 0.2f);
            if (Btn("🔄 重新实验"))
            {
                if (hasCtrl) { Ctrl().ResetExperiment(); Refresh(); }
            }
            GUI.color = Color.white;
            GUI.enabled = true;

            Div();
        }

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
        wavelength = 632.8f; slitDistance = 0.1f;
        Ctrl().ResetExperiment();
        Ctrl().SetParameters(wavelength, slitDistance);
        Ctrl().ConfirmParameters();
        Ctrl().StartMeasurement();

        float th = Ctrl().TheoreticalDeltaX;
        playerTheoryDeltaX = th;
        _theoryInput = th.ToString("F3");
        measuredDeltaX = th;
        _measureInput = th.ToString("F3");
        Ctrl().ValidateForSuccess(playerTheoryDeltaX, measuredDeltaX);
        Refresh();
    }

    void Refresh()
    {
        if (Ctrl() == null) { _log = "❌ 控制器未找到"; return; }
        var step = Ctrl().CurrentStep;
        if (step == DoubleSlitSimpleController.ExperimentStep.Success)
        {
            _log = $"🎉 实验成功完成！\n"
                 + $"理论 Δx(你的答案) = {playerTheoryDeltaX:F3} mm\n"
                 + $"测量 Δx(实验结果) = {measuredDeltaX:F3} mm\n"
                 + $"正确理论值 = {Ctrl().TheoreticalDeltaX:F3} mm";
        }
        else
        {
            _log = $"阶段：{StepName(step)}\n"
                 + $"参数：{(Ctrl().IsParametersValid ? "✓ 有效" : "✗ 未设置")}\n"
                 + $"正确理论 Δx = {Ctrl().TheoreticalDeltaX:F3} mm\n"
                 + $"测量 Δx = {Ctrl().MeasuredDeltaX:F3} mm\n"
                 + $"误差    = {Ctrl().CurrentError:F1}%";
        }
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
        DoubleSlitSimpleController.ExperimentStep.Measure => "理论计算 & 测量验证",
        DoubleSlitSimpleController.ExperimentStep.Success => "实验成功 🎉",
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
        _sTextField = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white, background = _tBtn },
            margin = new RectOffset(0, 0, 2, 2)
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
    }
#endif
}