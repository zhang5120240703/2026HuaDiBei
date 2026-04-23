using UnityEngine;

/// <summary>
/// 简化的双缝实验参数管理器
/// 只负责基本的参数验证和应用
/// </summary>
[AddComponentMenu("DoubleSlit/Core/Double Slit Parameter Manager")]
public class DoubleSlitParameterManager : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector 字段
    // ══════════════════════════════════════════════

    [Header("── 实验参数范围 ──")]
    [Tooltip("波长范围 (nm)")]
    public Vector2 wavelengthRange = new Vector2(400f, 700f);
    [Tooltip("缝间距范围 (mm)")]
    public Vector2 slitDistanceRange = new Vector2(0.05f, 0.5f);
    [Tooltip("屏距范围 (m)")]
    public Vector2 screenDistanceRange = new Vector2(0.5f, 3f);

    // ══════════════════════════════════════════════
    //  运行时状态
    // ══════════════════════════════════════════════

    [Header("── 运行时状态（只读）──")]
    [SerializeField] private bool parametersValid;
    [SerializeField] private float currentWavelength;
    [SerializeField] private float currentSlitDistance;
    [SerializeField] private float currentScreenDistance;

    // ══════════════════════════════════════════════
    //  属性访问器
    // ══════════════════════════════════════════════

    public bool ParametersValid => parametersValid;
    public float CurrentWavelength => currentWavelength;
    public float CurrentSlitDistance => currentSlitDistance;
    public float CurrentScreenDistance => currentScreenDistance;

    // ══════════════════════════════════════════════
    //  公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 简化参数验证 - 只检查基本范围
    /// </summary>
    public bool ValidateParameters(float wavelength, float slitDistance, float screenDistance)
    {
        // 保存当前参数值
        currentWavelength = wavelength;
        currentSlitDistance = slitDistance;
        currentScreenDistance = screenDistance;
        
        // 基本范围检查
        bool wavelengthOk = wavelength >= wavelengthRange.x && wavelength <= wavelengthRange.y;
        bool slitDistanceOk = slitDistance >= slitDistanceRange.x && slitDistance <= slitDistanceRange.y;
        bool screenDistanceOk = screenDistance >= screenDistanceRange.x && screenDistance <= screenDistanceRange.y;
        
        parametersValid = wavelengthOk && slitDistanceOk && screenDistanceOk;
        
        return parametersValid;
    }

    /// <summary>
    /// 应用参数到LUT生成器
    /// </summary>
    public void ApplyParametersToLUT(DoubleSlitLUTGenerator lutGenerator, float wavelength, float slitDistance, float screenDistance)
    {
        if (lutGenerator == null) return;
        
        lutGenerator.wavelength = Mathf.Clamp(wavelength, wavelengthRange.x, wavelengthRange.y);
        lutGenerator.slitDistance = Mathf.Clamp(slitDistance, slitDistanceRange.x, slitDistanceRange.y);
        lutGenerator.screenDistance = Mathf.Clamp(screenDistance, screenDistanceRange.x, screenDistanceRange.y);
        lutGenerator.slitWidth = 0.05f; // 固定缝宽
    }

    // ══════════════════════════════════════════════
    //  Editor 调试
    // ══════════════════════════════════════════════

#if UNITY_EDITOR
    void OnValidate()
    {
        // 确保参数范围合理
        wavelengthRange.x = Mathf.Max(380f, wavelengthRange.x);
        wavelengthRange.y = Mathf.Min(780f, wavelengthRange.y);
        slitDistanceRange.x = Mathf.Max(0.01f, slitDistanceRange.x);
        screenDistanceRange.x = Mathf.Max(0.1f, screenDistanceRange.x);
    }
#endif
}