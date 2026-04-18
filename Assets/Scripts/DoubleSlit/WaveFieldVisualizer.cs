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
    public bool autoColorFromWavelength = true;
    public Color manualColor = new Color(0.8f, 1f, 0.3f, 1f);

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

        Color col = autoColorFromWavelength
            ? (lutGenerator.isWhiteLight ? Color.white : WlToColor(lutGenerator.wavelength))
            : manualColor;
        _mat.SetColor(P_Col, col);
    }

    static Color WlToColor(float wl)
    {
        float r, g, b;
        
        // ★ 标准可见光谱映射（380nm-780nm）
        if (wl >= 380f && wl < 440f)
        {
            // 紫色（380-440nm）
            r = -(wl - 440f) / 60f;
            g = 0f;
            b = 1f;
        }
        else if (wl >= 440f && wl < 490f)
        {
            // 蓝色（440-490nm）
            r = 0f;
            g = (wl - 440f) / 50f;
            b = 1f;
        }
        else if (wl >= 490f && wl < 510f)
        {
            // 青色（490-510nm）
            r = 0f;
            g = 1f;
            b = -(wl - 510f) / 20f;
        }
        else if (wl >= 510f && wl < 580f)
        {
            // 绿色（510-580nm）
            r = (wl - 510f) / 70f;
            g = 1f;
            b = 0f;
        }
        else if (wl >= 580f && wl < 645f)
        {
            // 黄色-橙色（580-645nm）
            r = 1f;
            g = -(wl - 645f) / 65f;
            b = 0f;
        }
        else if (wl >= 645f && wl <= 780f)
        {
            // 红色（645-780nm）
            r = 1f;
            g = 0f;
            b = 0f;
        }
        else
        {
            // 超出范围
            r = 0f;
            g = 0f;
            b = 0f;
        }

        // ★ 光谱边缘强度衰减（模拟人眼感知）
        float factor = (wl >= 380f && wl < 420f)
            ? 0.3f + 0.7f * (wl - 380f) / 40f
            : (wl >= 700f && wl <= 780f)
            ? 0.3f + 0.7f * (780f - wl) / 80f
            : 1f;

        return new Color(
            Mathf.Clamp01(r * factor),
            Mathf.Clamp01(g * factor),
            Mathf.Clamp01(b * factor),
            1f);
    }
}