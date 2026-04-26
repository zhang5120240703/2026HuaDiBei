using UnityEngine;

/// <summary>
/// 方案二：波场动态可视化控制器
/// 挂在双缝和光屏之间的 Quad 上，材质使用 WaveField2D Shader
/// 自动读取 DoubleSlitLUTGenerator 的物理参数并同步至 Shader
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class WaveFieldVisualizer : MonoBehaviour
{
    [Header("物理参数来源")]
    public DoubleSlitLUTGenerator lutGenerator;

    [Header("可视化设置")]
    [Tooltip("沿传播方向可见的波纹数量（值越大波纹越密）")]
    [Range(3, 40)] public int targetVisibleWaves = 10;
    [Range(0.1f, 5f)] public float animationSpeed = 1f;
    [Range(0.1f, 5f)] public float brightness = 2f;
    [Tooltip("false = 强度干涉纹（平静）  true = 瞬态波形动画（动感）")]
    public bool phaseWaveMode = false;

    [Header("缝距映射")]
    [Tooltip("缝间距在光场平面高度中的比例，调节使缝位置与挡板对齐")]
    [Range(0.02f, 0.48f)] public float slitSepToPlaneHeightRatio = 0.12f;

    // Shader 属性 ID（缓存避免字符串查找）
    static readonly int P_S1Y = Shader.PropertyToID("_Slit1Y");
    static readonly int P_S2Y = Shader.PropertyToID("_Slit2Y");
    static readonly int P_K = Shader.PropertyToID("_VisualK");
    static readonly int P_Asp = Shader.PropertyToID("_Aspect");
    static readonly int P_Spd = Shader.PropertyToID("_TimeScale");
    static readonly int P_Brt = Shader.PropertyToID("_Brightness");
    static readonly int P_Col = Shader.PropertyToID("_WaveColor");
    static readonly int P_Ph = Shader.PropertyToID("_ShowPhase");

    private Material _mat;

    void OnEnable()
    {
        _mat = GetComponent<Renderer>().material;
        SyncShader();
    }

    void Update() => SyncShader();
    void OnValidate() => SyncShader();

    void SyncShader()
    {
        if (_mat == null || lutGenerator == null) return;

        // 宽高比（Quad 的 localScale.x / localScale.y）
        Vector3 s = transform.localScale;
        float aspect = s.x / Mathf.Max(s.y, 0.0001f);

        // 缝的 UV.y 位置（以中心 0.5 为基准上下偏移）
        float halfSep = Mathf.Clamp(slitSepToPlaneHeightRatio, 0.02f, 0.48f) * 0.5f;
        _mat.SetFloat(P_S1Y, 0.5f + halfSep);
        _mat.SetFloat(P_S2Y, 0.5f - halfSep);

        // 视觉波数：在宽高比修正后的 UV 空间中有 targetVisibleWaves 个波长
        // k = 2π * N / aspect（N 条波纹跨越整个宽度）
        _mat.SetFloat(P_K, (2f * Mathf.PI * targetVisibleWaves) / aspect);
        _mat.SetFloat(P_Asp, aspect);

        _mat.SetFloat(P_Spd, animationSpeed);
        _mat.SetFloat(P_Brt, brightness);
        _mat.SetFloat(P_Ph, phaseWaveMode ? 1f : 0f);

        // 波的颜色始终从波长自动获取，保持与光的颜色一致
        Color col = lutGenerator.isWhiteLight ? Color.white : WlToColor(lutGenerator.wavelength);
        _mat.SetColor(P_Col, col);
    }

    static Color WlToColor(float wl)
    {
        // 使用 DoubleSlitLUTGenerator 的波长转颜色方法，保证颜色一致性
        return DoubleSlitLUTGenerator.WavelengthToColor(wl);
    }
}