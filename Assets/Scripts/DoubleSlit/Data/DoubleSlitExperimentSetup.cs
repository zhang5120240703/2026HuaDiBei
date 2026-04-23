using UnityEngine;

/// <summary>
/// 简化的双缝干涉实验场景配置
/// 专注于3个核心步骤的配置
/// </summary>
[AddComponentMenu("DoubleSlit/Double Slit Experiment Setup")]
public class DoubleSlitExperimentSetup : MonoBehaviour
{
    [Header("── 核心组件引用 ──")]
    public ExperimentBenchManager benchManager;
    public DoubleSlitLUTGenerator lutGenerator;
    public ExperimentHintUI hintUI;
    public DoubleSlitSimpleController experimentController;
    public DoubleSlitMeasurementTool measurementTool;
    public DoubleSlitParameterManager parameterManager;
    public DoubleSlitFormulaCalculator formulaCalculator;

    [Header("── 场景物体引用 ──")]
    public GameObject lightSource;
    public GameObject singleSlit;
    public GameObject doubleSlit;
    public GameObject screen;

    void Start()
    {
        // 自动配置组件引用
        AutoConfigureComponents();
        
        // 验证配置完整性
        ValidateConfiguration();
        
        Debug.Log("[DoubleSlit] 简化的实验场景配置完成！");
    }

    /// <summary>
    /// 自动配置组件引用
    /// </summary>
    private void AutoConfigureComponents()
    {
        // 查找缺失的组件
        if (benchManager == null)
            benchManager = FindObjectOfType<ExperimentBenchManager>();
        
        if (lutGenerator == null)
            lutGenerator = FindObjectOfType<DoubleSlitLUTGenerator>();
        
        if (hintUI == null)
            hintUI = FindObjectOfType<ExperimentHintUI>();
        
        if (experimentController == null)
            experimentController = FindObjectOfType<DoubleSlitSimpleController>();
        
        if (measurementTool == null)
            measurementTool = FindObjectOfType<DoubleSlitMeasurementTool>();
        
        if (parameterManager == null)
            parameterManager = FindObjectOfType<DoubleSlitParameterManager>();
        
        if (formulaCalculator == null)
            formulaCalculator = FindObjectOfType<DoubleSlitFormulaCalculator>();
        
        // 配置简化的实验控制器
        if (experimentController != null)
        {
            experimentController.benchManager = benchManager;
            experimentController.lutGenerator = lutGenerator;
            experimentController.hintUI = hintUI;
            experimentController.parameterManager = parameterManager;
            experimentController.formulaCalculator = formulaCalculator;
            experimentController.measurementTool = measurementTool;
        }
        
        // 配置LUT生成器
        if (lutGenerator != null && benchManager != null)
        {
            // 设置光源、单缝、双缝的Transform引用
            if (lightSource != null) lutGenerator.lightSourceTf = lightSource.transform;
            if (singleSlit != null) lutGenerator.singleSlitTf = singleSlit.transform;
            if (doubleSlit != null) lutGenerator.doubleSlitTf = doubleSlit.transform;
        }
    }

    /// <summary>
    /// 验证配置完整性
    /// </summary>
    private void ValidateConfiguration()
    {
        bool configValid = true;
        
        if (benchManager == null)
        {
            Debug.LogError("❌ 缺少 ExperimentBenchManager 组件！");
            configValid = false;
        }
        
        if (lutGenerator == null)
        {
            Debug.LogError("❌ 缺少 DoubleSlitLUTGenerator 组件！");
            configValid = false;
        }
        
        if (experimentController == null)
        {
            Debug.LogError("❌ 缺少 DoubleSlitSimpleController 组件！");
            configValid = false;
        }
        
        if (parameterManager == null)
        {
            Debug.LogWarning("⚠ 缺少 DoubleSlitParameterManager 组件，将使用默认参数验证");
        }
        
        if (formulaCalculator == null)
        {
            Debug.LogWarning("⚠ 缺少 DoubleSlitFormulaCalculator 组件，将使用内置计算");
        }
        
        if (configValid)
        {
            Debug.Log("✅ 实验配置验证通过！");
        }
        else
        {
            Debug.LogError("⚠ 实验配置不完整，请检查场景设置！");
        }
    }

    /// <summary>
    /// 快速启动实验（用于测试）
    /// </summary>
    [ContextMenu("快速启动实验")]
    public void QuickStartExperiment()
    {
        if (experimentController != null)
        {
            experimentController.ResetExperiment();
        }
    }

    /// <summary>
    /// 重置实验状态
    /// </summary>
    [ContextMenu("重置实验")]
    public void ResetExperiment()
    {
        if (experimentController != null)
        {
            experimentController.ResetExperiment();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在编辑器中自动配置场景
    /// </summary>
    [ContextMenu("自动配置场景")]
    public void AutoSetupScene()
    {
        AutoConfigureComponents();
        
        // 设置默认参数
        if (lutGenerator != null)
        {
            lutGenerator.wavelength = 632.8f;
            lutGenerator.slitDistance = 0.1f;
            lutGenerator.screenDistance = 1.0f;
            lutGenerator.slitWidth = 0.05f;
        }
        
        Debug.Log("[DoubleSlit] 简化的场景自动配置完成！");
    }
#endif
}