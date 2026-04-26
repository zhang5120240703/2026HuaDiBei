using UnityEngine;

/// <summary>
/// 双缝干涉实验控制器
/// 所有阶段转换均有 Debug.Log 输出，便于排查问题
/// </summary>
[AddComponentMenu("DoubleSlit/Double Slit Simple Controller")]
public class DoubleSlitSimpleController : MonoBehaviour
{
    [Header("── Inspector 引用（均可留空，自动查找）──")]
    public Renderer interferenceRenderer;
    public Renderer waveFieldRenderer;
    public DoubleSlitLUTGenerator lutGenerator;
    public ExperimentBenchManager benchManager;
    public ExperimentHintUI hintUI;
    public DoubleSlitParameterManager parameterManager;
    public DoubleSlitFormulaCalculator formulaCalculator;
    public DoubleSlitMeasurementTool measurementTool;

    [Header("── 相机切换 ──")]
    public Camera mainCamera;
    public Camera frontViewCamera;
    private bool _isFrontView = false;

    public bool IsFrontView => _isFrontView;// 供外部查询当前是否为前视相机视角

    public enum ExperimentStep { Setup, Observe, Measure }
    private ExperimentStep _step = ExperimentStep.Setup;
    private bool _paramsValid = false;
    private float _measuredDeltaX = 0f;
    private float _theoreticalDx = 0f;
    private float _error = 0f;
    // 预先计算Shader属性ID，避免运行时字符串查找开销
    static readonly int s_Emission = Shader.PropertyToID("_EmissionColor");
    // 供外部查询当前实验阶段、参数有效性、测量结果等状态
    public ExperimentStep CurrentStep => _step;
    public bool IsParametersValid => _paramsValid;
    public float MeasuredDeltaX => _measuredDeltaX;
    // 理论值和误差优先从 formulaCalculator 获取，若其存在则覆盖本地计算结果，确保一致性
    public float TheoreticalDeltaX
        => formulaCalculator != null ? formulaCalculator.TheoreticalDeltaX : _theoreticalDx;
    // 同理，误差也优先使用 formulaCalculator 的计算结果（如果存在），以保持与公式计算器的同步
    public float CurrentError
        => formulaCalculator != null ? formulaCalculator.CurrentError : _error;

    void Start()
    {
        AutoFind();
        Log("[Start] 各组件引用状态:");
        Log($"  lutGenerator       = {(lutGenerator != null ? "✓" : "✗ 未找到!")}");
        Log($"  benchManager       = {(benchManager != null ? "✓" : "✗ 未找到")}");
        Log($"  hintUI             = {(hintUI != null ? "✓" : "✗ 未找到(将使用Debug.Log)")}");
        Log($"  interferenceRenderer = {(interferenceRenderer != null ? "✓ " + interferenceRenderer.name : "✗ 未找到!")}");

        ShowStage2Objects(false);
        if (benchManager != null)
            benchManager.onExperimentIncorrect.AddListener(OnBenchSetupInvalid);

        ShowHint("🔬 双缝干涉实验\n① 设置参数 ② 确认参数→观察条纹 ③ 测量");
    }

    public void SetParameters(float wavelength, float slitDistance)
    {
        float screenDistance = GetActualScreenDistance();
        Log($"[SetParameters] λ={wavelength} d={slitDistance} L={screenDistance:F3}(自动)");
        if (lutGenerator == null) { Log("  ✗ lutGenerator 为空!"); ShowHint("⚠ 未找到 LUT 生成器！"); return; }

        _paramsValid = (parameterManager != null)
            ? parameterManager.ValidateParameters(wavelength, slitDistance, screenDistance)
            : wavelength >= 380f && wavelength <= 780f
           && slitDistance >= 0.01f && slitDistance <= 1f
           && screenDistance >= 0.1f && screenDistance <= 10f;

        Log($"  → _paramsValid = {_paramsValid}");

        ApplyToLUT(wavelength, slitDistance, screenDistance);

        ShowHint(_paramsValid
            ? $"✅ 参数已设置\n  λ={wavelength:F0}nm  d={slitDistance:F3}mm  L={screenDistance:F2}m(实测)\n  理论 Δx = {_theoreticalDx:F3} mm\n\n点击「确认参数」进入观察阶段"
            : "⚠ 参数超出范围！\n  波长 380–780nm / 缝距 0.01–1mm / 屏距 0.1–10m");
    }

    public void ConfirmParameters()
    {
        Log($"[ConfirmParameters] 当前_step={_step}, _paramsValid={_paramsValid}");

        if (!_paramsValid) { 
            Log("  → 参数无效，返回"); 
            ShowHint("⚠ 请先设置合理参数！（点击「应用参数」按钮）"); 
            return; 
        }

        if (benchManager != null)
        {
            var result = benchManager.ValidateSetup();
            Log($"  bench验证: correct={result.isCorrect}, errors={result.errors.Count}");
            if (!result.isCorrect)
            {
                ShowHint("⚠ 请先正确摆放所有器材！\n" + result.errors[0]);
                return;
            }
        }

        _step = ExperimentStep.Observe;
        Log("  → _step = Observe, 调用 ShowStage2Objects(true)");

        ClearInterferenceRendererEmission();// 确保干涉图案初始时不受发光影响，避免残留亮斑干扰观察
        ShowStage2Objects(true);

        VerifyLUTApplied();// 调试输出当前LUT应用状态，帮助排查干涉图样不显示的问题

        Log("  → ShowStage2Objects 完成");
        ShowHint("👁 干涉图样已显示！\n请观察明暗交替的条纹。");
    }

    private void VerifyLUTApplied()//调试
    {
        if (interferenceRenderer == null) { Log("  [验证] interferenceRenderer 为空!"); return; }
        var mat = interferenceRenderer.sharedMaterial;
        if (mat == null) { Log("  [验证] sharedMaterial 为空!"); return; }
        var tex = mat.GetTexture("_LUT");
        Log($"  [验证] Renderer={interferenceRenderer.name}, Shader={mat.shader?.name ?? "null"}, _LUT纹理={(tex!=null?tex.width+"x"+tex.height:"null")}, enabled={interferenceRenderer.enabled}");
        if (tex == null)
        {
            var instMat = interferenceRenderer.material;
            if (instMat != mat)
            {
                tex = instMat.GetTexture("_LUT");
                Log($"  [验证] material实例 _LUT={(tex!=null?tex.width+"x"+tex.height:"null")}");
            }
        }
    }

    public void StartMeasurement()
    {
        Log($"[StartMeasurement] _step={_step}");
        if (_step != ExperimentStep.Observe) { ShowHint("⚠ 请先完成观察阶段！"); return; }
        _step = ExperimentStep.Measure;
        if (measurementTool != null) measurementTool.StartMeasurement();
        Log("  → _step = Measure");
        ShowHint("📏 测量阶段\n切换到正面视角，使用读数显微镜测量条纹间距。");
    }

    public void RecordMeasurement(float deltaX)//UI调用，传入测量值
    {
        Log($"[RecordMeasurement] deltaX={deltaX} _step={_step}");
        if (_step != ExperimentStep.Measure) { ShowHint("⚠ 请先进入测量阶段！"); return; }

        _measuredDeltaX = deltaX;
        float th = TheoreticalDeltaX;
        _error = th > 0f ? Mathf.Abs(deltaX - th) / th * 100f : 0f;
        if (formulaCalculator != null) formulaCalculator.CalculateError(deltaX, th);

        Log($"  → 误差={_error:F1}%");
        ShowHint($"📏 测量完成\n  测量值 = {deltaX:F3} mm\n  理论值 = {th:F3} mm\n  "
               + (_error < 10f ? $"✅ 误差 {_error:F1}%，通过！" : $"⚠ 误差 {_error:F1}%，请重测"));
    }

    public void ToggleFrontViewCamera()
    {
        if (mainCamera == null || frontViewCamera == null)
        {
            Log("[相机切换] 主相机或前视相机未设置！请在 Inspector 中拖入引用。");
            ShowHint("⚠ 相机未配置！\n请在 Inspector 中设置 mainCamera 和 frontViewCamera。");
            return;
        }

        _isFrontView = !_isFrontView;
        frontViewCamera.enabled = _isFrontView;
        mainCamera.enabled = !_isFrontView;

        Log($"[相机切换] {(_isFrontView ? "前视相机（正面观察干涉图样）" : "主相机（实验台全景）")}");
        ShowHint(_isFrontView
            ? "📷 正面观察干涉图样\n可清晰看到明暗条纹分布。"
            : "🔬 返回实验台全景视角");
    }

    public void ResetExperiment()
    {
        Log("[ResetExperiment]");
        _step = ExperimentStep.Setup;
        _paramsValid = false;
        _measuredDeltaX = _error = 0f;

        if (_isFrontView) ToggleFrontViewCamera();

        if (measurementTool != null) measurementTool.StopMeasurement();

        if (benchManager != null) benchManager.ResetAll();

        ShowStage2Objects(false);
        _paramsValid = false;

        ShowHint("🔄 已重置！\n请重新设置参数后点击「确认参数」。");
    }

    private void ShowStage2Objects(bool show)
    {
        Log($"[ShowStage2Objects] show={show}");

        if (interferenceRenderer != null)
        {
            interferenceRenderer.enabled = true;
            Log($"  interferenceRenderer.enabled = true ({(interferenceRenderer ? interferenceRenderer.name : "null")})");
        }
        else Log("  ✗ interferenceRenderer 为空!");

        if (waveFieldRenderer != null)
        {
            waveFieldRenderer.enabled = show;
            Log($"  waveFieldRenderer.enabled = {show}");
        }

        if (lutGenerator != null)
        {
            lutGenerator.SetBeamsEnabled(show);
            lutGenerator.SetLUTVisible(show);
            Log($"  lutGenerator.SetBeamsEnabled({show}), SetLUTVisible({show})");
        }
        else Log("  ✗ lutGenerator 为空!");
    }

    private void ClearInterferenceRendererEmission()
    {
        if (interferenceRenderer == null) return;
        var mpb = new MaterialPropertyBlock();
        interferenceRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(s_Emission, Color.black);
        interferenceRenderer.SetPropertyBlock(mpb);
        Log("  ClearInterferenceRendererEmission → _EmissionColor = black");
    }

    private void OnBenchSetupInvalid()
    {
        Log($"[OnBenchSetupInvalid] _step={_step}");
        if (_step != ExperimentStep.Setup)
        {
            _step = ExperimentStep.Setup;
            ShowStage2Objects(false);
            _paramsValid = false;
            ShowHint("⚠ 器材被移动，请重新对齐后再「确认参数」。");
        }
    }

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

        lutGenerator.ForceRegenerate();
        _theoreticalDx = d > 0f ? (wl * 1e-9f * L / (d * 1e-3f)) * 1000f : 0f;

        if (formulaCalculator != null)
            formulaCalculator.CalculateTheoreticalDeltaX(wl, L, d);
    }

    private float GetActualScreenDistance()
    {
        if (benchManager == null || benchManager.doubleSlit == null || benchManager.screen == null)
            return 1f;
        return benchManager.GetDistanceAlongBench(benchManager.doubleSlit, benchManager.screen);
    }

    private void AutoFind()
    {
        if (lutGenerator == null) lutGenerator = FindObjectOfType<DoubleSlitLUTGenerator>();
        if (benchManager == null) benchManager = FindObjectOfType<ExperimentBenchManager>();
        if (hintUI == null) hintUI = FindObjectOfType<ExperimentHintUI>();
        if (parameterManager == null) parameterManager = FindObjectOfType<DoubleSlitParameterManager>();
        if (formulaCalculator == null) formulaCalculator = FindObjectOfType<DoubleSlitFormulaCalculator>();
        if (measurementTool == null) measurementTool = FindObjectOfType<DoubleSlitMeasurementTool>();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera != null) Log($"  AutoFind 主相机: {mainCamera.name}");
        }

        if (frontViewCamera == null)
        {
            var allCams = FindObjectsOfType<Camera>();
            foreach (var cam in allCams)
            {
                if (cam != mainCamera && !cam.name.Contains("UI"))
                {
                    frontViewCamera = cam;
                    frontViewCamera.enabled = false;
                    Log($"  AutoFind 前视相机: {cam.name} (已禁用)");
                    break;
                }
            }
            if (frontViewCamera == null && allCams.Length > 1)
            {
                foreach (var cam in allCams)
                {
                    if (cam != mainCamera)
                    {
                        frontViewCamera = cam;
                        frontViewCamera.enabled = false;
                        Log($"  AutoFind 前视相机(备选): {cam.name} (已禁用)");
                        break;
                    }
                }
            }
        }
        else
        {
            frontViewCamera.enabled = false;
        }

        if (waveFieldRenderer == null)
        {
            var wfv = FindObjectOfType<WaveFieldVisualizer>();
            if (wfv != null) waveFieldRenderer = wfv.GetComponent<Renderer>();
        }

        if (interferenceRenderer == null && lutGenerator != null)
            interferenceRenderer = lutGenerator.interferenceRenderer;

        if (interferenceRenderer == null)
        {
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name == "Custom/DoubleSlit")
                {
                    interferenceRenderer = r;
                    Log($"  AutoFind 找到干涉渲染器: {r.name}");
                    break;
                }
            }
        }

        if (interferenceRenderer == null)
            Log("  ⚠ 未找到任何 Custom/DoubleSlit 渲染器！干涉图案将无法显示！");

        if (lutGenerator != null && interferenceRenderer != null)
        {
            if (lutGenerator.interferenceRenderer == null)
                lutGenerator.interferenceRenderer = interferenceRenderer;
        }
    }

    private void ShowHint(string msg)
    {
        Log("[Hint] " + msg.Replace("\n", " | "));
        if (hintUI != null) hintUI.ShowHint(msg);
    }

    private void Log(string msg) => Debug.Log("[双缝实验] " + msg);
}
