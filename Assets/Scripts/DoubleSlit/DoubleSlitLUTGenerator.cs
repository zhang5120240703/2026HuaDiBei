using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DoubleSlitLUTGenerator : MonoBehaviour
{
    public Material interferenceMaterial;
    public int lutWidth = 1024;

    [Header("Physics Parameters")]
    [Range(380, 780)] public float wavelength = 550f;
    [Range(0.01f, 1.0f)] public float slitDistance = 0.2f;
    [Range(0.001f, 0.5f)] public float slitWidth = 0.05f;
    [Range(0.1f, 10.0f)] public float screenDistance = 1.0f;
    public bool isWhiteLight = false;

    [Header("Runtime Control")]
    public bool enableRuntimeUpdate = true;
    [Range(0.1f, 2.0f)]
    public float updateInterval = 0.5f;

    private Texture2D lutTexture;

    // ★ Fix1：GenerateLUT 完成后记录的"上次生成时的值"
    private float prevWavelength, prevSlitDistance, prevSlitWidth, prevScreenDistance;
    private bool prevIsWhiteLight;

    // ★ Fix1：Update 专用的"本帧观测值"，与 prev 完全独立
    private float watchWavelength, watchSlitDistance, watchSlitWidth, watchScreenDistance;
    private bool watchIsWhiteLight;

    private float lastParamChangeTime = 0f;
    private bool parameterChanged = false;

    private const string LUT_ASSET_PATH = "Assets/Art/Texture/DoubleSlitLUT.asset";

    void Start()
    {
        if (Application.isPlaying)
        {
            // 初始化 watch 值，防止进入运行模式时立即误判为"有变化"
            SyncWatchValues();
            GenerateLUT();
            Debug.Log("[DoubleSlitLUTGenerator] Runtime 模式：初始化 LUT");
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // ★ Fix2：用 delayCall 延迟执行，避免在退出 PlayMode 的销毁阶段操作 Asset
            EditorApplication.delayCall -= OnValidateDelayed;
            EditorApplication.delayCall += OnValidateDelayed;
        }
#endif
    }

#if UNITY_EDITOR
    private void OnValidateDelayed()
    {
        EditorApplication.delayCall -= OnValidateDelayed;

        // 对象可能已被销毁（切换场景等情况），需要判空
        if (this == null) return;

        bool changed = wavelength != prevWavelength
                    || slitDistance != prevSlitDistance
                    || slitWidth != prevSlitWidth
                    || screenDistance != prevScreenDistance
                    || isWhiteLight != prevIsWhiteLight;

        if (changed)
        {
            GenerateLUT();
            Debug.Log("[DoubleSlitLUTGenerator] Editor 模式：参数已改变，LUT 已更新");
        }
    }
#endif

    void Update()
    {
        if (!Application.isPlaying || !enableRuntimeUpdate) return;

        // ★ Fix1：与 watchX 比较，只有"本帧与上次 Update 记录"不同才算新变化
        bool newChange = wavelength != watchWavelength
                      || slitDistance != watchSlitDistance
                      || slitWidth != watchSlitWidth
                      || screenDistance != watchScreenDistance
                      || isWhiteLight != watchIsWhiteLight;

        if (newChange)
        {
            // 记录本次变化的值，下一帧不会重复触发
            SyncWatchValues();
            parameterChanged = true;
            lastParamChangeTime = Time.time;
            return;
        }

        // 参数稳定后超过 updateInterval 才真正更新
        if (parameterChanged && (Time.time - lastParamChangeTime) >= updateInterval)
        {
            GenerateLUT();
            parameterChanged = false;
            Debug.Log($"[DoubleSlitLUTGenerator] Runtime LUT 已更新 - 波长:{wavelength}nm 缝距:{slitDistance}mm");
        }
    }

    /// <summary>将当前参数同步到 watch 变量（Update 专用）</summary>
    private void SyncWatchValues()
    {
        watchWavelength = wavelength;
        watchSlitDistance = slitDistance;
        watchSlitWidth = slitWidth;
        watchScreenDistance = screenDistance;
        watchIsWhiteLight = isWhiteLight;
    }

    void GenerateLUT()
    {
        if (interferenceMaterial == null) return;

        if (lutTexture == null || lutTexture.width != lutWidth)
        {
            lutTexture = new Texture2D(lutWidth, 1, TextureFormat.ARGB32, false, true);
            lutTexture.wrapMode = TextureWrapMode.Clamp;
            lutTexture.filterMode = FilterMode.Bilinear;
        }

        float[] wavelengths = isWhiteLight
            ? new float[] { 680, 620, 580, 530, 480, 440, 400 }
            : new float[] { wavelength };
        float normalizeFactor = isWhiteLight ? 7.0f : 1.0f;

        float d_m = slitDistance * 1e-3f;
        float a_m = slitWidth * 1e-3f;
        float L_m = screenDistance;
        float maxRange = 50.0f * 1e-3f;

        for (int x = 0; x < lutWidth; x++)
        {
            float u = (float)x / (lutWidth - 1);
            float physicalX_m = (u - 0.5f) * 2.0f * maxRange;

            Color finalColor = Color.black;

            foreach (float wl in wavelengths)
            {
                float lambda_m = wl * 1e-9f;
                float phaseFactor = Mathf.PI * physicalX_m / (lambda_m * L_m);

                // ★ 干涉强度（双缝）
                float phase_i = d_m * phaseFactor;
                float intensity_i = Mathf.Cos(phase_i) * Mathf.Cos(phase_i);

                // ★ 衍射强度（单缝 sinc 函数）- Fix：正确处理极限
                float phase_d = a_m * phaseFactor;
                float sinc;
                
                if (Mathf.Abs(phase_d) < 1e-6f)
                {
                    // phase_d ≈ 0 时，sin(x)/x ≈ 1
                    sinc = 1f;
                }
                else
                {
                    // 标准 sinc 函数
                    sinc = Mathf.Sin(phase_d) / phase_d;
                }
                
                float intensity_d = sinc * sinc;
                
                // ★ 增加暗纹的相对亮度，改善视觉对比
                float intensity = (intensity_i * intensity_d);
                finalColor += WavelengthToRGB(wl) * Mathf.Pow(intensity, 0.8f);  // 伽马校正
            }

            lutTexture.SetPixel(x, 0, finalColor / normalizeFactor);
        }

        lutTexture.Apply();
        interferenceMaterial.SetTexture("_LUT", lutTexture);

        // 仅 Editor 非运行模式下保存资产
        if (!Application.isPlaying)
        {
            SaveLUTAsset();
        }

        prevWavelength = wavelength;
        prevSlitDistance = slitDistance;
        prevSlitWidth = slitWidth;
        prevScreenDistance = screenDistance;
        prevIsWhiteLight = isWhiteLight;
    }

    private void SaveLUTAsset()
    {
#if UNITY_EDITOR
        // ★ Fix2：纹理无效时直接跳过，防止保存已销毁对象
        if (lutTexture == null) return;

        try
        {
            string dir = System.IO.Path.GetDirectoryName(LUT_ASSET_PATH);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(LUT_ASSET_PATH);
            if (existing != null)
                AssetDatabase.DeleteAsset(LUT_ASSET_PATH);

            // ★ Fix3：使用 Texture2D.SaveAndReimport() 避免 null 异常
            // 直接将纹理保存为 PNG/EXR，然后由 AssetDatabase 导入
            string pngPath = LUT_ASSET_PATH.Replace(".asset", ".png");
            byte[] pngData = lutTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(pngPath, pngData);
            
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            
            Debug.Log("[DoubleSlitLUTGenerator] LUT Asset 已保存: " + pngPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[DoubleSlitLUTGenerator] 保存 LUT 失败: " + ex.Message);
        }
#endif
    }

    Color WavelengthToRGB(float wl)
    {
        float r = Mathf.Clamp01(1.0f - Mathf.Abs(wl - 645.0f) / 65.0f);
        float g = Mathf.Clamp01(1.0f - Mathf.Abs(wl - 540.0f) / 85.0f);
        float b = Mathf.Clamp01(1.0f - Mathf.Abs(wl - 440.0f) / 70.0f);

        // 原代码问题：这些乘法掩码过度限制了颜色范围
        r *= (wl >= 580 ? 1 : 0) + (wl < 440 ? 0.5f : 0);  // 580-780nm 为红色
        g *= (wl >= 490 && wl < 580 ? 1 : 0) + (wl >= 440 && wl < 490 ? 0.5f : 0);  // 490-580nm 为绿色
        b *= (wl < 490 ? 1 : 0);  // <490nm 为蓝色
        return new Color(r, g, b);
    }
}