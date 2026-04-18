using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 双缝干涉 LUT 生成器
/// ─ 功能：
///   1. 运行时 / Editor 生成干涉图样 LUT，写入材质 _LUT
///   2. 将光源颜色（_LightColor / _IsWhiteLight）同步到材质，驱动 Quad 着色
///   3. 用 LineRenderer 可视化光路：
///      光源 ──宽散束──▶ 单缝 ──凝实束──▶ 双缝
/// </summary>
[ExecuteInEditMode]
public class DoubleSlitLUTGenerator : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    //  Inspector 字段
    // ═══════════════════════════════════════════════════════

    [Header("材质")]
    public Material interferenceMaterial;
    [Range(64, 2048)] public int lutWidth = 1024;

    [Header("物理参数")]
    [Range(380, 780)] public float wavelength = 550f;   // nm
    [Range(0.01f, 1f)] public float slitDistance = 0.2f;   // mm，双缝间距 d
    [Range(0.001f, 0.5f)] public float slitWidth = 0.05f;  // mm，单缝宽度 a
    [Range(0.1f, 10f)] public float screenDistance = 1.0f;   // m，缝到屏距离 L
    /// <summary>屏幕物理半宽（mm）。越小则可见条纹越稀，建议 10~30</summary>
    [Range(5f, 100f)] public float screenHalfWidthMm = 20f;    // ★ 新增：控制条纹密度
    public bool isWhiteLight = false;

    [Header("光路可视化")]
    [Tooltip("光源 Transform（可为空，为空则禁用光路渲染）")]
    public Transform lightSourceTf;
    [Tooltip("单缝 Transform")]
    public Transform singleSlitTf;
    [Tooltip("双缝 Transform")]
    public Transform doubleSlitTf;
    [Tooltip("光束使用的材质（建议 Particles/Additive）")]
    public Material beamMaterial;
    [Range(0.005f, 0.2f)] public float sourceBeamWidth = 0.05f;
    [Range(0.005f, 0.1f)] public float slitBeamWidth = 0.02f;
    [Range(0f, 1f)] public float beamAlpha = 0.75f;

    [Header("运行时更新")]
    public bool enableRuntimeUpdate = true;
    [Range(0.1f, 3f)] public float updateInterval = 0.5f;

    // ═══════════════════════════════════════════════════════
    //  私有字段
    // ═══════════════════════════════════════════════════════

    private Texture2D lutTexture;
    private Color32[] pixelBuffer;

    // 光束 LineRenderer
    private LineRenderer sourceBeamLR;
    private LineRenderer slitBeamLR;

    // ★ 零 GC 的预分配渐变数组
    private static readonly GradientColorKey[] s_monoColorKeys = new GradientColorKey[2];
    private static readonly GradientAlphaKey[] s_monoAlphaKeys = new GradientAlphaKey[2];
    private static readonly GradientColorKey[] s_rainbowColorKeys = new GradientColorKey[5];
    private static readonly GradientAlphaKey[] s_rainbowAlphaKeys = new GradientAlphaKey[2];
    private static readonly Gradient s_sharedGrad = new Gradient();

    // 变化检测
    private float prevWavelength, prevSlitDistance, prevSlitWidth, prevScreenDistance, prevScreenHalfWidth;
    private bool prevIsWhiteLight;
    private float watchWavelength, watchSlitDistance, watchSlitWidth, watchScreenDistance, watchScreenHalfWidth;
    private bool watchIsWhiteLight;

    private const string LUT_ASSET_PATH = "Assets/Art/Texture/DoubleSlitLUT.png";

    // ═══════════════════════════════════════════════════════
    //  生命周期
    // ═══════════════════════════════════════════════════════

    void Start()
    {
        if (!Application.isPlaying) return;

        InitBeamRenderers();
        SyncWatchValues();
        RegenerateAll();

        // ★ 用协程替代 Update()，彻底消除每帧轮询开销
        if (enableRuntimeUpdate)
            StartCoroutine(RuntimeUpdateCoroutine());

        Debug.Log("[DoubleSlit] 运行模式初始化完成");
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= OnValidateDelayed;
            EditorApplication.delayCall += OnValidateDelayed;
        }
#endif
    }

    // ★ Update() 已删除，改用协程，笔记本风扇不会因此空转
    // void Update() { ... }

    void OnDestroy()
    {
        if (sourceBeamLR) DestroyImmediate(sourceBeamLR.gameObject);
        if (slitBeamLR) DestroyImmediate(slitBeamLR.gameObject);
    }

    // ═══════════════════════════════════════════════════════
    //  运行时协程（代替 Update，仅按 updateInterval 间隔唤醒）
    // ═══════════════════════════════════════════════════════

    private IEnumerator RuntimeUpdateCoroutine()
    {
        // 预分配 WaitForSeconds，避免每次协程恢复时 GC
        var wait = new WaitForSeconds(updateInterval);
        while (true)
        {
            yield return wait;
            if (HasParamsChanged())
            {
                SyncWatchValues();
                RegenerateAll();
            }
        }
    }

    private bool HasParamsChanged()
    {
        return wavelength != watchWavelength
            || slitDistance != watchSlitDistance
            || slitWidth != watchSlitWidth
            || screenDistance != watchScreenDistance
            || screenHalfWidthMm != watchScreenHalfWidth
            || isWhiteLight != watchIsWhiteLight;
    }

    // ═══════════════════════════════════════════════════════
    //  主入口
    // ═══════════════════════════════════════════════════════

    private void RegenerateAll()
    {
        GenerateLUT();
        UpdateMaterialColor();
        UpdateBeamVisuals();
    }

    // ═══════════════════════════════════════════════════════
    //  LUT 生成
    // ═══════════════════════════════════════════════════════

    private void GenerateLUT()
    {
        if (interferenceMaterial == null) return;

        if (lutTexture == null || lutTexture.width != lutWidth)
        {
            if (lutTexture != null) DestroyImmediate(lutTexture);
            lutTexture = new Texture2D(lutWidth, 1, TextureFormat.RGBA32, false, true);
            lutTexture.wrapMode = TextureWrapMode.Clamp;
            lutTexture.filterMode = FilterMode.Bilinear;
            pixelBuffer = new Color32[lutWidth];
        }

        float[] wavelengths = isWhiteLight
            ? new float[] { 680f, 630f, 580f, 530f, 490f, 450f, 410f }
            : new float[] { wavelength };
        float normFactor = isWhiteLight ? wavelengths.Length : 1f;

        float d = slitDistance * 1e-3f;
        float a = slitWidth * 1e-3f;
        float L = screenDistance;
        float xMax = screenHalfWidthMm * 1e-3f;   // ★ 由 Inspector 控制

        Color[] wlColors = new Color[wavelengths.Length];
        for (int w = 0; w < wavelengths.Length; w++)
            wlColors[w] = WavelengthToColor(wavelengths[w]);

        for (int x = 0; x < lutWidth; x++)
        {
            float u = x / (float)(lutWidth - 1);
            float physX = (u - 0.5f) * 2f * xMax;

            Color acc = Color.black;
            for (int w = 0; w < wavelengths.Length; w++)
            {
                float lambda = wavelengths[w] * 1e-9f;
                float phaseFactor = Mathf.PI * physX / (lambda * L);

                float phaseI = d * phaseFactor;
                float intensity_i = Mathf.Cos(phaseI) * Mathf.Cos(phaseI);

                float phaseD = a * phaseFactor;
                float sinc = Mathf.Abs(phaseD) < 1e-6f
                    ? 1f : Mathf.Sin(phaseD) / phaseD;
                float intensity_d = sinc * sinc;

                // ★ gamma=1.0：线性强度，暗纹 = 真黑（配合 Opaque shader）
                acc += wlColors[w] * (intensity_i * intensity_d);
            }

            pixelBuffer[x] = acc / normFactor;
        }

        lutTexture.SetPixels32(pixelBuffer);
        lutTexture.Apply(false);
        interferenceMaterial.SetTexture("_LUT", lutTexture);

        prevWavelength = wavelength;
        prevSlitDistance = slitDistance;
        prevSlitWidth = slitWidth;
        prevScreenDistance = screenDistance;
        prevScreenHalfWidth = screenHalfWidthMm;
        prevIsWhiteLight = isWhiteLight;

        if (!Application.isPlaying)
            SaveLUTAsset();
    }

    // ═══════════════════════════════════════════════════════
    //  材质颜色同步
    // ═══════════════════════════════════════════════════════

    private void UpdateMaterialColor()
    {
        if (interferenceMaterial == null) return;
        // ★ 颜色已编码在 LUT 中，无需额外的材质参数
        // Shader 直接从 LUT 采样得到正确的波长色（或白光光谱色）
    }

    // ═══════════════════════════════════════════════════════
    //  光束可视化
    // ═══════════════════════════════════════════════════════

    private LineRenderer GetOrCreateLR(ref LineRenderer lr, string goName)
    {
        if (lr != null) return lr;
        var go = new GameObject(goName) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(transform, false);
        lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 2;
        return lr;
    }

    private void InitBeamRenderers()
    {
        GetOrCreateLR(ref sourceBeamLR, "_SourceBeam");
        GetOrCreateLR(ref slitBeamLR, "_SlitBeam");
        if (beamMaterial != null)
        {
            sourceBeamLR.material = beamMaterial;
            slitBeamLR.material = beamMaterial;
        }
    }

    private void UpdateBeamVisuals()
    {
        bool hasSource = lightSourceTf != null && singleSlitTf != null;
        bool hasSlit = singleSlitTf != null && doubleSlitTf != null;

        if (hasSource && sourceBeamLR == null) GetOrCreateLR(ref sourceBeamLR, "_SourceBeam");
        if (hasSlit && slitBeamLR == null) GetOrCreateLR(ref slitBeamLR, "_SlitBeam");

        Color lightCol = isWhiteLight ? Color.white : WavelengthToColor(wavelength);

        if (hasSource && sourceBeamLR != null)
        {
            if (beamMaterial != null) sourceBeamLR.material = beamMaterial;
            sourceBeamLR.SetPosition(0, lightSourceTf.position);
            sourceBeamLR.SetPosition(1, singleSlitTf.position);
            sourceBeamLR.startWidth = sourceBeamWidth;
            sourceBeamLR.endWidth = sourceBeamWidth * 0.4f;
            // ★ 零 GC：复用静态数组写入渐变
            ApplyGradient(sourceBeamLR, lightCol, beamAlpha * 0.55f, beamAlpha, isWhiteLight);
            sourceBeamLR.enabled = true;
        }
        else if (sourceBeamLR != null) sourceBeamLR.enabled = false;

        if (hasSlit && slitBeamLR != null)
        {
            if (beamMaterial != null) slitBeamLR.material = beamMaterial;
            slitBeamLR.SetPosition(0, singleSlitTf.position);
            slitBeamLR.SetPosition(1, doubleSlitTf.position);
            slitBeamLR.startWidth = slitBeamWidth;
            slitBeamLR.endWidth = slitBeamWidth;
            ApplyGradient(slitBeamLR, lightCol, beamAlpha, beamAlpha, isWhiteLight);
            slitBeamLR.enabled = true;
        }
        else if (slitBeamLR != null) slitBeamLR.enabled = false;
    }

    /// <summary>复用预分配数组设置 LineRenderer 渐变，零堆内存分配</summary>
    private static void ApplyGradient(LineRenderer lr, Color col,
                                      float alphaStart, float alphaEnd, bool rainbow)
    {
        if (rainbow)
        {
            s_rainbowColorKeys[0] = new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0.00f);
            s_rainbowColorKeys[1] = new GradientColorKey(new Color(1f, 0.85f, 0.0f), 0.25f);
            s_rainbowColorKeys[2] = new GradientColorKey(new Color(0.1f, 1f, 0.1f), 0.50f);
            s_rainbowColorKeys[3] = new GradientColorKey(new Color(0.0f, 0.7f, 1f), 0.75f);
            s_rainbowColorKeys[4] = new GradientColorKey(new Color(0.4f, 0.0f, 1f), 1.00f);
            s_rainbowAlphaKeys[0] = new GradientAlphaKey(alphaStart, 0f);
            s_rainbowAlphaKeys[1] = new GradientAlphaKey(alphaEnd, 1f);
            s_sharedGrad.SetKeys(s_rainbowColorKeys, s_rainbowAlphaKeys);
        }
        else
        {
            s_monoColorKeys[0] = new GradientColorKey(col, 0f);
            s_monoColorKeys[1] = new GradientColorKey(col, 1f);
            s_monoAlphaKeys[0] = new GradientAlphaKey(alphaStart, 0f);
            s_monoAlphaKeys[1] = new GradientAlphaKey(alphaEnd, 1f);
            s_sharedGrad.SetKeys(s_monoColorKeys, s_monoAlphaKeys);
        }
        lr.colorGradient = s_sharedGrad;
    }

    // ═══════════════════════════════════════════════════════
    //  波长 → RGB（标准分段线性插值，与 WaveFieldVisualizer 统一）
    // ═══════════════════════════════════════════════════════

    public static Color WavelengthToColor(float wl)
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

    // ═══════════════════════════════════════════════════════
    //  Editor 专用
    // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidateDelayed()
    {
        EditorApplication.delayCall -= OnValidateDelayed;
        if (this == null) return;

        bool changed = wavelength != prevWavelength
                    || slitDistance != prevSlitDistance
                    || slitWidth != prevSlitWidth
                    || screenDistance != prevScreenDistance
                    || screenHalfWidthMm != prevScreenHalfWidth
                    || isWhiteLight != prevIsWhiteLight;

        if (changed)
        {
            RegenerateAll();
            Debug.Log("[DoubleSlit] Editor 参数已更新");
        }
    }
#endif

    private void SyncWatchValues()
    {
        watchWavelength = wavelength;
        watchSlitDistance = slitDistance;
        watchSlitWidth = slitWidth;
        watchScreenDistance = screenDistance;
        watchScreenHalfWidth = screenHalfWidthMm;
        watchIsWhiteLight = isWhiteLight;
    }

    private void SaveLUTAsset()
    {
#if UNITY_EDITOR
        if (lutTexture == null) return;
        try
        {
            string dir = System.IO.Path.GetDirectoryName(LUT_ASSET_PATH);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(LUT_ASSET_PATH, lutTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(LUT_ASSET_PATH, ImportAssetOptions.ForceUpdate);
        }
        catch (System.Exception ex)
        {
        Debug.LogError("[DoubleSlit] 保存 LUT 失败: " + ex.Message);
    }
#endif  
}
}