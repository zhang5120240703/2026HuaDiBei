using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static IdealGasSimulation;

public  class GasSimulationAIInterface : MonoBehaviour
{
    public static GasSimulationAIInterface Instance;

    public DataCollector dataCollector; // 数据收集器
    public IdealGasSimulation gasSimulation; // 气体模拟器
    public ExperimentStepController experimentController; // 实验步骤控制器


    private void Awake()
    {
        if(Instance!=null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }

    // 获取实验参数接口
    public float GetPressure() { return gasSimulation.GetPressure(); }// 获取当前压力
    public float GetVolume() { return gasSimulation.GetVolume(); }// 获取当前体积
    public float GetTemperature() { return gasSimulation.GetTemperature(); }// 获取当前温度
    public float GetPVProduct() { return gasSimulation.GetPVProduct(); }// 获取当前压强体积乘积
    public float GetMinVolume() { return gasSimulation.GetMinVolume(); }// 获取最小体积
    public float GetMaxVolume() { return gasSimulation.GetMaxVolume(); }// 获取最大体积
    public float GetMinTemperature() { return gasSimulation.GetMinTemperature(); }// 获取最小温度
    public float GetMaxTemperature() { return gasSimulation.GetMaxTemperature(); }// 获取最大温度
    public float GetMinPressure() { return gasSimulation.GetMinPressure(); }// 获取最小压力
    public float GetMaxPressure() { return gasSimulation.GetMaxPressure(); }// 获取最大压力
    public ProcessType GetCurrentProcess() { return gasSimulation.GetCurrentProcess(); }// 获取当前过程
    public List<DataCollector.DataPoint> GetGraphDataPoint(){ return dataCollector.GetDataPoints(); }// 获取图表数据点列表
    public float GetPVAverageErrorPercentage(){ return dataCollector.GetAverageErrorPercentage(); }// 获取PV乘积平均误差百分比
    public float GetVTAverageErrorPercentage(){ return dataCollector.GetVTAverageErrorPercentage(); }// 获取VT乘积平均误差百分比
    public float GetPTAverageErrorPercentage(){ return dataCollector.GetPTAverageErrorPercentage(); }// 获取PT乘积平均误差百分比

    //获取当前实验阶段
    public ExperimentStepController.ExperimentStage GetCurrentExperimentStep() { return experimentController.GetCurrentStage(); }// 获取当前实验阶段
}
