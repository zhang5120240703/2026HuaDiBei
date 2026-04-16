using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 方案一：几何射线描迹
/// 挂在场景任意空对象上，指定光源/挡板/光屏三个 Transform 即可
/// </summary>
[ExecuteInEditMode]
public class DoubleSlitRayVisualizer : MonoBehaviour
{
    [Header("场景引用")]
    public DoubleSlitLUTGenerator lutGenerator;
    public Transform lightSource;     // 光源对象
    public Transform slitBarrier;     // 双缝挡板（缝在 up 方向上下偏移）
    public Transform screenPlane;     // 光屏对象

    [Header("视觉参数")]
    [Tooltip("两缝在场景中的视觉间距（Unity 单位，对应 slitBarrier.up 方向）")]
    public float visualSlitSeparation = 0.2f;
    [Tooltip("光屏可视范围的半高（Unity 单位），应与光屏 Mesh 尺寸对应")]
    public float visualScreenHalfHeight = 0.5f;

    [Header("射线设置")]
    [Range(8, 128)] public int raysPerSlit = 48;
    [Range(0.001f, 0.03f)] public float maxRayWidth = 0.006f;
    [Range(0f, 0.5f)] public float minAlpha = 0.02f;
    [Tooltip("推荐使用 Particles/Additive（加法混合，适合发光效果）")]
    public Material lineMaterial;
    [Tooltip("是否显示光源→缝的入射线（禁用后只显示衍射线）")]
    public bool drawIncidentRays = false;   // ★ 改为 false，防止光线提前分束
    
    [Tooltip("波长显示模式：Manual=手动调整，Auto=根据wavelength自动")]
    public enum ColorMode { Auto, Manual }
    public ColorMode rayColorMode = ColorMode.Auto;
    [Tooltip("手动调整时的光线颜色")]
    public Color manualRayColor = Color.white;
    [Range(0f, 1f)]
    [Tooltip("色彩饱和度（1.0=完整物理色，<1.0=更白/淡）")]
    public float colorSaturation = 1.0f;

    // ── 内部状态 ───────────────────────────────────────────────
    private readonly List<LineRenderer> _rays = new List<LineRenderer>();
    private readonly Stack<LineRenderer> _rayPool = new Stack<LineRenderer>();
    private float _cWl, _cD, _cA, _cL;
    private bool _cWl2;

    public bool usePhysicalSeparation = true; // 如果 true，将 visualSlitSeparation 同步到 lutGenerator.slitDistance（mm->m）
    [Range(0.1f, 10f)] public float intensityScale = 1.0f; // 强度放大器
    private float lastParamChangeTime;
    private bool paramsDirty = false;
    public float rebuildDebounce = 0.25f; // 停止拖动 0.25s 后重建

    void OnEnable() => RebuildRays();
    void OnDisable() => DestroyRays();
    void OnValidate() { if (!Application.isPlaying) RebuildRays(); }

    void Update()
    {
        if (lutGenerator == null) return;

        bool changed = !Mathf.Approximately(lutGenerator.wavelength, _cWl) ||
                       !Mathf.Approximately(lutGenerator.slitDistance, _cD) ||
                       !Mathf.Approximately(lutGenerator.slitWidth, _cA) ||
                       !Mathf.Approximately(lutGenerator.screenDistance, _cL) ||
                       lutGenerator.isWhiteLight != _cWl2;

        if (changed)
        {
            paramsDirty = true;
            lastParamChangeTime = Time.time;
            CacheParams();
        }

        if (paramsDirty && (Time.time - lastParamChangeTime) >= rebuildDebounce)
        {
            RebuildRays();
            paramsDirty = false;
        }
    }

    void RebuildRays()
    {
        DestroyRays();
        if (!ValidateRefs()) return;

        CacheParams();

        Vector3 up = slitBarrier.up;
        Vector3 bCenter = slitBarrier.position;
        Vector3 slit1 = bCenter + up * (visualSlitSeparation * 0.5f);
        Vector3 slit2 = bCenter - up * (visualSlitSeparation * 0.5f);

        // 光源 → 两缝（粗明亮入射线）
        if (drawIncidentRays)
        {
            SpawnRay(lightSource.position, slit1, BaseColor(1f), maxRayWidth * 2.5f, maxRayWidth * 1.8f);
            SpawnRay(lightSource.position, slit2, BaseColor(1f), maxRayWidth * 2.5f, maxRayWidth * 1.8f);
        }

        // 双缝 → 光屏（扇形衍射线）
        for (int i = 0; i < raysPerSlit; i++)
        {
            float t = (float)i / (raysPerSlit - 1);
            float screenY = (t - 0.5f) * 2f * visualScreenHalfHeight;
            Vector3 target = screenPlane.position + screenPlane.up * screenY;

            // 将 screenY 映射到物理坐标（maxRange = 50mm）
            float physY = screenY / visualScreenHalfHeight * 0.05f;
            float intensity = CalcIntensity(physY);
            if (intensity < 0.004f) continue;

            float alpha = Mathf.Lerp(minAlpha, 1f, Mathf.Sqrt(intensity));
            float width = maxRayWidth * Mathf.Lerp(0.05f, 1f, intensity);

            SpawnRay(slit1, target, BaseColor(alpha), width, width * 0.15f);
            SpawnRay(slit2, target, BaseColor(alpha), width, width * 0.15f);
        }
    }

    LineRenderer GetPooledRay()
    {
        if (_rayPool.Count > 0) return _rayPool.Pop();
        var go = new GameObject("Ray");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.numCapVertices = 2;
        if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        return lr;
    }

    void ReturnRayToPool(LineRenderer lr)
    {
        lr.positionCount = 0;
        lr.gameObject.SetActive(false);
        _rayPool.Push(lr);
    }

    void SpawnRay(Vector3 from, Vector3 to, Color color, float startW, float endW)
    {
        var lr = GetPooledRay();
        lr.gameObject.SetActive(true);

        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = startW;
        lr.endWidth = endW;

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        lr.colorGradient = grad;

        _rays.Add(lr);
    }

    float CalcIntensity(float screenY)
    {
        float lambda = lutGenerator.wavelength * 1e-9f;
        float d = lutGenerator.slitDistance * 1e-3f;
        float a = lutGenerator.slitWidth * 1e-3f;
        float L = lutGenerator.screenDistance;

        float phase = Mathf.PI * screenY / (lambda * L);
        float Ii = Mathf.Pow(Mathf.Cos(d * phase), 2f);

        float arg = a * phase;
        float sinc = Mathf.Abs(arg) < 1e-6f ? 1f : Mathf.Sin(arg) / arg;
        return Ii * sinc * sinc * intensityScale;
    }

    Color BaseColor(float alpha)
    {
        if (rayColorMode == ColorMode.Manual)
            return new Color(manualRayColor.r, manualRayColor.g, manualRayColor.b, alpha);

        if (lutGenerator.isWhiteLight) 
            return new Color(1f, 1f, 1f, alpha);

        float wl = lutGenerator.wavelength;
        float r = 0f, g = 0f, b = 0f;

        // ★ 物理波长到RGB的准确映射
        if (wl >= 620)
        {
            r = 1f;
            g = (750f - wl) / 130f;
            b = 0f;
        }
        else if (wl >= 580)
        {
            r = 1f;
            g = 1f;
            b = 0f;
        }
        else if (wl >= 495)
        {
            r = (wl - 495f) / 85f;
            g = 1f;
            b = 0f;
        }
        else if (wl >= 480)
        {
            r = 0f;
            g = 1f;
            b = (495f - wl) / 15f;
        }
        else
        {
            r = 0f;
            g = (wl - 380f) / 100f;
            b = 1f;
        }

        // ★ 应用饱和度控制
        r = Mathf.Lerp(0.5f, r, colorSaturation);
        g = Mathf.Lerp(0.5f, g, colorSaturation);
        b = Mathf.Lerp(0.5f, b, colorSaturation);

        return new Color(r, g, b, alpha);
    }

    void CacheParams()
    {
        _cWl = lutGenerator.wavelength;
        _cD = lutGenerator.slitDistance;
        _cA = lutGenerator.slitWidth;
        _cL = lutGenerator.screenDistance;
        _cWl2 = lutGenerator.isWhiteLight;
    }

    bool ValidateRefs() =>
        lutGenerator != null && lightSource != null &&
        slitBarrier != null && screenPlane != null;

    void DestroyRays()
    {
        foreach (var lr in _rays)
            if (lr != null)
            {
                if (Application.isPlaying) lr.gameObject.SetActive(false);
                else DestroyImmediate(lr.gameObject);
            }
        _rays.Clear();
    }
}