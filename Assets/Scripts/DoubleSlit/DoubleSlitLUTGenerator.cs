using UnityEngine;

[ExecuteInEditMode] // 允许在编辑器模式下实时预览
public class DoubleSlitLUTGenerator : MonoBehaviour
{
    public Material interferenceMaterial; // 挂载干涉Shader的材质
    public int lutWidth = 1024;           // LUT纹理的宽度，越大精度越高

    [Header("Physics Parameters")]
    [Range(380, 780)] public float wavelength = 550f;
    [Range(0.01f, 1.0f)] public float slitDistance = 0.2f;
    [Range(0.001f, 0.5f)] public float slitWidth = 0.05f;
    [Range(0.1f, 10.0f)] public float screenDistance = 1.0f;

    [Header("Light Mode")]
    public bool isWhiteLight = false;

    private Texture2D lutTexture;
    private float prevWavelength, prevSlitDistance, prevSlitWidth, prevScreenDistance;
    private bool prevIsWhiteLight;

    void Start()
    {
        GenerateLUT();
    }

    // 当在Inspector中修改参数时自动触发
    void OnValidate()
    {
        if (wavelength != prevWavelength || slitDistance != prevSlitDistance ||
            slitWidth != prevSlitWidth || screenDistance != prevScreenDistance ||
            isWhiteLight != prevIsWhiteLight)
        {
            GenerateLUT();
        }
    }

    void GenerateLUT()
    {
        if (interferenceMaterial == null) return;

        // 初始化或重建纹理
        if (lutTexture == null || lutTexture.width != lutWidth)
        {
            // 关闭sRGB，因为这是数据纹理而非颜色纹理，避免线性空间转换导致偏色[4](@ref)
            lutTexture = new Texture2D(lutWidth, 1, TextureFormat.ARGB32, false, true);
            lutTexture.wrapMode = TextureWrapMode.Clamp; // 防止边缘重复[4](@ref)
            lutTexture.filterMode = FilterMode.Bilinear; // 双线性过滤使条纹平滑[5](@ref)
        }

        // 白光采样7个特征波长，单色光仅采样1个
        float[] wavelengths = isWhiteLight ? new float[] { 680, 620, 580, 530, 480, 440, 400 } : new float[] { wavelength };
        float normalizeFactor = isWhiteLight ? 4.0f : 1.0f; // 白光叠加后需归一化防过曝

        // 设定LUT覆盖的物理坐标范围，需与Shader中的 _MaxRange 一致
        float maxRange = 50.0f;

        for (int x = 0; x < lutWidth; x++)
        {
            float u = (float)x / (lutWidth - 1); // 0 ~ 1
            float targetX = (u - 0.5f) * 2.0f * maxRange; // 映射到 -maxRange ~ maxRange

            Color finalColor = Color.black;
            float d = slitDistance;
            float a = slitWidth;
            float l = screenDistance * 1000f; // m转mm

            foreach (float wl in wavelengths)
            {
                float lambda = wl * 1e-6f; // nm转mm
                float phaseFactor = Mathf.PI * targetX / (lambda * l);

                // 双缝干涉
                float phase_interference = d * phaseFactor;
                float intensity_interference = Mathf.Cos(phase_interference) * Mathf.Cos(phase_interference);

                // 单缝衍射
                float phase_diffraction = a * phaseFactor;
                float sinc = Mathf.Sin(phase_diffraction) / (phase_diffraction + 1e-7f);
                float intensity_diffraction = sinc * sinc;

                float intensity = intensity_interference * intensity_diffraction;
                Color wlColor = WavelengthToRGB(wl);
                finalColor += wlColor * intensity;
            }

            finalColor /= normalizeFactor;
            lutTexture.SetPixel(x, 0, finalColor);
        }

        lutTexture.Apply();
        // 将生成好的LUT传递给Shader
        interferenceMaterial.SetTexture("_LUT", lutTexture);

        // 缓存当前参数
        prevWavelength = wavelength;
        prevSlitDistance = slitDistance;
        prevSlitWidth = slitWidth;
        prevScreenDistance = screenDistance;
        prevIsWhiteLight = isWhiteLight;
    }

    // 将波长转换为RGB颜色的辅助函数
    Color WavelengthToRGB(float wavelength)
    {
        float r = Mathf.Clamp01(1.0f - Mathf.Abs(wavelength - 645.0f) / 65.0f);
        float g = Mathf.Clamp01(1.0f - Mathf.Abs(wavelength - 540.0f) / 85.0f);
        float b = Mathf.Clamp01(1.0f - Mathf.Abs(wavelength - 440.0f) / 70.0f);

        r *= (wavelength >= 580 ? 1 : 0) + (wavelength < 440 ? 0.5f : 0);
        g *= (wavelength >= 490 && wavelength < 580 ? 1 : 0) + (wavelength >= 440 && wavelength < 490 ? 0.5f : 0);
        b *= (wavelength < 490 ? 1 : 0);

        return new Color(r, g, b);
    }
}
