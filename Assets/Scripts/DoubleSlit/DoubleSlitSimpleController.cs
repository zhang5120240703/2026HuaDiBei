using UnityEngine;

/// <summary>
/// 简化双缝干涉实验控制器（直接控制版）
///
/// 核心策略：完全不碰材质属性。
///   显示 / 隐藏 = 直接切换 Renderer.enabled 或 Component.enabled
///   第一阶段：干涉 Quad、光束、波场 Quad 全部隐藏
///   第二阶段：全部显示，LUT 已在 Start 时生成完毕，直接可用
///
/// Inspector 配置：
///   interferenceRenderer  → 干涉图案 Quad 的 MeshRenderer
///   waveFieldComponent    → WaveFieldVisualizer 组件（或其 Renderer）
///   lutGenerator          → DoubleSlitLUTGenerator
/// </summary>
[AddComponentMenu("DoubleSlit/Double Slit Simple Controller")]
public class DoubleSlitSimpleController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Inspector 字段
    // ════════════════════════════════════════════════════════════

    [Header("── 第二阶段才显示的对象（直接拖入 Renderer / Component）──")]

    [Tooltip("干涉条纹 Quad 的 MeshRenderer")]
    public Renderer interferenceRenderer;

    [Tooltip("波场传播 Quad 的 MeshRenderer（WaveFieldVisualizer 所在 Quad）")]
    public Renderer waveFieldRenderer;

    [Tooltip("LUT 生成器，控制光束 LineRenderer")]
    public DoubleSlitLUTGenerator lutGenerator;

    [Header("── 可选引用 ──")]
    public ExperimentBenchManager benchManager;
    public ExperimentHintUI hintUI;
    public DoubleSlitParameterManager parameterManager;
    public DoubleSlitFormulaCalculator formulaCalculator;
    public DoubleSlitMeasurementTool measurementTool;

    [Header("── 默认参数 ──")]
    [Range(380f, 780f)] public float defaultWavelength = 632.8f;
    [Range(0.01f, 1f)] public float defaultSlitDistance = 0.1f;
    [Range(0.1f, 5f)] public float defaultScreenDistance = 1f;

    // ════════════════════════════════════════════════════════════
    //  枚举 & 状态
    // ════════════════════════════════════════════════════════════

    public enum ExperimentStep { Setup, Observe, Measure }

    private ExperimentStep _step = ExperimentStep.Setup;
    private bool _paramsValid = false;
    private float _measuredDeltaX = 0f;
    private float _theoreticalDx = 0f;   // mm
    private float _error = 0f;   // %

    // ════════════════════════════════════════════════════════════
    //  公开属性
    // ════════════════════════════════════════════════════════════

    public ExperimentStep CurrentStep => _step;
    public bool IsParametersValid => _paramsValid;
    public float MeasuredDeltaX => _measuredDeltaX;
    public float TheoreticalDeltaX
        => formulaCalculator != null ? formulaCalculator.TheoreticalDeltaX : _theoreticalDx;
    public float CurrentError
        => formulaCalculator != null ? formulaCalculator.CurrentError : _error;

    // ════════════════════════════════════════════════════════════
    //  生命周期
    // ════════════════════════════════════════════════════════════

    void Start()
    {
        AutoFind();

        // ★ 第一阶段：全部隐藏（直接 enabled = false）
        ShowStage2Objects(false);

        // LUT 在 LUTGenerator.Start() 里已生成，这里只是应用默认参数
        ApplyToLUT(defaultWavelength, defaultSlitDistance, defaultScreenDistance);

        ShowHint("🔬 双缝干涉实验\n" +
                 "① 设置参数（波长 / 缝距 / 屏距）\n" +
                 "② 点击「确认参数」——干涉条纹和光路才会出现\n" +
                 "③ 测量条纹间距，验证 Δx = λL/d");
    }

    // ════════════════════════════════════════════════════════════
    //  公开接口
    // ════════════════════════════════════════════════════════════

    public void SetParameters(float wavelength, float slitDistance, float screenDistance)
    {
        if (lutGenerator == null)
        {
            ShowHint("⚠ 未找到 LUT 生成器！");
            return;
        }

        _paramsValid = (parameterManager != null)
            ? parameterManager.ValidateParameters(wavelength, slitDistance, screenDistance)
            : wavelength >= 380f && wavelength <= 780f
           && slitDistance >= 0.01f && slitDistance <= 1f
           && screenDistance >= 0.1f && screenDistance <= 10f;

        ApplyToLUT(wavelength, slitDistance, screenDistance);

        ShowHint(_paramsValid
            ? $"✅ 参数已设置\n  λ={wavelength:F0}nm  d={slitDistance:F3}mm  L={screenDistance:F2}m\n  理论 Δx = {_theoreticalDx:F3} mm\n\n点击「确认参数」进入观察阶段"
            : "⚠ 参数超出范围！\n  波长 380–780nm / 缝距 0.01–1mm / 屏距 0.1–10m");
    }

    public void ConfirmParameters()
    {
        if (!_paramsValid) { ShowHint("⚠ 请先设置合理参数！"); return; }

        _step = ExperimentStep.Observe;

        // ★ 进入第二阶段：直接打开所有渲染器
        ShowStage2Objects(true);

        ShowHint("👁 干涉图样已显示！\n请观察明暗交替的条纹，然后点击「开始测量」。");
    }

    public void StartMeasurement()
    {
        if (_step != ExperimentStep.Observe) { ShowHint("⚠ 请先完成观察阶段！"); return; }
        _step = ExperimentStep.Measure;
        ShowHint("📏 测量阶段\n请调节标尺，测量相邻亮纹间距 Δx (mm)。");
    }

    public void RecordMeasurement(float deltaX)
    {
        if (_step != ExperimentStep.Measure) { ShowHint("⚠ 请先进入测量阶段！"); return; }

        _measuredDeltaX = deltaX;
        float th = TheoreticalDeltaX;
        _error = th > 0f ? Mathf.Abs(deltaX - th) / th * 100f : 0f;
        if (formulaCalculator != null) formulaCalculator.CalculateError(deltaX, th);

        ShowHint($"📏 测量完成\n  测量值 = {deltaX:F3} mm\n  理论值 = {th:F3} mm\n  "
               + (_error < 10f ? $"✅ 误差 {_error:F1}%，通过！" : $"⚠ 误差 {_error:F1}%，请重测"));
    }

    public void ResetExperiment()
    {
        _step = ExperimentStep.Setup;
        _paramsValid = false;
        _measuredDeltaX = _error = 0f;

        // ★ 回到第一阶段：直接隐藏所有
        ShowStage2Objects(false);
        ApplyToLUT(defaultWavelength, defaultSlitDistance, defaultScreenDistance);
        _paramsValid = false;

        ShowHint("🔄 已重置！\n请重新设置参数后点击「确认参数」。");
    }

    // ════════════════════════════════════════════════════════════
    //  核心：直接切换所有第二阶段对象
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ 唯一的显隐控制入口
    /// 第一阶段（show=false）：光屏可见但显示白色
    /// 第二阶段（show=true）：光屏显示干涉图案
    /// </summary>
    private void ShowStage2Objects(bool show)
    {
        // 1. 干涉条纹 Quad - 始终显示（第一阶段为白色，第二阶段为干涉图案）
        if (interferenceRenderer != null)
            interferenceRenderer.enabled = true;

        // 2. 波场传播 Quad
        if (waveFieldRenderer != null)
            waveFieldRenderer.enabled = show;

        // 3. 光路光束（LUTGenerator 的 LineRenderer）
        if (lutGenerator != null)
            lutGenerator.SetBeamsEnabled(show);

        // 4. 控制干涉图案的LUT显示
        if (lutGenerator != null)
            lutGenerator.SetLUTVisible(show);
    }

    // ════════════════════════════════════════════════════════════
    //  辅助
    // ════════════════════════════════════════════════════════════

    private void ApplyToLUT(float wl, float d, float L)
    {
        if (lutGenerator == null) return;

        if (parameterManager != null)
            parameterManager.ApplyParametersToLUT(lutGenerator, wl, d, L);
        else
        {
            lutGenerator.wavelength = wl;
            lutGenerator.slitDistance = d;
            lutGenerator.screenDistance = L;
        }

        // 内置理论值 Δx = λL/d → mm
        _theoreticalDx = d > 0f ? (wl * 1e-9f * L / (d * 1e-3f)) * 1000f : 0f;

        if (formulaCalculator != null)
            formulaCalculator.CalculateTheoreticalDeltaX(wl, L, d);
    }

    private void AutoFind()
    {
        if (lutGenerator == null) lutGenerator = FindObjectOfType<DoubleSlitLUTGenerator>();
        if (benchManager == null) benchManager = FindObjectOfType<ExperimentBenchManager>();
        if (hintUI == null) hintUI = FindObjectOfType<ExperimentHintUI>();
        if (parameterManager == null) parameterManager = FindObjectOfType<DoubleSlitParameterManager>();
        if (formulaCalculator == null) formulaCalculator = FindObjectOfType<DoubleSlitFormulaCalculator>();
        if (measurementTool == null) measurementTool = FindObjectOfType<DoubleSlitMeasurementTool>();

        // 波场：自动查找 WaveFieldVisualizer 的 Renderer
        if (waveFieldRenderer == null)
        {
            var wfv = FindObjectOfType<WaveFieldVisualizer>();
            if (wfv != null) waveFieldRenderer = wfv.GetComponent<Renderer>();
        }
    }

    private void ShowHint(string msg)
    {
        if (hintUI != null) hintUI.ShowHint(msg);
        else Debug.Log("[双缝实验] " + msg);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        defaultWavelength = Mathf.Clamp(defaultWavelength, 380f, 780f);
        defaultSlitDistance = Mathf.Clamp(defaultSlitDistance, 0.01f, 1f);
        defaultScreenDistance = Mathf.Clamp(defaultScreenDistance, 0.1f, 10f);
    }
#endif
}