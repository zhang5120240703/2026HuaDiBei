using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;

public class GraphRenderer : MonoBehaviour
{
    // 引用
    public DataCollector dataCollector;

    // 图表组件（Inspector 指定）
    public LineChart pvChart;
    public LineChart pInverseVChart;
    public LineChart ptChart;
    public LineChart pInverseTChart;

    // 字体（Inspector 指定，可选）
    //public TMP_FontAsset font;

    private bool showLine = false;
    private void Start()
    {
        InitializeCharts(); // 初始化图表

        if (dataCollector != null)
        {
            dataCollector.OnAnalysisCompleted += OnAnalysisCompleted;
        }
    }

    private void OnDisable()
    {
        if (dataCollector != null)
        {
            dataCollector.OnAnalysisCompleted -= OnAnalysisCompleted;
        }
    }

    // 初始化图表
    private void InitializeCharts()
    {
        // 初始化PV图表
        InitializeChart(pvChart, "P-V 图像", "体积(L)", "压力(kPa)");
        
        // 初始化P-1/V图表
        InitializeChart(pInverseVChart, "P-1/V 图像", "1/体积(1/L)", "压力(kPa)");
        
        // 初始化P-T图表
        InitializeChart(ptChart, "P-T 图像", "温度(K)", "压力(kPa)");
        
        // 初始化P-1/T图表
        InitializeChart(pInverseTChart, "P-1/T 图像", "1/温度(1/K)", "压力(kPa)");
    }

    private void InitializeChart(LineChart chart, string title, string xAxisName, string yAxisName)
    {
        if (chart == null) return;
        
        chart.Init();
        
        // 设置标题
        var titleComponent = chart.EnsureChartComponent<Title>();
        titleComponent.show = true;
        titleComponent.text = title;
        
        // 设置提示
        var tooltip = chart.EnsureChartComponent<Tooltip>();
        tooltip.show = true;
        
        // 设置图例
        var legend = chart.EnsureChartComponent<Legend>();
        legend.show = true;
        
        // 设置X轴
        var xAxis = chart.EnsureChartComponent<XAxis>();
        xAxis.show = true;
        xAxis.type = Axis.AxisType.Value;
        xAxis.axisName.show = true;
        xAxis.axisName.name = xAxisName;
        xAxis.axisName.labelStyle.position = LabelStyle.Position.End;
        xAxis.axisLabel.formatter = "{value:F3}";//显示两位小数

        // 设置Y轴
        var yAxis = chart.EnsureChartComponent<YAxis>();
        yAxis.show = true;
        yAxis.type = Axis.AxisType.Value;
        yAxis.axisName.show = true;
        yAxis.axisName.name = yAxisName;
        yAxis.axisName.labelStyle.position = LabelStyle.Position.End;
        // 清除默认数据
        chart.RemoveData();
        
    }

    

    private void OnAnalysisCompleted()
    {

        showLine = true ;
        UpdateGraphs();
    }

    //更新图表
    public void UpdateGraphs()
    {

        if (dataCollector == null) return;
        var points = dataCollector.GetDataPoints();
        if (points == null || points.Count == 0)
        {
            // 清空图表数据
            ClearChartData(pvChart);
            ClearChartData(pInverseVChart);
            ClearChartData(ptChart);
            ClearChartData(pInverseTChart);
            return;
        }

        // 过滤掉压强、温度或体积值为0的异常数据点
        var validPoints = new List<DataCollector.DataPoint>();
        foreach (var p in points)
        {
            if (p.pressure > 0 && p.temperature > 0 && p.volume > 0.00001f)
            {
                validPoints.Add(p);
            }
        }

        if (validPoints.Count == 0)
        {
            // 清空图表数据
            ClearChartData(pvChart);
            ClearChartData(pInverseVChart);
            ClearChartData(ptChart);
            ClearChartData(pInverseTChart);
            return;
        }

        // 更新图表数据
        UpdatePVChart(pvChart, validPoints);
        UpdatePInverseVChart(pInverseVChart, validPoints);
        UpdatePTChart(ptChart, validPoints);
        UpdatePInverseTChart(pInverseTChart, validPoints);
    }

    //添加数据点（是否绘制线条）
    private void AddDataPoint(LineChart chart)
    {
        // 数据点
        chart.AddSerie<Line>("数据点");
        var scatter = chart.GetSerie(0);
        if (scatter != null)
        {
            scatter.lineStyle.show = false; // 不连线
            scatter.symbol.show = true;
            scatter.symbol.type = SymbolType.Circle;
            scatter.symbol.size = 6f;
        }

        // 曲线
        chart.AddSerie<Line>("曲线");
        var line = chart.GetSerie(1);
        if (line != null)
        {
            line.symbol.show = false;
            line.lineStyle.show = showLine;
            line.lineStyle.width = 2f;
        }
    }

    //清除数据点
    private void ClearChartData(LineChart chart)
    {
        if (chart == null) return;
        chart.RemoveData();
        chart.AddSerie<Line>("数据点");
    }

    #region 更新图表数据
    private void UpdatePVChart(LineChart chart, List<DataCollector.DataPoint> points)
    {
        if (chart == null) return;
        
        chart.RemoveData();

        AddDataPoint(chart);

        points.Sort((a, b) => a.volume.CompareTo(b.volume));

        foreach (var p in points)
        {
            // 数据点
            chart.AddData(0, p.volume, p.pressure);

            // 曲线
            if (showLine)
            {
                chart.AddData(1, p.volume, p.pressure);
            }
        }
    }

    private void UpdatePInverseVChart(LineChart chart, List<DataCollector.DataPoint> points)
    {
        if (chart == null) return;
        
        chart.RemoveData();

        AddDataPoint(chart);

        points.Sort((a, b) => a.inverseVolume.CompareTo(b.inverseVolume));
        foreach (var p in points)
        {
            // 数据点
            chart.AddData(0, p.inverseVolume, p.pressure);

            // 曲线
            if (showLine)
            {
                chart.AddData(1, p.inverseVolume, p.pressure);
            }
        }
        
    }

    private void UpdatePTChart(LineChart chart, List<DataCollector.DataPoint> points)
    {
        if (chart == null) return;
        
        chart.RemoveData();

        AddDataPoint(chart);

        points.Sort((a, b) => a.temperature.CompareTo(b.temperature));

        foreach (var p in points)
        {
            // 数据点
            chart.AddData(0, p.temperature, p.pressure);

            // 曲线
            if (showLine)
            {
                chart.AddData(1, p.temperature, p.pressure);
            }
        }
    }

    private void UpdatePInverseTChart(LineChart chart, List<DataCollector.DataPoint> points)
    {
        if (chart == null) return;
        
        chart.RemoveData();

        AddDataPoint(chart);

        points.Sort((a, b) => a.inverseTempreture.CompareTo(b.inverseTempreture));

        foreach (var p in points)
        {
            // 数据点
            chart.AddData(0, p.inverseTempreture, p.pressure);

            // 曲线
            if (showLine)
            {
                chart.AddData(1, p.inverseTempreture, p.pressure);
            }
        }
    }

    #endregion

    #region 重置图表
    public void ResetGraphs()
    {
        // 重置所有图表
        ResetChart(pvChart);
        ResetChart(pInverseVChart);
        ResetChart(ptChart);
        ResetChart(pInverseTChart);
    }

    private void ResetChart(LineChart chart)
    {
        if (chart == null) return;
        chart.RemoveData();
        AddDataPoint(chart);
        showLine = false;
    }
    #endregion
}