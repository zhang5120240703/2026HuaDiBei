using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 双缝干涉条纹间距测量工具
/// 提供在屏幕上测量相邻亮纹间距的功能
/// </summary>
[AddComponentMenu("DoubleSlit/Double Slit Measurement Tool")]
public class DoubleSlitMeasurementTool : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector 字段
    // ══════════════════════════════════════════════

    [Header("── UI 组件引用 ──")]
    public GameObject measurementPanel;
    public TextMeshProUGUI measurementText;
    public Button startMeasurementButton;
    public Button recordMeasurementButton;
    public Button cancelMeasurementButton;
    public Slider measurementScaleSlider;
    public TextMeshProUGUI scaleValueText;

    [Header("── 测量参数 ──")]
    [Tooltip("测量线材质")]
    public Material measurementLineMaterial;
    [Tooltip("测量点材质")]
    public Material measurementPointMaterial;
    [Range(0.1f, 5f)] public float measurementScale = 1f;

    [Header("── 视觉反馈 ──")]
    public Color measurementLineColor = new Color(1f, 0.8f, 0f, 0.8f);
    public Color measurementPointColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [Range(0.01f, 0.1f)] public float lineWidth = 0.02f;
    [Range(0.05f, 0.2f)] public float pointSize = 0.1f;

    // ══════════════════════════════════════════════
    //  事件回调
    // ══════════════════════════════════════════════

    [Header("── 事件回调 ──")]
    public UnityEngine.Events.UnityEvent<float> onMeasurementRecorded; // 测量值 (mm)

    // ══════════════════════════════════════════════
    //  运行时状态
    // ══════════════════════════════════════════════

    [Header("── 运行时状态（只读）──")]
    [SerializeField] private bool isMeasuring;
    [SerializeField] private Vector3 startPoint;
    [SerializeField] private Vector3 endPoint;
    [SerializeField] private float currentMeasurement;

    // ══════════════════════════════════════════════
    //  私有字段
    // ══════════════════════════════════════════════

    private LineRenderer measurementLine;
    private GameObject startPointObj;
    private GameObject endPointObj;
    private DoubleSlitSimpleController experimentController;

    // ══════════════════════════════════════════════
    //  属性访问器
    // ══════════════════════════════════════════════

    public bool IsMeasuring => isMeasuring;
    public float CurrentMeasurement => currentMeasurement;

    // ══════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════

    void Awake()
    {
        // 查找实验控制器
        experimentController = FindObjectOfType<DoubleSlitSimpleController>();
        
        // 初始化测量组件
        InitializeMeasurementComponents();
        
        // 初始化UI状态
        if (measurementPanel != null) measurementPanel.SetActive(false);
    }

    void Start()
    {
        // 绑定按钮事件
        if (startMeasurementButton != null)
            startMeasurementButton.onClick.AddListener(StartMeasurement);
        
        if (recordMeasurementButton != null)
            recordMeasurementButton.onClick.AddListener(RecordMeasurement);
        
        if (cancelMeasurementButton != null)
            cancelMeasurementButton.onClick.AddListener(CancelMeasurement);
        
        if (measurementScaleSlider != null)
        {
            measurementScaleSlider.onValueChanged.AddListener(OnScaleChanged);
            measurementScaleSlider.value = measurementScale;
        }
    }

    void Update()
    {
        if (isMeasuring)
        {
            UpdateMeasurement();
        }
    }

    // ══════════════════════════════════════════════
    //  公开接口
    // ══════════════════════════════════════════════

    /// <summary>
    /// 开始测量
    /// </summary>
    public void StartMeasurement()
    {
        if (isMeasuring) return;

        isMeasuring = true;
        
        // 显示测量面板
        if (measurementPanel != null) measurementPanel.SetActive(true);
        
        // 重置测量点
        ResetMeasurementPoints();
        
        // 更新UI
        UpdateMeasurementUI();
        
        Debug.Log("[Measurement] 开始测量条纹间距");
    }

    /// <summary>
    /// 记录当前测量值
    /// </summary>
    public void RecordMeasurement()
    {
        if (!isMeasuring || currentMeasurement <= 0f) return;

        // 通知实验控制器
        if (experimentController != null)
        {
            experimentController.RecordMeasurement(currentMeasurement);
        }
        
        onMeasurementRecorded?.Invoke(currentMeasurement);
        
        // 结束测量
        EndMeasurement();
        
        Debug.Log($"[Measurement] 记录测量值: {currentMeasurement:F3}mm");
    }

    /// <summary>
    /// 取消测量
    /// </summary>
    public void CancelMeasurement()
    {
        if (!isMeasuring) return;
        
        EndMeasurement();
        Debug.Log("[Measurement] 测量已取消");
    }

    /// <summary>
    /// 设置测量比例尺
    /// </summary>
    public void SetMeasurementScale(float scale)
    {
        measurementScale = Mathf.Clamp(scale, 0.1f, 5f);
        
        if (measurementScaleSlider != null)
            measurementScaleSlider.value = measurementScale;
        
        if (scaleValueText != null)
            scaleValueText.text = $"比例尺: {measurementScale:F1}x";
        
        // 重新计算当前测量值
        if (isMeasuring)
        {
            CalculateMeasurement();
            UpdateMeasurementUI();
        }
    }

    // ══════════════════════════════════════════════
    //  内部方法
    // ══════════════════════════════════════════════

    /// <summary>
    /// 初始化测量组件
    /// </summary>
    private void InitializeMeasurementComponents()
    {
        // 创建测量线
        GameObject lineObj = new GameObject("MeasurementLine");
        lineObj.transform.SetParent(transform);
        measurementLine = lineObj.AddComponent<LineRenderer>();
        
        measurementLine.positionCount = 2;
        measurementLine.startWidth = lineWidth;
        measurementLine.endWidth = lineWidth;
        measurementLine.useWorldSpace = true;
        measurementLine.material = measurementLineMaterial ?? CreateDefaultMaterial(measurementLineColor);
        measurementLine.enabled = false;
        
        // 创建测量点
        startPointObj = CreateMeasurementPoint("StartPoint", measurementPointColor);
        endPointObj = CreateMeasurementPoint("EndPoint", measurementPointColor);
    }

    /// <summary>
    /// 创建测量点对象
    /// </summary>
    private GameObject CreateMeasurementPoint(string name, Color color)
    {
        GameObject pointObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pointObj.name = name;
        pointObj.transform.SetParent(transform);
        pointObj.transform.localScale = Vector3.one * pointSize;
        
        Renderer renderer = pointObj.GetComponent<Renderer>();
        renderer.material = measurementPointMaterial ?? CreateDefaultMaterial(color);
        pointObj.SetActive(false);
        
        // 移除碰撞体
        DestroyImmediate(pointObj.GetComponent<Collider>());
        
        return pointObj;
    }

    /// <summary>
    /// 创建默认材质
    /// </summary>
    private Material CreateDefaultMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Glossiness", 0f);
        return mat;
    }

    /// <summary>
    /// 更新测量过程
    /// </summary>
    private void UpdateMeasurement()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 设置起始点
            if (GetScreenPoint(out Vector3 point))
            {
                startPoint = point;
                startPointObj.transform.position = startPoint;
                startPointObj.SetActive(true);
                
                measurementLine.SetPosition(0, startPoint);
                measurementLine.enabled = true;
            }
        }
        else if (Input.GetMouseButton(0) && startPointObj.activeSelf)
        {
            // 更新结束点
            if (GetScreenPoint(out Vector3 point))
            {
                endPoint = point;
                endPointObj.transform.position = endPoint;
                endPointObj.SetActive(true);
                
                measurementLine.SetPosition(1, endPoint);
                
                CalculateMeasurement();
                UpdateMeasurementUI();
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // 测量完成，等待记录或取消
        }
        
        // ESC键取消测量
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMeasurement();
        }
    }

    /// <summary>
    /// 获取屏幕上的测量点
    /// </summary>
    private bool GetScreenPoint(out Vector3 point)
    {
        point = Vector3.zero;
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // 检查是否点击在屏幕物体上
            if (hit.collider.gameObject.name.Contains("Screen") || 
                hit.collider.gameObject.name.Contains("光屏"))
            {
                point = hit.point;
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 计算测量值
    /// </summary>
    private void CalculateMeasurement()
    {
        if (!startPointObj.activeSelf || !endPointObj.activeSelf) return;
        
        // 计算两点间距离（世界单位）
        float distance = Vector3.Distance(startPoint, endPoint);
        
        // 转换为毫米，应用比例尺
        currentMeasurement = distance * 1000f * measurementScale; // m -> mm
    }

    /// <summary>
    /// 更新测量UI
    /// </summary>
    private void UpdateMeasurementUI()
    {
        if (measurementText == null) return;
        
        if (startPointObj.activeSelf && endPointObj.activeSelf)
        {
            measurementText.text = $"📏 条纹间距: {currentMeasurement:F3} mm\n" +
                                  $"点击[记录]保存测量值";
            
            if (recordMeasurementButton != null)
                recordMeasurementButton.interactable = currentMeasurement > 0f;
        }
        else if (startPointObj.activeSelf)
        {
            measurementText.text = "🖱 拖动鼠标设置结束点...";
        }
        else
        {
            measurementText.text = "🖱 点击屏幕设置起始点...";
        }
    }

    /// <summary>
    /// 重置测量点
    /// </summary>
    private void ResetMeasurementPoints()
    {
        startPointObj.SetActive(false);
        endPointObj.SetActive(false);
        measurementLine.enabled = false;
        currentMeasurement = 0f;
    }

    /// <summary>
    /// 结束测量
    /// </summary>
    private void EndMeasurement()
    {
        isMeasuring = false;
        
        // 隐藏测量面板
        if (measurementPanel != null) measurementPanel.SetActive(false);
        
        // 重置测量组件
        ResetMeasurementPoints();
    }

    /// <summary>
    /// 比例尺变化回调
    /// </summary>
    private void OnScaleChanged(float value)
    {
        SetMeasurementScale(value);
    }

    // ══════════════════════════════════════════════
    //  Editor 调试
    // ══════════════════════════════════════════════

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (isMeasuring && startPointObj.activeSelf && endPointObj.activeSelf)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPoint, endPoint);
            
            // 显示测量值
            UnityEditor.Handles.Label(
                (startPoint + endPoint) * 0.5f + Vector3.up * 0.1f,
                $"Δx = {currentMeasurement:F2}mm"
            );
        }
    }
#endif
}