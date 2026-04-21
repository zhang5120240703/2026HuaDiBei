using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GraphRenderer : MonoBehaviour
{
    // 引用
    public DataCollector dataCollector;
    public IdealGasSimulation gasSimulation;
    
    // 图表区域
    public RectTransform pvGraphArea;
    public RectTransform pInverseVGraphArea;
    
    // 线条渲染器
    private LineRenderer pvLineRenderer;
    private LineRenderer pInverseVLineRenderer;
    private LineRenderer pvFitLineRenderer;
    private LineRenderer pInverseVFitLineRenderer;
    
    // 数据点标记
    private List<GameObject> dataPointMarkers = new List<GameObject>();
    
    // UI 轴对象（用于在 Reset 时移除）
    private List<GameObject> axisObjects = new List<GameObject>();
    
    // 图表参数
    private const float pointSize = 12f; // 像素
    private const float lineWidth = 4f; // 像素


    //字体资源
    public TMP_FontAsset font;

    //图像资源
    public Sprite uiImage;
    private void Start()
    {
        // 初始化图表
        InitializeGraphs();
        
        // 监听数据变化（防 null）
        if (dataCollector != null)
        {
            dataCollector.OnDataCollected += UpdateGraphs;
            dataCollector.OnAnalysisCompleted += UpdateGraphs;
        }
    }

    private void OnDisable()
    {
        if (dataCollector != null)
        {
            dataCollector.OnDataCollected -= UpdateGraphs;
            dataCollector.OnAnalysisCompleted -= UpdateGraphs;
        }
    }
    
    private void InitializeGraphs()
    {
        // 创建 PV 关系图的 LineRenderer
        pvLineRenderer = CreateLineRenderer(pvGraphArea, Color.blue, lineWidth);
        pvFitLineRenderer = CreateLineRenderer(pvGraphArea, Color.cyan, lineWidth * 0.6f);
        
        // 创建 P-1/V 关系图的 LineRenderer
        pInverseVLineRenderer = CreateLineRenderer(pInverseVGraphArea, Color.red, lineWidth);
        pInverseVFitLineRenderer = CreateLineRenderer(pInverseVGraphArea, Color.black, lineWidth * 0.6f);
        
        // 绘制坐标轴（UI）
        DrawAxes(pvGraphArea, "体积 (L)", "压力 (kPa)");
        DrawAxes(pInverseVGraphArea, "1/体积 (1/L)", "压力 (kPa)");
    }

    // DrawAxis: 在指定 panel 上绘制简单坐标轴和标签（左/下轴）
    private void DrawAxes(RectTransform graphArea, string xLabel, string yLabel)
    {
        if (graphArea == null) return;

        // 轴颜色与粗细
        Color axisColor = new Color(1f, 1f, 1f, 1f);
        float axisThickness = 10f;

        // 垂直轴（左）
        GameObject vAxis = CreateUIImage("Axis_V", graphArea, axisColor);
        var vRt = vAxis.GetComponent<RectTransform>();
        // 锚点左中，宽度 axisThickness，高度撑满
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(0f, 1f);
        vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.anchoredPosition = new Vector2(4f, 0f);
        vRt.sizeDelta = new Vector2(axisThickness, 0f);

        // 水平轴（下）
        GameObject hAxis = CreateUIImage("Axis_H", graphArea, axisColor);
        var hRt = hAxis.GetComponent<RectTransform>();
        // 锚点底部，横向撑满，高度 axisThickness
        hRt.anchorMin = new Vector2(0f, 0f);
        hRt.anchorMax = new Vector2(1f, 0f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.anchoredPosition = new Vector2(0f, 4f);
        hRt.sizeDelta = new Vector2(0f, axisThickness);

        // X 轴标签（底部中间）
        GameObject xLabelGO = CreateUIText("XLabel", graphArea, xLabel);
        var xRt = xLabelGO.GetComponent<RectTransform>();
        xRt.anchorMin = new Vector2(0.5f, 0f);
        xRt.anchorMax = new Vector2(0.5f, 0f);
        xRt.pivot = new Vector2(0.5f, 1f);
        xRt.anchoredPosition = new Vector2(0f, 6f);
        xRt.sizeDelta = new Vector2(graphArea.rect.width * 0.6f, 24f);

        // Y 轴标签（左中，旋转）
        GameObject yLabelGO = CreateUIText("YLabel", graphArea, yLabel);
        var yRt = yLabelGO.GetComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0f, 0.5f);
        yRt.anchorMax = new Vector2(0f, 0.5f);
        yRt.pivot = new Vector2(0.5f, 0.5f);
        yRt.anchoredPosition = new Vector2(12f, 0f);
        yRt.sizeDelta = new Vector2(graphArea.rect.height * 0.6f, 24f);
        // 旋转并居中显示为纵向文本
        yRt.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // 记录用于后续清理
        axisObjects.Add(vAxis);
        axisObjects.Add(hAxis);
        axisObjects.Add(xLabelGO);
        axisObjects.Add(yLabelGO);
    }

    // Helper: 创建 Image 作为轴线或其他 UI 背景
    private GameObject CreateUIImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return go;
    }

    // Helper: 创建 TextMeshProUGUI 标签
    private GameObject CreateUIText(string name, RectTransform parent, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 24;
        txt.alignment = TextAlignmentOptions.BottomRight;
        txt.color = Color.red;
        txt.raycastTarget = false;
        txt.font = font; // 使用指定字体资源
        return go;
    }
    
    private LineRenderer CreateLineRenderer(RectTransform parent, Color color, float width)
    {
        GameObject lineObject = new GameObject("LineRenderer");
        lineObject.transform.SetParent(parent, false); // 保持本地变换
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localScale = Vector3.one;

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 0;

        // 关键：在 UI 容器内使用本地坐标
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.TransformZ;

        return lineRenderer;
    }
    
    // 把归一化 (0..1) 转为父 RectTransform 的本地像素坐标（左下为 rect.xMin,yMin）
    private Vector3 ToLocalPosition(RectTransform area, float normX, float normY)
    {
        if (area == null) return Vector3.zero;
        Rect r = area.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(normX));
        float y = Mathf.Lerp(r.yMin, r.yMax, Mathf.Clamp01(normY));
        return new Vector3(x, y, 0f);
    }
    
    // 用父 RectTransform 像素坐标返回 PV 点位置
    private Vector3 GetPVGraphPosition(DataCollector.DataPoint point)
    {
        // 动态范围：优先使用 gasSimulation 范围，若需要可改为基于 dataCollector 数据动态计算 min/max
        float minV = gasSimulation.GetMinVolume();
        float maxV = gasSimulation.GetMaxVolume();
        float minP = 0f; // 你可以替换为更合适的下界，例如 gasSimulation.GetMinPressure()
        float maxP = gasSimulation.GetMaxPressure();

        float normX = Mathf.InverseLerp(minV, maxV, point.volume); // 0..1
        float normY = Mathf.InverseLerp(minP, maxP, point.pressure); // 0..1

        return ToLocalPosition(pvGraphArea, normX, normY);
    }

    private Vector3 GetPInverseVGraphPosition(DataCollector.DataPoint point)
    {
        // 1/V 范围，用数据或模拟范围决定
        float minInvV = 1.0f / Mathf.Max(gasSimulation.GetMaxVolume(), 1e-6f);
        float maxInvV = 1.0f / Mathf.Max(gasSimulation.GetMinVolume(), 1e-6f);
        float minP = 0f;
        float maxP = gasSimulation.GetMaxPressure();

        float normX = Mathf.InverseLerp(minInvV, maxInvV, point.inverseVolume);
        float normY = Mathf.InverseLerp(minP, maxP, point.pressure);

        return ToLocalPosition(pInverseVGraphArea, normX, normY);
    }
    
    public void UpdateGraphs()
    {
        var dataPoints = dataCollector.GetDataPoints();
        if (dataPoints == null || dataPoints.Count == 0) return;
        
        // 更新PV关系图表
        UpdatePVGraph(dataPoints);
        
        // 更新P-1/V关系图表
        UpdatePInverseVGraph(dataPoints);
        
        // 绘制数据点标记
        DrawDataPointMarkers(dataPoints);
    }
    
    private void UpdatePVGraph(List<DataCollector.DataPoint> dataPoints)
    {
        if (pvLineRenderer == null) return;

        // 更新数据点连线
        pvLineRenderer.positionCount = dataPoints.Count;
        for (int i = 0; i < dataPoints.Count; i++)
        {
            Vector3 position = GetPVGraphPosition(dataPoints[i]);
            pvLineRenderer.SetPosition(i, position);
        }
        
        // 绘制拟合曲线（等温过程）
        if (gasSimulation != null && gasSimulation.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isothermal && dataPoints.Count >= 2)
        {
            DrawPVFitCurve();
        }
    }
    
    private void UpdatePInverseVGraph(List<DataCollector.DataPoint> dataPoints)
    {
        if (pInverseVLineRenderer == null) return;

        // 更新数据点连线
        pInverseVLineRenderer.positionCount = dataPoints.Count;
        for (int i = 0; i < dataPoints.Count; i++)
        {
            Vector3 position = GetPInverseVGraphPosition(dataPoints[i]);
            pInverseVLineRenderer.SetPosition(i, position);
        }
        
        // 绘制线性拟合直线（等温过程）
        if (gasSimulation != null && gasSimulation.GetCurrentProcess() == IdealGasSimulation.ProcessType.Isothermal && dataPoints.Count >= 2)
        {
            DrawPInverseVFitLine(dataPoints);
        }
    }
    
    private void DrawPVFitCurve()
    {
        float temperature = gasSimulation.GetTemperature();
        float moles = gasSimulation.moles;
        float R = IdealGasSimulation.R;
        
        int pointCount = 50;
        if (pvFitLineRenderer == null) return;
        pvFitLineRenderer.positionCount = pointCount;
        
        float minVolume = gasSimulation.GetMinVolume();
        float maxVolume = gasSimulation.GetMaxVolume();
        
        for (int i = 0; i < pointCount; i++)
        {
            float volume = minVolume + (maxVolume - minVolume) * i / (pointCount - 1);
            float pressure = (moles * R * temperature) / Mathf.Max(volume, 1e-6f);
            
            DataCollector.DataPoint point = new DataCollector.DataPoint
            {
                volume = volume,
                pressure = pressure
            };
            
            Vector3 position = GetPVGraphPosition(point);
            pvFitLineRenderer.SetPosition(i, position);
        }
    }
    
    private void DrawPInverseVFitLine(List<DataCollector.DataPoint> dataPoints)
    {
        if (pInverseVFitLineRenderer == null) return;

        float k = dataCollector.GetAveragePVProduct();
        
        pInverseVFitLineRenderer.positionCount = 2;
        DataCollector.DataPoint startPoint = new DataCollector.DataPoint
        {
            inverseVolume = 1.0f / gasSimulation.GetMaxVolume(),
            pressure = k * (1.0f / gasSimulation.GetMaxVolume())
        };
        DataCollector.DataPoint endPoint = new DataCollector.DataPoint
        {
            inverseVolume = 1.0f / gasSimulation.GetMinVolume(),
            pressure = k * (1.0f / gasSimulation.GetMinVolume())
        };
        pInverseVFitLineRenderer.SetPosition(0, GetPInverseVGraphPosition(startPoint));
        pInverseVFitLineRenderer.SetPosition(1, GetPInverseVGraphPosition(endPoint));
    }
    
    private void DrawDataPointMarkers(List<DataCollector.DataPoint> dataPoints)
    {
        // 清除旧标记
        ClearDataPointMarkers();
        
        // 绘制新标记（使用 UI Image）
        Sprite uiSprite = uiImage;
        foreach (var point in dataPoints)
        {
            GameObject marker1 = CreateDataPointMarker(pvGraphArea, GetPVGraphPosition(point), Color.cyan, uiSprite);
            dataPointMarkers.Add(marker1);
            GameObject marker2 = CreateDataPointMarker(pInverseVGraphArea, GetPInverseVGraphPosition(point), Color.yellow, uiSprite);
            dataPointMarkers.Add(marker2);
        }
    }
    
    //生成数据点标记
    private GameObject CreateDataPointMarker(RectTransform parent, Vector3 localPos, Color color, Sprite sprite)
    {
        GameObject go = new GameObject("DataPoint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = localPos;
        rt.sizeDelta = new Vector2(pointSize, pointSize);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return go;
    }
    

    //清除数据点标记
    private void ClearDataPointMarkers()
    {
        foreach (var m in dataPointMarkers) if (m != null) Destroy(m);
        dataPointMarkers.Clear();
    }
    
    // 重置图表
    public void ResetGraphs()
    {
        // 清除轴与标记
        //foreach (var a in axisObjects) if (a != null) Destroy(a);
        //axisObjects.Clear();
        ClearDataPointMarkers();
        
        // 重置线条渲染器
        if (pvLineRenderer != null) pvLineRenderer.positionCount = 0;
        if (pInverseVLineRenderer != null) pInverseVLineRenderer.positionCount = 0;
        if (pvFitLineRenderer != null) pvFitLineRenderer.positionCount = 0;
        if (pInverseVFitLineRenderer != null) pInverseVFitLineRenderer.positionCount = 0;
        

    }
}