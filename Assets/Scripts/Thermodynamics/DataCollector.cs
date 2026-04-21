using UnityEngine;
using System.Collections.Generic;

public class DataCollector : MonoBehaviour
{
    // 引用
    public CylinderController cylinderController;
    
    // 数据点结构
    public struct DataPoint
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
    private const float stabilizationTime = 2.0f; // 稳定时间
    
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
        // 检查数据稳定状态
        //if (cylinderController.IsVolumeStable() &&
        //    Time.time - lastVolumeChangeTime > stabilizationTime &&
        //    dataPoints.Count < 5)
        //{
        //    CollectDataPoint();
        //}


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

        return point;
    }
     
    //采集数据
    public void CollectDataPoint()
    {
        DataPoint point=CreatDataPoint();
        
        // 添加到数据列表
        dataPoints.Add(point);
        
        // 触发事件
        OnDataCollected?.Invoke();
        
        // 检查是否需要分析
        if (dataPoints.Count >= 3)
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


    // 检查是否需要更多数据
    public bool NeedMoreData()
    {
        return dataPoints.Count < 3;
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

    #endregion
}