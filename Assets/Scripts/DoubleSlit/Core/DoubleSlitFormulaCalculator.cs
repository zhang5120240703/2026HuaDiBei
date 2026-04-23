using UnityEngine;

/// <summary>
/// 双缝干涉公式计算器
/// 专门负责物理公式计算和误差分析
/// </summary>
[AddComponentMenu("DoubleSlit/Core/Double Slit Formula Calculator")]
public class DoubleSlitFormulaCalculator : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector 字段
    // ══════════════════════════════════════════════

    [Header("── 误差分析参数 ──")]
    [Tooltip("允许的误差阈值 (%)")]
    [Range(1f, 15f)] public float errorThreshold = 8f;

    // ══════════════════════════════════════════════
    //  运行时状态
    // ══════════════════════════════════════════════

    [Header("── 运行时状态（只读）──")]
    [SerializeField] private float theoreticalDeltaX;   // 理论条纹间距 (mm)
    [SerializeField] private float measuredDeltaX;      // 测量条纹间距 (mm)
    [SerializeField] private float currentError;        // 当前误差 (%)
    [SerializeField] private bool isErrorAcceptable;    // 误差是否可接受

    // ══════════════════════════════════════════════
    //  属性访问器
    // ══════════════════════════════════════════════

    public float TheoreticalDeltaX => theoreticalDeltaX;
    public float MeasuredDeltaX => measuredDeltaX;
    public float CurrentError => currentError;
    public bool IsErrorAcceptable => isErrorAcceptable;

    // ══════════════════════════════════════════════
    //  公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 计算理论条纹间距 Δx = λD/d
    /// </summary>
    public float CalculateTheoreticalDeltaX(float wavelength, float screenDistance, float slitDistance)
    {
        // λ: 波长 (m), D: 屏距 (m), d: 缝距 (m)
        float lambda = wavelength * 1e-9f;           // nm -> m
        float D = screenDistance;                    // m
        float d = slitDistance * 1e-3f;              // mm -> m

        if (d <= 0f) return 0f;

        theoreticalDeltaX = (lambda * D / d) * 1000f; // m -> mm
        return theoreticalDeltaX;
    }

    /// <summary>
    /// 计算测量误差
    /// </summary>
    public ErrorAnalysisResult CalculateError(float measuredDeltaX, float theoreticalDeltaX)
    {
        var result = new ErrorAnalysisResult();
        
        // 保存测量值
        this.measuredDeltaX = measuredDeltaX;
        
        if (theoreticalDeltaX <= 0f || measuredDeltaX <= 0f)
        {
            result.Error = 100f;
            result.IsAcceptable = false;
            result.Message = "无效的测量值或理论值";
            return result;
        }

        // 计算相对误差
        currentError = Mathf.Abs((measuredDeltaX - theoreticalDeltaX) / theoreticalDeltaX) * 100f;
        isErrorAcceptable = currentError <= errorThreshold;

        result.Error = currentError;
        result.IsAcceptable = isErrorAcceptable;
        result.Message = isErrorAcceptable
            ? $"✅ 验证通过！误差 {currentError:F1}% ≤ {errorThreshold}%"
            : $"⚠ 误差较大！{currentError:F1}% > {errorThreshold}%，请检查测量或调整参数";

        return result;
    }

    /// <summary>
    /// 根据测量结果反推参数
    /// </summary>
    public ParameterInferenceResult InferParameterFromMeasurement(float measuredDeltaX, float screenDistance, float slitDistance)
    {
        var result = new ParameterInferenceResult();
        
        if (measuredDeltaX <= 0f || screenDistance <= 0f || slitDistance <= 0f)
        {
            result.IsValid = false;
            result.Message = "无效的输入参数";
            return result;
        }

        // 反推波长：λ = Δx * d / D
        float d = slitDistance * 1e-3f;              // mm -> m
        float inferredWavelength = (measuredDeltaX * 1e-3f * d / screenDistance) * 1e9f; // m -> nm

        result.IsValid = true;
        result.InferredWavelength = inferredWavelength;
        result.Message = $"根据测量值推断波长约为 {inferredWavelength:F0}nm";

        return result;
    }

    /// <summary>
    /// 计算干涉条纹的可见性
    /// </summary>
    public VisibilityResult CalculateVisibility(float wavelength, float slitDistance, float screenDistance)
    {
        var result = new VisibilityResult();
        
        // 计算条纹间距
        float deltaX = CalculateTheoreticalDeltaX(wavelength, screenDistance, slitDistance);
        
        // 估算可见条纹数量（基于屏幕尺寸和条纹间距）
        float screenWidth = 0.1f; // 假设屏幕宽度为10cm
        int visibleFringes = Mathf.FloorToInt(screenWidth / (deltaX * 0.001f)); // mm -> m
        
        result.DeltaX = deltaX;
        result.VisibleFringes = Mathf.Max(1, visibleFringes);
        result.VisibilityRating = visibleFringes >= 5 ? VisibilityRating.Excellent :
                                 visibleFringes >= 3 ? VisibilityRating.Good :
                                 visibleFringes >= 1 ? VisibilityRating.Fair : VisibilityRating.Poor;
        
        return result;
    }

    // ══════════════════════════════════════════════
    //  辅助方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 格式化物理量显示
    /// </summary>
    public string FormatPhysicalValue(float value, string unit, int decimals = 3)
    {
        return string.Format("{0:F" + decimals + "} {1}", value, unit);
    }
}

/// <summary>
/// 误差分析结果
/// </summary>
public struct ErrorAnalysisResult
{
    public float Error;           // 误差百分比
    public bool IsAcceptable;     // 是否可接受
    public string Message;        // 结果消息
}

/// <summary>
/// 参数反推结果
/// </summary>
public struct ParameterInferenceResult
{
    public bool IsValid;          // 是否有效
    public float InferredWavelength; // 推断的波长 (nm)
    public string Message;        // 结果消息
}

/// <summary>
/// 可见性分析结果
/// </summary>
public struct VisibilityResult
{
    public float DeltaX;          // 条纹间距 (mm)
    public int VisibleFringes;    // 可见条纹数量
    public VisibilityRating VisibilityRating; // 可见性评级
}

/// <summary>
/// 可见性评级
/// </summary>
public enum VisibilityRating
{
    Poor,      // 差（1条条纹）
    Fair,      // 一般（2-3条条纹）
    Good,      // 良好（4-5条条纹）
    Excellent  // 优秀（5条以上条纹）
}