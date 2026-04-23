

using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 双缝干涉 LUT 生成器
/// </summary>
[ExecuteInEditMode]
public class DoubleSlitLUTGenerator : MonoBehaviour
{
    [Header("材质")]
    public Material interferenceMaterial;
    [Range(64, 2048)] public int lutWidth = 1024;

    [Header("物理参数")]
    [Range(380, 780)] public float wavelength = 550f;
    [Range(0.01f, 1f)] public float slitDistance = 0.2f;
    [Range(0.001f, 0.5f)] public float slitWidth = 0.05f;
    [Range(0.1f, 10f)] public float screenDistance = 1.0f;
    [Range(5f, 100f)] public float screenHalfWidthMm = 20f;
    public bool isWhiteLight = false;

    [Header("光路可视化")]
    public Transform lightSourceTf;
    public Transform singleSlitTf;
    public Transform doubleSlitTf;
    public Material beamMaterial;
    [Range(0.005f, 0.2f)] public float sourceBeamWidth = 0.05f;
    [Range(0.005f, 0.1f)] public float slitBeamWidth = 0.02f;
    [Range(0f, 1f)] public float beamAlpha = 0.75f;

    [Header("运行时更新")]
    public bool enableRuntimeUpdate = true;
    [Range(0.1f, 3f)] public float updateInterval = 0.5f;

    // ── 私有字段 ──────────────────────────────────────────────────
    private Texture2D lutTexture;
    private Color32[] pixelBuffer;
    private LineRenderer sourceBeamLR;
    private LineRenderer slitBeamLR;
    // 1x1 白色纹理，用于第一阶段显示白色屏幕
    private Texture2D whiteLUTTexture;
    // 当前是否应在材质上显示 LUT（由 SimpleController 控制）
    private bool _lutVisible = false;

    //外部强制隐藏光束标志
    private bool _beamsForceHidden = false;// 防止协程自动把光束重新打开

    private static readonly GradientColorKey[] s_monoColorKeys = new GradientColorKey[2];
    private static readonly GradientAlphaKey[] s_monoAlphaKeys = new GradientAlphaKey[2];
    private static readonly GradientColorKey[] s_rainbowColorKeys = new GradientColorKey[5];
    private static readonly GradientAlphaKey[] s_rainbowAlphaKeys = new GradientAlphaKey[2];
    private static readonly Gradient s_sharedGrad = new Gradient();

    private float watchWavelength, watchSlitDistance, watchSlitWidth, watchScreenDistance, watchScreenHalfWidth;
    private bool watchIsWhiteLight;
    private float prevWavelength, prevSlitDistance, prevSlitWidth, prevScreenDistance, prevScreenHalfWidth;
    private bool prevIsWhiteLight;

    private const string LUT_ASSET_PATH = "Assets/Art/Texture/DoubleSlitLUT.png";

    // ── 生命周期 ──────────────────────────────────────────────────

    void Start()
    {
        if (!Application.isPlaying) return;
        InitBeamRenderers();
        // ★ 启动时光束隐藏，等 SimpleController 调用 SetBeamsEnabled(true)
        SetBeamsEnabledInternal(false);
        SyncWatchValues();
        // 预备一个 1x1 白色纹理用于第一阶段显示白色屏幕
        whiteLUTTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        whiteLUTTexture.SetPixel(0, 0, Color.white);
        whiteLUTTexture.Apply(false, false);

        GenerateLUT();      // 生成 LUT
        SetLUTVisible(false);  // 第一阶段隐藏LUT，显示白色光屏
        if (enableRuntimeUpdate)
            StartCoroutine(RuntimeUpdateCoroutine());
        Debug.Log("[DoubleSlit] LUT 生成器初始化完成");
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

    void OnDestroy()
    {
        if (sourceBeamLR) DestroyImmediate(sourceBeamLR.gameObject);
        if (slitBeamLR) DestroyImmediate(slitBeamLR.gameObject);
    }

    // ── 公开接口 ──────────────────────────────────────────────────

    /// <summary>
    /// ★ 由 DoubleSlitSimpleController 调用，控制光路光束的显隐。
    /// 设为 false 后协程不再重新打开光束。
    /// </summary>
    public void SetBeamsEnabled(bool enable)//关注
    {
        _beamsForceHidden = !enable;
        SetBeamsEnabledInternal(enable);
        // 立即更新光束可视化，确保在外部开启时创建并设置 LineRenderer
        UpdateBeamVisuals();
    }

    /// <summary>强制立即重新生成 LUT（外部调用）</summary>
    public void ForceRegenerate()
    {
        GenerateLUT();
        UpdateBeamVisuals();
    }

    /// <summary>
    /// 控制LUT纹理的显示/隐藏
    /// true = 显示干涉图案（有LUT纹理）
    /// false = 显示白色（无LUT纹理）
    /// </summary>
    public void SetLUTVisible(bool visible)//关注
    {
        _lutVisible = visible;
        if (interferenceMaterial == null) return;

        if (visible)
        {
            // 显示干涉图案：应用LUT纹理
            if (lutTexture != null)
                interferenceMaterial.SetTexture("_LUT", lutTexture);
        }
        else
        {
            // 显示白色：应用 1x1 白色纹理（比设置 null 更可靠，避免 Shader 采样返回黑色）
            if (whiteLUTTexture != null)
                interferenceMaterial.SetTexture("_LUT", whiteLUTTexture);
            else
                interferenceMaterial.SetTexture("_LUT", null);
        }
    }

    // ── 协程 ──────────────────────────────────────────────────────

    private IEnumerator RuntimeUpdateCoroutine()
    {
        var wait = new WaitForSeconds(updateInterval);
        while (true)
        {
            yield return wait;
            if (HasParamsChanged())
            {
                SyncWatchValues();
                GenerateLUT();
                UpdateBeamVisuals();    // UpdateBeamVisuals 内部受 _beamsForceHidden 控制
            }
        }
    }

    private bool HasParamsChanged()
        => wavelength != watchWavelength || slitDistance != watchSlitDistance
        || slitWidth != watchSlitWidth || screenDistance != watchScreenDistance
        || screenHalfWidthMm != watchScreenHalfWidth || isWhiteLight != watchIsWhiteLight;

    // ── LUT 生成 ──────────────────────────────────────────────────

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
        float xMax = screenHalfWidthMm * 1e-3f;

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
                float sinc = Mathf.Abs(phaseD) < 1e-6f ? 1f : Mathf.Sin(phaseD) / phaseD;
                float intensity_d = sinc * sinc;
                acc += wlColors[w] * (intensity_i * intensity_d);
            }
            pixelBuffer[x] = acc / normFactor;
        }

        lutTexture.SetPixels32(pixelBuffer);
        lutTexture.Apply(false);
    // 仅在当前应显示 LUT 时，才把纹理应用到材质上。
    // 否则只更新内存中的 lutTexture，材质仍保持白屏或现有纹理。
    if (_lutVisible && interferenceMaterial != null)
        interferenceMaterial.SetTexture("_LUT", lutTexture);

        prevWavelength = wavelength; prevSlitDistance = slitDistance;
        prevSlitWidth = slitWidth; prevScreenDistance = screenDistance;
        prevScreenHalfWidth = screenHalfWidthMm; prevIsWhiteLight = isWhiteLight;

        if (!Application.isPlaying) SaveLUTAsset();
    }

    // ── 光束可视化 ────────────────────────────────────────────────

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

    /// <summary>直接设置光束 enabled，不改变 _beamsForceHidden 标志</summary>
    private void SetBeamsEnabledInternal(bool enable)
    {
        if (sourceBeamLR != null) sourceBeamLR.enabled = enable;
        if (slitBeamLR != null) slitBeamLR.enabled = enable;
    }

    private void UpdateBeamVisuals()
    {
        // ★ 受强制隐藏标志保护，协程不会意外重开光束
        if (_beamsForceHidden)
        {
            SetBeamsEnabledInternal(false);
            return;
        }

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

    // ── 波长 → RGB ────────────────────────────────────────────────

    public static Color WavelengthToColor(float wl)
    {
        float r, g, b;
        if (wl >= 380f && wl < 440f) { r = -(wl - 440f) / 60f; g = 0f; b = 1f; }
        else if (wl >= 440f && wl < 490f) { r = 0f; g = (wl - 440f) / 50f; b = 1f; }
        else if (wl >= 490f && wl < 510f) { r = 0f; g = 1f; b = -(wl - 510f) / 20f; }
        else if (wl >= 510f && wl < 580f) { r = (wl - 510f) / 70f; g = 1f; b = 0f; }
        else if (wl >= 580f && wl < 645f) { r = 1f; g = -(wl - 645f) / 65f; b = 0f; }
        else if (wl >= 645f && wl <= 780f) { r = 1f; g = 0f; b = 0f; }
        else { r = 0f; g = 0f; b = 0f; }

        float factor = (wl >= 380f && wl < 420f) ? 0.3f + 0.7f * (wl - 380f) / 40f
                     : (wl >= 700f && wl <= 780f) ? 0.3f + 0.7f * (780f - wl) / 80f
                     : 1f;
        return new Color(Mathf.Clamp01(r * factor), Mathf.Clamp01(g * factor), Mathf.Clamp01(b * factor), 1f);
    }

    // ── Editor ────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidateDelayed()
    {
        EditorApplication.delayCall -= OnValidateDelayed;
        if (this == null) return;
        bool changed = wavelength != prevWavelength || slitDistance != prevSlitDistance
                    || slitWidth != prevSlitWidth || screenDistance != prevScreenDistance
                    || screenHalfWidthMm != prevScreenHalfWidth || isWhiteLight != prevIsWhiteLight;
        if (changed) { GenerateLUT(); Debug.Log("[DoubleSlit] Editor 参数已更新"); }
    }
#endif

    private void SyncWatchValues()
    {
        watchWavelength = wavelength; watchSlitDistance = slitDistance;
        watchSlitWidth = slitWidth; watchScreenDistance = screenDistance;
        watchScreenHalfWidth = screenHalfWidthMm; watchIsWhiteLight = isWhiteLight;
    }

    private void SaveLUTAsset()
    {
#if UNITY_EDITOR
        if (lutTexture == null) return;
        try
        {
            string dir = System.IO.Path.GetDirectoryName(LUT_ASSET_PATH);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(LUT_ASSET_PATH, lutTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(LUT_ASSET_PATH, ImportAssetOptions.ForceUpdate);
        }
        catch (System.Exception ex) { Debug.LogError("[DoubleSlit] 保存 LUT 失败: " + ex.Message); }
#endif
    }
}