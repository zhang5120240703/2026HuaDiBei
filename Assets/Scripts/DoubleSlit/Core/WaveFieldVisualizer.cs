using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class WaveFieldVisualizer : MonoBehaviour
{
    [Header("参数来源")]
    public DoubleSlitLUTGenerator lutGenerator;

    [Header("可视化参数")]
    [Tooltip("可见波数")]
    [Range(3, 40)] public int targetVisibleWaves = 10;
    [Range(0.1f, 5f)] public float animationSpeed = 1f;
    [Range(0.1f, 5f)] public float brightness = 2f;
    public bool phaseWaveMode = false;

    [Header("可视化区域")]
    [Range(0.02f, 0.48f)] public float slitSepToPlaneHeightRatio = 0.12f;

    [Header("自动适配双缝到光屏")]
    public bool autoFitDistance = true;
    public Transform doubleSlitTransform;
    public Transform screenTransform;
    [Tooltip("自动适配后再乘这个系数来微调长度，>1 变长，<1 变短")]
    [Range(0.1f, 5f)] public float manualScale = 1f;

    static readonly int P_S1Y = Shader.PropertyToID("_Slit1Y");
    static readonly int P_S2Y = Shader.PropertyToID("_Slit2Y");
    static readonly int P_K = Shader.PropertyToID("_VisualK");
    static readonly int P_Asp = Shader.PropertyToID("_Aspect");
    static readonly int P_Spd = Shader.PropertyToID("_TimeScale");
    static readonly int P_Brt = Shader.PropertyToID("_Brightness");
    static readonly int P_Col = Shader.PropertyToID("_WaveColor");
    static readonly int P_Ph = Shader.PropertyToID("_ShowPhase");

    private Material _mat;
    private ExperimentBenchManager _bench;

    void OnEnable()
    {
        _mat = GetComponent<Renderer>().material;
        AutoFindTransforms();
        SyncShader();
    }

    void Start()
    {
        if (_bench == null) AutoFindTransforms();
    }

    void Update() => SyncShader();
    void OnValidate() => SyncShader();

    private void AutoFindTransforms()
    {
        if (lutGenerator == null)
            lutGenerator = FindObjectOfType<DoubleSlitLUTGenerator>();

        _bench = FindObjectOfType<ExperimentBenchManager>();

        if (doubleSlitTransform == null && _bench != null && _bench.doubleSlit != null)
            doubleSlitTransform = _bench.doubleSlit.transform;

        if (screenTransform == null && _bench != null && _bench.screen != null)
            screenTransform = _bench.screen.transform;
    }

    void SyncShader()
    {
        if (_mat == null)
        {
            _mat = GetComponent<Renderer>().material;
            if (_mat == null) return;
        }
        if (lutGenerator == null) return;

        if (autoFitDistance && doubleSlitTransform != null && screenTransform != null)
        {
            Vector3 slitPos = doubleSlitTransform.position;
            Vector3 screenPos = screenTransform.position;
            Vector3 mid = (slitPos + screenPos) * 0.5f;
            float dist = Vector3.Distance(slitPos, screenPos);

            if (dist > 0.001f)
            {
                float targetX = dist * manualScale;
                Transform parent = transform.parent;
                if (parent != null)
                {
                    transform.localPosition = parent.InverseTransformPoint(mid);
                    Vector3 ls = transform.localScale;
                    ls.x = targetX / parent.lossyScale.x;
                    transform.localScale = ls;
                }
                else
                {
                    transform.position = mid;
                    Vector3 ls = transform.localScale;
                    ls.x = targetX;
                    transform.localScale = ls;
                }
            }
        }

        Vector3 s = transform.localScale;
        float aspect = s.x / Mathf.Max(s.y, 0.0001f);

        float halfSep = Mathf.Clamp(slitSepToPlaneHeightRatio, 0.02f, 0.48f) * 0.5f;
        _mat.SetFloat(P_S1Y, 0.5f + halfSep);
        _mat.SetFloat(P_S2Y, 0.5f - halfSep);

        _mat.SetFloat(P_K, (2f * Mathf.PI * targetVisibleWaves) / aspect);
        _mat.SetFloat(P_Asp, aspect);

        _mat.SetFloat(P_Spd, animationSpeed);
        _mat.SetFloat(P_Brt, brightness);
        _mat.SetFloat(P_Ph, phaseWaveMode ? 1f : 0f);

        Color col = lutGenerator.isWhiteLight ? Color.white : WlToColor(lutGenerator.wavelength);
        _mat.SetColor(P_Col, col);
    }

    static Color WlToColor(float wl)
    {
        return DoubleSlitLUTGenerator.WavelengthToColor(wl);
    }
}
