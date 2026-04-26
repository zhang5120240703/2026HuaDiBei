using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class PendulumEnergyVisualizer : MonoBehaviour
{
    [Header("=== 核心绑定 ===")]
    public Rigidbody ballRb;
    public Transform pendulumAnchor;

    [Header("=== 物理参数 ===")]
    public float gravity = 9.8f;
    public float ballMass = 1f;

    [Header("=== UI显示设置 ===")]
    public TMP_Text kineticEnergyText;
    public TMP_Text potentialEnergyText;
    public TMP_Text totalEnergyText;
    public RawImage energyGraphImage;
    public int graphWidth = 500;
    public int graphHeight = 200;
    public Color kineticEnergyColor = Color.red;
    public Color potentialEnergyColor = Color.blue;
    public Color totalEnergyColor = Color.green;
    public int curvePointSize = 2;

    // 能量数据缓存
    private float _maxEnergy = 1f;
    private Texture2D _energyGraphTexture;
    private Color[] _graphPixels;
    private float[] _kineticEnergyBuffer;
    private float[] _potentialEnergyBuffer;
    private float[] _totalEnergyBuffer;
    private int _bufferIndex;

    // 性能优化
    private float _updateInterval = 0.02f;
    private float _lastUpdateTime;

    #region ===================== 【AI 实验数据接口】 =====================
    /// <summary>
    /// 获取当前动能
    /// 单位：焦耳(J)
    /// </summary>
    public float GetCurrentKinetic()
    {
        int latestIndex = (_bufferIndex - 1 + graphWidth) % graphWidth;
        return _kineticEnergyBuffer[latestIndex];
    }

    /// <summary>
    /// 获取当前势能
    /// 单位：焦耳(J)
    /// </summary>
    public float GetCurrentPotential()
    {
        int latestIndex = (_bufferIndex - 1 + graphWidth) % graphWidth;
        return _potentialEnergyBuffer[latestIndex];
    }

    /// <summary>
    /// 获取当前总机械能
    /// 单位：焦耳(J)
    /// </summary>
    public float GetTotalEnergy()
    {
        int latestIndex = (_bufferIndex - 1 + graphWidth) % graphWidth;
        return _totalEnergyBuffer[latestIndex];
    }
    #endregion

    void Start()
    {
        // 强制开启RawImage，确保可见
        if (energyGraphImage != null)
        {
            energyGraphImage.enabled = true;
            energyGraphImage.color = Color.white;
        }

        // 验证核心组件
        if (ballRb == null || pendulumAnchor == null || energyGraphImage == null)
        {
            Debug.LogError("核心组件未绑定！检查ballRb/pendulumAnchor/energyGraphImage");
            enabled = false;
            return;
        }

        // 初始化能量缓存
        _kineticEnergyBuffer = new float[graphWidth];
        _potentialEnergyBuffer = new float[graphWidth];
        _totalEnergyBuffer = new float[graphWidth];
        _bufferIndex = 0;

        // 初始化纹理
        _energyGraphTexture = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
        _energyGraphTexture.filterMode = FilterMode.Bilinear;
        _energyGraphTexture.wrapMode = TextureWrapMode.Clamp;
        _graphPixels = new Color[graphWidth * graphHeight];

        ClearGraphTexture();
        _energyGraphTexture.Apply();
        energyGraphImage.texture = _energyGraphTexture;

        // 初始化UI文本
        UpdateEnergyText(0, 0, 0);
        Debug.Log($"纹理初始化完成：{graphWidth}x{graphHeight}，RawImage绑定状态：{energyGraphImage.texture != null}");
    }

    void FixedUpdate()
    {
        // 限制更新频率
        if (Time.time - _lastUpdateTime < _updateInterval) return;
        _lastUpdateTime = Time.time;

        CalculateEnergy();
        UpdateEnergyUI();
    }

    /// <summary>
    /// 计算动能、势能、总机械能（适配摆长调节，动态最低点）
    /// </summary>
    private void CalculateEnergy()
    {
        // 1. 实时计算当前摆长
        float currentLength = Vector3.Distance(pendulumAnchor.position, ballRb.position);
        // 2. 计算摆角（与竖直向下的夹角）
        Vector3 dirToBall = (ballRb.position - pendulumAnchor.position).normalized;
        float cosTheta = Mathf.Clamp(Vector3.Dot(dirToBall, -Vector3.up), -1f, 1f);
        // 3. 计算相对最低点的高度差（核心公式：h = L*(1-cosθ)）
        float heightDiff = currentLength * (1 - cosTheta);

        // 4. 计算能量
        float velocity = ballRb.velocity.magnitude;
        float ek = 0.5f * ballMass * velocity * velocity;
        float ep = ballMass * gravity * heightDiff;
        float total = ek + ep;

        // 5. 动态更新最大能量
        _maxEnergy = Mathf.Max(_maxEnergy, total, 1f);

        // 6. 存入缓存
        _kineticEnergyBuffer[_bufferIndex] = ek;
        _potentialEnergyBuffer[_bufferIndex] = ep;
        _totalEnergyBuffer[_bufferIndex] = total;
        _bufferIndex = (_bufferIndex + 1) % graphWidth;

    }

    /// <summary>
    /// 更新能量UI（数值+曲线）
    /// </summary>
    private void UpdateEnergyUI()
    {
        // 获取最新能量值
        int latestIndex = (_bufferIndex - 1 + graphWidth) % graphWidth;
        float ek = _kineticEnergyBuffer[latestIndex];
        float ep = _potentialEnergyBuffer[latestIndex];
        float total = _totalEnergyBuffer[latestIndex];

        // 更新数值文本
        UpdateEnergyText(ek, ep, total);

        // 强制绘制曲线
        if (energyGraphImage != null && _maxEnergy > 0.1f)
        {
            UpdateEnergyGraph();
        }
    }

    /// <summary>
    /// 更新能量数值文本
    /// </summary>
    private void UpdateEnergyText(float ek, float ep, float total)
    {
        // 空引用保护，避免崩溃
        if (kineticEnergyText != null)
            kineticEnergyText.text = $"动能：{ek:F2} J";
        if (potentialEnergyText != null)
            potentialEnergyText.text = $"势能：{ep:F2} J";
        if (totalEnergyText != null)
            totalEnergyText.text = $"总机械能：{total:F2} J";
    }

    /// <summary>
    /// 更新能量变化曲线图
    /// </summary>
    private void UpdateEnergyGraph()
    {
        
        Color bgColor = new Color(0, 0, 0, 0);

        for (int i = 0; i < _graphPixels.Length; i++)
        {
            _graphPixels[i] = bgColor;
        }

        // 绘制曲线
        for (int x = 0; x < graphWidth; x++)
        {
            // 简化索引计算，避免越界
            int bufferIndex = (x + _bufferIndex) % graphWidth;

            // 归一化能量值（强制0~1，避免Y轴越界）
            float ekNorm = Mathf.Clamp01(_kineticEnergyBuffer[bufferIndex] / _maxEnergy);
            float epNorm = Mathf.Clamp01(_potentialEnergyBuffer[bufferIndex] / _maxEnergy);
            float totalNorm = Mathf.Clamp01(_totalEnergyBuffer[bufferIndex] / _maxEnergy);

            // 计算Y轴（翻转+强制在画布内）
            int ekY = Mathf.Clamp(Mathf.RoundToInt((1 - ekNorm) * (graphHeight - 1)), 0, graphHeight - 1);
            int epY = Mathf.Clamp(Mathf.RoundToInt((1 - epNorm) * (graphHeight - 1)), 0, graphHeight - 1);
            int totalY = Mathf.Clamp(Mathf.RoundToInt((1 - totalNorm) * (graphHeight - 1)), 0, graphHeight - 1);

            // 绘制超粗点（彻底解决"点太细看不见"）
            DrawThickPoint(x, ekY, kineticEnergyColor, 3);
            DrawThickPoint(x, epY, potentialEnergyColor, 3);
            DrawThickPoint(x, totalY, totalEnergyColor, 3);
        }

        // 强制应用纹理，立即刷新显示
        _energyGraphTexture.SetPixels(_graphPixels);
        _energyGraphTexture.Apply();
    }

    /// <summary>
    /// 绘制粗点（支持自定义大小）
    /// </summary>
    private void DrawThickPoint(int x, int y, Color color, int size)
    {
        for (int dx = -size; dx <= size; dx++)
        {
            for (int dy = -size; dy <= size; dy++)
            {
                int px = x + dx;
                int py = y + dy;
                if (px >= 0 && px < graphWidth && py >= 0 && py < graphHeight)
                {
                    int index = px + py * graphWidth;
                    _graphPixels[index] = color;
                }
            }
        }
    }

    /// <summary>
    /// 清空曲线图纹理
    /// </summary>
    private void ClearGraphTexture()
    {
       
        Color bgColor = new Color(0, 0, 0, 0);

        for (int i = 0; i < _graphPixels.Length; i++)
        {
            _graphPixels[i] = bgColor;
        }
        if (_energyGraphTexture != null)
        {
            _energyGraphTexture.SetPixels(_graphPixels);
        }
    }

    /// <summary>
    /// 重置能量可视化（配合实验重置/摆长调节）
    /// </summary>
    [ContextMenu("重置能量显示")]
    public void ResetEnergyVisualizer()
    {
        // 重置缓存
        System.Array.Clear(_kineticEnergyBuffer, 0, _kineticEnergyBuffer.Length);
        System.Array.Clear(_potentialEnergyBuffer, 0, _potentialEnergyBuffer.Length);
        System.Array.Clear(_totalEnergyBuffer, 0, _totalEnergyBuffer.Length);
        _bufferIndex = 0;
        _maxEnergy = 1f;

        // 清空曲线
        ClearGraphTexture();
        if (_energyGraphTexture != null)
        {
            _energyGraphTexture.Apply();
        }

        // 重置UI文本（确保方法名与定义完全一致，修复CS0103）
        UpdateEnergyText(0, 0, 0);
    }

    /// <summary>
    /// 编辑器参数校验
    /// </summary>
    private void OnValidate()
    {
        graphWidth = Mathf.Max(200, graphWidth);
        graphHeight = Mathf.Max(100, graphHeight);
        curvePointSize = Mathf.Max(2, curvePointSize);
        gravity = Mathf.Max(9.8f, gravity);
        ballMass = Mathf.Max(0.1f, ballMass);
    }

    /// <summary>
    /// 场景视图辅助线（调试用）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (pendulumAnchor != null && ballRb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pendulumAnchor.position, ballRb.position);
            float len = Vector3.Distance(pendulumAnchor.position, ballRb.position);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pendulumAnchor.position - Vector3.up * len, 0.1f);
        }
    }
}