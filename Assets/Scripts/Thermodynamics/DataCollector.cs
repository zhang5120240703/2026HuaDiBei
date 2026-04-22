using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DataCollector : MonoBehaviour
{
    // 引用
    public CylinderController cylinderController;
    public ExperimentStepController experimentStepController;
    // 数据点结构
    public class DataPoint
    {
        public float volume; // 体积 (L)
        public float pressure; // 压力 (kPa)
        public float temperature; // 温度 (K)
        public float pvProduct; // PV乘积
        public float inverseVolume; // 1/V

    }
    
    // 数据记录
    private List<DataPoint> dataPoints = new List<DataPoint>();
    private float lastVolumeChangeTime;
    private const float stabilizationTime = 0.5f; // 稳定时间

    // 自动采集间隔
    private const float autoCollectInterval = 1.0f;
    private float autoCollectTimer = 0f;
    // 控制：达到多少个数据点后开始绘制连线（Inspector 可调）
    public int requiredPointsForLines = 10;
    // 最小采样间距（归一化后）。控制相邻数据点不要过近，取值范围 ~0.02-0.2 之间常用
    [Tooltip("归一化的最小距离阈值（0..1），用于避免采集过于接近的数据点）")]
    public float minNormalizedSpacing = 0.08f;
    // 分析结果
    private float averagePVProduct;
    private float pvStandardDeviation;
    private float maxErrorPercentage;
    
    // 事件
    public System.Action OnDataCollected;
    public System.Action OnAnalysisCompleted;
    
    private void Start()
    {
        // 初始化
        lastVolumeChangeTime = Time.time;
    }
    
    private void Update()
    {
        // 每隔 autoCollectInterval 秒自动采集一次数据点
        autoCollectTimer += Time.deltaTime;
        //检查数据稳定状态
        if (cylinderController.IsVolumeStable() &&
            Time.time - lastVolumeChangeTime > stabilizationTime &&
            dataPoints.Count < requiredPointsForLines && autoCollectTimer >= autoCollectInterval &&
            experimentStepController.GetCurrentStage()==ExperimentStepController.ExperimentStage.DataCollection)
        {
            CollectDataPoint();
            autoCollectTimer = 0f; // 重置自动采集计时器
        }


    }
    
    public void OnVolumeChanged(float newVolume)
    {
        lastVolumeChangeTime = Time.time;
    }


    // 创建数据点
    private DataPoint CreatDataPoint()
    {
        float pressure = IdealGasSimulation.Instance.GetPressure();
        float volume = IdealGasSimulation.Instance.GetVolume();
        float temperature = IdealGasSimulation.Instance.GetTemperature();

        DataPoint point = new DataPoint
        {
            volume = volume,
            pressure = pressure,
            temperature = temperature,
            pvProduct = pressure * volume,
            inverseVolume = 1.0f / Mathf.Max(volume, 1e-6f)
        };
        // 手动检查是否存在相同数据点
        bool exists = dataPoints.Any(p =>
            Mathf.Approximately(p.volume, point.volume) &&
            Mathf.Approximately(p.pressure, point.pressure) &&
            Mathf.Approximately(p.temperature, point.temperature));

        if (exists)
        {
            return null;
        }
        // 如果与已有点过于接近（归一化空间），则忽略以保证点分散
        if (IsTooClose(point))
        {
            return null;
        }


        return point;
    }


    // 判断 candidate 是否与已有点太接近（归一化体积/压力空间的欧氏距离）
    private bool IsTooClose(DataPoint candidate)
    {
        if (dataPoints == null || dataPoints.Count == 0) return false;

        var sim = IdealGasSimulation.Instance;
        if (sim == null) return false;

        float minV = sim.GetMinVolume();
        float maxV = sim.GetMaxVolume();
        float minP = sim.GetMinPressure();
        float maxP = sim.GetMaxPressure();

        // 防止除以 0
        float vRange = Mathf.Max(1e-6f, maxV - minV);
        float pRange = Mathf.Max(1e-6f, maxP - minP);

        foreach (var p in dataPoints)
        {
            float nx = Mathf.Abs(candidate.volume - p.volume) / vRange;
            float ny = Mathf.Abs(candidate.pressure - p.pressure) / pRange;
            float dist = Mathf.Sqrt(nx * nx + ny * ny);
            if (dist < minNormalizedSpacing)
                return true;
        }

        return false;
    }
    //自动采集数据
    public void CollectDataPoint()
    {

        DataPoint point=CreatDataPoint();
        
        if(point ==null) return;
        // 添加到数据列表
        dataPoints.Add(point);
        
        // 触发事件
        OnDataCollected?.Invoke();
        
        // 检查是否需要分析
        if (dataPoints.Count >= 20)
        {
            AnalyzeData();
        }
    }

    private void AnalyzeData()
    {
        if (dataPoints.Count == 0) return;
        
        // 计算PV乘积的平均值和标准差
        float sumPV = 0;
        float sumPVSquared = 0;
        float sumInverseVolume = 0;
        float sumPressure = 0;
        
        foreach (var point in dataPoints)
        {
            sumPV += point.pvProduct;
            sumPVSquared += point.pvProduct * point.pvProduct;
            sumInverseVolume += point.inverseVolume;
            sumPressure += point.pressure;
        }
        
        averagePVProduct = sumPV / dataPoints.Count;
        float variance = (sumPVSquared / dataPoints.Count) - (averagePVProduct * averagePVProduct);
        pvStandardDeviation = Mathf.Sqrt(variance);
        
        // 计算最大误差
        maxErrorPercentage = 0;
        foreach (var point in dataPoints)
        {
            float error = Mathf.Abs((point.pvProduct - averagePVProduct) / averagePVProduct) * 100f;
            if (error > maxErrorPercentage)
            {
                maxErrorPercentage = error;
            }
        }
        
        // 触发分析完成事件
        OnAnalysisCompleted?.Invoke();
    }


    // 重置数据
    public void ResetData()
    {
        dataPoints.Clear();
        lastVolumeChangeTime = Time.time;
        averagePVProduct = 0;
        pvStandardDeviation = 0;
        maxErrorPercentage = 0;
    }



    #region 检查是否验证实验
    // 检查是否验证了玻意耳定律
    public bool IsBoyleLawVerified()
    {
        return maxErrorPercentage < 3.0f; // 误差控制在3%以内
    }
    
    // 检查是否验证了盖-吕萨克定律
    public bool IsCharlesLawVerified()
    {
        if (dataPoints.Count < 2) return false;
        
        // 计算V/T比值的平均值
        float sumVT = 0;
        foreach (var point in dataPoints)
        {
            sumVT += point.volume / point.temperature;
        }
        float averageVT = sumVT / dataPoints.Count;
        
        // 检查误差
        float maxError = 0;
        foreach (var point in dataPoints)
        {
            float error = Mathf.Abs((point.volume / point.temperature - averageVT) / averageVT) * 100f;
            if (error > maxError)
            {
                maxError = error;
            }
        }
        
        return maxError < 3.0f;
    }
    
    // 检查是否验证了查理定律
    public bool IsGayLussacLawVerified()
    {
        if (dataPoints.Count < 2) return false;
        
        // 计算P/T比值的平均值
        float sumPT = 0;
        foreach (var point in dataPoints)
        {
            sumPT += point.pressure / point.temperature;
        }
        float averagePT = sumPT / dataPoints.Count;
        
        // 检查误差
        float maxError = 0;
        foreach (var point in dataPoints)
        {
            float error = Mathf.Abs((point.pressure / point.temperature - averagePT) / averagePT) * 100f;
            if (error > maxError)
            {
                maxError = error;
            }
        }
        
        return maxError < 3.0f;
    }

    #endregion

    #region 数据获取接口
    // 获取数据点
    public List<DataPoint> GetDataPoints()
    {
        return dataPoints;
    }
    
    // 获取分析结果
    public float GetAveragePVProduct()
    {
        return averagePVProduct;
    }
    
    public float GetPVStandardDeviation()
    {
        return pvStandardDeviation;
    }
    
    public float GetMaxErrorPercentage()
    {
        return maxErrorPercentage;
    }
    
    // 获取数据点数量
    public int GetDataPointCount()
    {
        return dataPoints.Count;
    }

    public int GetRequiredPointsForLines()
    {
        return requiredPointsForLines;
    }

    #endregion
}