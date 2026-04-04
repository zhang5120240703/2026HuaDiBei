using UnityEngine;
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
    
    // 图表参数
    private const float pointSize = 0.05f;
    private const float lineWidth = 0.02f;
    
    private void Start()
    {
        // 初始化图表
        InitializeGraphs();
        
        // 监听数据变化
        dataCollector.OnDataCollected += UpdateGraphs;
        dataCollector.OnAnalysisCompleted += UpdateGraphs;
    }
    
    private void InitializeGraphs()
    {
        // 创建PV关系图表的LineRenderer
        pvLineRenderer = CreateLineRenderer(pvGraphArea, Color.blue, lineWidth);
        pvFitLineRenderer = CreateLineRenderer(pvGraphArea, Color.cyan, lineWidth * 0.5f);
        
        // 创建P-1/V关系图表的LineRenderer
        pInverseVLineRenderer = CreateLineRenderer(pInverseVGraphArea, Color.red, lineWidth);
        pInverseVFitLineRenderer = CreateLineRenderer(pInverseVGraphArea, Color.black, lineWidth * 0.5f);
        
        // 绘制坐标轴
        DrawAxes();
    }
    
    private LineRenderer CreateLineRenderer(RectTransform parent, Color color, float width)
    {
        GameObject lineObject = new GameObject("LineRenderer");
        lineObject.transform.SetParent(parent);
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localScale = Vector3.one;
        
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 0;
        
        return lineRenderer;
    }
    
    private void DrawAxes()
    {
        // 绘制PV图表坐标轴
        DrawAxis(pvGraphArea, "体积 (L)", "压力 (kPa)");
        
        // 绘制P-1/V图表坐标轴
        DrawAxis(pInverseVGraphArea, "1/体积 (1/L)", "压力 (kPa)");
    }
    
    private void DrawAxis(RectTransform graphArea, string xLabel, string yLabel)
    {
        // 这里可以添加UI文本元素来显示坐标轴标签
        // 简化实现，实际项目中可以使用TextMeshPro或UI Text
    }
    
    public void UpdateGraphs()
    {
        var dataPoints = dataCollector.GetDataPoints();
        if (dataPoints.Count == 0) return;
        
        // 更新PV关系图表
        UpdatePVGraph(dataPoints);
        
        // 更新P-1/V关系图表
        UpdatePInverseVGraph(dataPoints);
        
        // 绘制数据点标记
        DrawDataPointMarkers(dataPoints);
    }
    
    private void UpdatePVGraph(List<DataCollector.DataPoint> dataPoints)
    {
        // 更新数据点连线
        pvLineRenderer.positionCount = dataPoints.Count;
        for (int i = 0; i < dataPoints.Count; i++)
        {
            Vector3 position = GetPVGraphPosition(dataPoints[i]);
            pvLineRenderer.SetPosition(i, position);
        }
        
        // 绘制拟合曲线（等温过程）
        if (gasSimulation.currentProcess == IdealGasSimulation.ProcessType.Isothermal && dataPoints.Count >= 2)
        {
            DrawPVFitCurve();
        }
    }
    
    private void UpdatePInverseVGraph(List<DataCollector.DataPoint> dataPoints)
    {
        // 更新数据点连线
        pInverseVLineRenderer.positionCount = dataPoints.Count;
        for (int i = 0; i < dataPoints.Count; i++)
        {
            Vector3 position = GetPInverseVGraphPosition(dataPoints[i]);
            pInverseVLineRenderer.SetPosition(i, position);
        }
        
        // 绘制线性拟合直线（等温过程）
        if (gasSimulation.currentProcess == IdealGasSimulation.ProcessType.Isothermal && dataPoints.Count >= 2)
        {
            DrawPInverseVFitLine(dataPoints);
        }
    }
    
    private Vector3 GetPVGraphPosition(DataCollector.DataPoint point)
    {
        // 归一化坐标
        float x = (point.volume - gasSimulation.GetMinVolume()) / (gasSimulation.GetMaxVolume() - gasSimulation.GetMinVolume());
        float y = (point.pressure - 50.0f) / 200.0f; // 假设压力范围50-250kPa
        
        // 转换为图表坐标
        return new Vector3(x - 0.5f, y - 0.5f, 0);
    }
    
    private Vector3 GetPInverseVGraphPosition(DataCollector.DataPoint point)
    {
        // 归一化坐标
        float x = (point.inverseVolume - 0.5f) / 1.5f; // 1/V范围0.5-2.0
        float y = (point.pressure - 50.0f) / 200.0f; // 假设压力范围50-250kPa
        
        // 转换为图表坐标
        return new Vector3(x - 0.5f, y - 0.5f, 0);
    }
    
    private void DrawPVFitCurve()
    {
        // 绘制等温过程的PV拟合曲线
        float temperature = gasSimulation.GetTemperature();
        float moles = gasSimulation.moles;
        float R = 8.314f;
        
        int pointCount = 50;
        pvFitLineRenderer.positionCount = pointCount;
        
        float minVolume = gasSimulation.GetMinVolume();
        float maxVolume = gasSimulation.GetMaxVolume();
        
        for (int i = 0; i < pointCount; i++)
        {
            float volume = minVolume + (maxVolume - minVolume) * i / (pointCount - 1);
            float pressure = (moles * R * temperature) / volume;
            
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
        // 线性拟合：P = k * (1/V)，其中k = nRT
        float k = dataCollector.GetAveragePVProduct();
        
        int pointCount = 2;
        pInverseVFitLineRenderer.positionCount = pointCount;
        
        float minInverseVolume = 1.0f / gasSimulation.GetMaxVolume();
        float maxInverseVolume = 1.0f / gasSimulation.GetMinVolume();
        
        // 起点
        DataCollector.DataPoint startPoint = new DataCollector.DataPoint
        {
            inverseVolume = minInverseVolume,
            pressure = k * minInverseVolume
        };
        pInverseVFitLineRenderer.SetPosition(0, GetPInverseVGraphPosition(startPoint));
        
        // 终点
        DataCollector.DataPoint endPoint = new DataCollector.DataPoint
        {
            inverseVolume = maxInverseVolume,
            pressure = k * maxInverseVolume
        };
        pInverseVFitLineRenderer.SetPosition(1, GetPInverseVGraphPosition(endPoint));
    }
    
    private void DrawDataPointMarkers(List<DataCollector.DataPoint> dataPoints)
    {
        // 清除旧标记
        ClearDataPointMarkers();
        
        // 绘制新标记
        foreach (var point in dataPoints)
        {
            // PV图表标记
            GameObject marker1 = CreateDataPointMarker(pvGraphArea, GetPVGraphPosition(point), Color.blue);
            dataPointMarkers.Add(marker1);
            
            // P-1/V图表标记
            GameObject marker2 = CreateDataPointMarker(pInverseVGraphArea, GetPInverseVGraphPosition(point), Color.red);
            dataPointMarkers.Add(marker2);
        }
    }
    
    private GameObject CreateDataPointMarker(RectTransform parent, Vector3 position, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.SetParent(parent);
        marker.transform.localScale = new Vector3(pointSize, pointSize, pointSize);
        marker.transform.localPosition = new Vector3(position.x, position.y, -1);
        marker.GetComponent<Renderer>().material.color = color;
        return marker;
    }
    
    private void ClearDataPointMarkers()
    {
        foreach (var marker in dataPointMarkers)
        {
            Destroy(marker);
        }
        dataPointMarkers.Clear();
    }
    
    // 重置图表
    public void ResetGraphs()
    {
        // 清除数据点标记
        ClearDataPointMarkers();
        
        // 重置线条渲染器
        if (pvLineRenderer != null) pvLineRenderer.positionCount = 0;
        if (pInverseVLineRenderer != null) pInverseVLineRenderer.positionCount = 0;
        if (pvFitLineRenderer != null) pvFitLineRenderer.positionCount = 0;
        if (pInverseVFitLineRenderer != null) pInverseVFitLineRenderer.positionCount = 0;
    }
}