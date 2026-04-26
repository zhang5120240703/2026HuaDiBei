using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static ExperimentStepController;
using static IdealGasSimulation;

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
        public float inverseTempreture;// 1/T
    }
    
    // 数据记录
    private List<DataPoint> dataPoints = new List<DataPoint>();
    private float lastVolumeChangeTime;
    private const float stabilizationTime = 0.5f; // 稳定时间

    // 自动采集间隔
    private const float autoCollectInterval = 1.0f;
    private float autoCollectTimer = 0f;
    // 控制：达到多少个数据点后开始绘制连线（Inspector 可调）
    private int requiredPointsForLines = 8;
    // 最小采样间距（归一化后）。控制相邻数据点不要过近，取值范围 ~0.02-0.2 之间常用
    [Tooltip("归一化的最小距离阈值（0..1），用于避免采集过于接近的数据点）")]
    private float minNormalizedSpacing = 0.015f;
    // 分析结果
    private float averagePVProduct;
    private float pvStandardDeviation;
    private float PVAverageErrorPercentage;//PV乘积平均误差百分比
    private float VTAverageErrorPercentage;//V/T平均误差百分比
    private float PTAverageErrorPercentage;//P/T平均误差百分比
    private bool isConfirm=false;

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

        NeedAnalyzData();
    }

    // 确认参数(按钮调用)
    public void ConfirmParameter()
    {
        if (isConfirm)
        {
            return;
        }
        ExperimentStepController.ExperimentStage currentStage= experimentStepController.GetCurrentStage();
        if (currentStage != ExperimentStage.DataCollection)
        {
            return;
        }
        isConfirm = true;


    }

    public void OnVolumeChanged(float newVolume)
    {
        lastVolumeChangeTime = Time.time;
    }



    #region 创建数据点
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
            inverseVolume = 1.0f / Mathf.Max(volume, 1e-6f),
            inverseTempreture = 1.0f / Mathf.Max(temperature, 1e-6f)
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

        var last = dataPoints[dataPoints.Count - 1];

        float minV = sim.GetMinVolume();
        float maxV = sim.GetMaxVolume();
        float minP = sim.GetMinPressure();
        float maxP = sim.GetMaxPressure();

        float vRange = Mathf.Max(1e-6f, maxV - minV);
        float pRange = Mathf.Max(1e-6f, maxP - minP);

        float dist = 0f;

        ProcessType type = IdealGasSimulation.Instance.GetCurrentProcess();
        foreach (var p in dataPoints)
        {

            switch (type)
            {
                case ProcessType.Isochoric: // 等容 👉 只看压力
                    dist = Mathf.Abs(candidate.pressure - p.pressure) / pRange;
                    break;

                case ProcessType.Isothermal: // 等温 👉 看 V + P
                    float nx = Mathf.Abs(candidate.volume - p.volume) / vRange;
                    float ny = Mathf.Abs(candidate.pressure - p.pressure) / pRange;
                    dist = Mathf.Sqrt(nx * nx + ny * ny);
                    break;

                case ProcessType.Isobaric: // 等压 👉 看体积
                    dist = Mathf.Abs(candidate.volume - p.volume) / vRange;
                    break;
            }

        }

        return dist < minNormalizedSpacing;
    }
    #endregion

    //自动采集数据
    public void CollectDataPoint()
    {

        DataPoint point=CreatDataPoint();
        
        if(point ==null) return;
        // 添加到数据列表
        dataPoints.Add(point);
        
        // 触发事件
        OnDataCollected?.Invoke();
        
        
    }

    private void NeedAnalyzData()
    {
        if(experimentStepController.GetCurrentStage()!=ExperimentStage.DataAnalysis)
            return; 
        // 检查是否需要分析(需按下确认按钮)
        if (dataPoints.Count >= requiredPointsForLines && isConfirm)
        {
            AnalyzeData();
            isConfirm = false;
        }
    }

    //分析数据
    private void AnalyzeData()
    {
        if (dataPoints.Count == 0) return;
        
        // 计算PV乘积的平均值和标准差
        float sumPV = 0;
        float sumPVSquared = 0;
        float sumInverseVolume = 0;
        float sumPressure = 0;
        float sumInverseTempreture = 0;
        foreach (var point in dataPoints)
        {
            sumPV += point.pvProduct;
            sumPVSquared += point.pvProduct * point.pvProduct;
            sumInverseVolume += point.inverseVolume;
            sumPressure += point.pressure;
            sumInverseTempreture += point.inverseTempreture;
        }
        
        averagePVProduct = sumPV / dataPoints.Count;
        float variance = (sumPVSquared / dataPoints.Count) - (averagePVProduct * averagePVProduct);
        pvStandardDeviation = Mathf.Sqrt(variance);

        // 计算平均误差
        PVAverageErrorPercentage = 0;
        float totalError = 0;
        foreach (var point in dataPoints)
        {
             totalError=totalError+ Mathf.Abs((point.pvProduct - averagePVProduct) / averagePVProduct) * 100f;
        }

        PVAverageErrorPercentage = totalError / dataPoints.Count;
        
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
        PVAverageErrorPercentage = 0;
    }



    #region 检查是否验证实验
    // 检查是否验证了玻意耳定律
    public bool IsBoyleLawVerified()
    {
        return PVAverageErrorPercentage < 3.0f; // 误差控制在3%以内
    }
    
    // 检查是否验证了盖-吕萨克定律(等压)
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
        float sumError = 0;
        float averageError = 0;
        foreach (var point in dataPoints)
        {
            sumError = Mathf.Abs((point.volume / point.temperature - averageVT) / averageVT) * 100f;

        }
        averageError = sumError / dataPoints.Count;
        VTAverageErrorPercentage = averageError;
        return averageError < 3.0f;
    }
    
    // 检查是否验证了查理定律（等容）
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
        float sumError = 0;
        float averageError = 0;
        foreach (var point in dataPoints)
        {
            sumError = Mathf.Abs((point.pressure / point.temperature - averagePT) / averagePT) * 100f;

        }
        averageError= sumError / dataPoints.Count;
        PTAverageErrorPercentage = averageError;
        return averageError < 3.0f;
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
    
    public float GetAverageErrorPercentage()
    {
        return PVAverageErrorPercentage;
    }


    public float GetVTAverageErrorPercentage()
    {
        return VTAverageErrorPercentage;
    }

    public float GetPTAverageErrorPercentage()
    {
        return PTAverageErrorPercentage;
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

    public bool GetIsConfirm()
    {
        return isConfirm;
    }

    #endregion
}