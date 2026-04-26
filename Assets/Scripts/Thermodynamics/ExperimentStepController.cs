using System.Collections;
using UnityEngine;

public class ExperimentStepController : MonoBehaviour
{
    // 引用
    public UI_Manager uiManager;
    public DataCollector dataCollector;
    public CylinderController cylinderController;
    public UIPanel uiPanel;
    // 实验步骤跳转标志
    private bool isSelectExp = false;
    private bool isStart=false;
    private bool isReset=false;

    // 实验阶段
    public enum ExperimentStage
    {
        Preparation, // 准备阶段
        Confirmation, // 确认实验过程
        DataCollection, // 数据采集
        DataAnalysis, // 数据分析 
    }

    private ExperimentStage currentStage = ExperimentStage.Preparation;// 当前实验阶段
    
    private void Start()
    {
        // 初始化实验阶段
        SetStage(ExperimentStage.Preparation);
    }
    
    private void Update()
    {
        // 检查阶段完成条件
        CheckStageCompletion();
    }
    
    private void SetStage(ExperimentStage stage)
    {
        currentStage = stage;
        
        // 更新UI显示
        int step = 0;
        
        switch (stage)
        {
            case ExperimentStage.Preparation:
                step = 1;
                break;
            case ExperimentStage.Confirmation:
                step = 2;
                break;
            case ExperimentStage.DataCollection:
                step = 3;
                break;
            case ExperimentStage.DataAnalysis:
                step = 4;
                break;
        }
        
        uiManager.SetStep(step);
    }
    
    private void CheckStageCompletion()
    {
        switch (currentStage)
        {
            case ExperimentStage.Preparation:
                // 检查是否选择了实验过程
                if(isSelectExp)
                    SetStage(ExperimentStage.Confirmation);
                break;
                
            case ExperimentStage.Confirmation:
                // 确认实验过程阶段：检查是否确认了实验过程
                // 当用户点击确认按钮时进入数据采集阶段
                if (isStart)
                {
                    StartCoroutine(DelayEnterDataCollection());
                }
                break;
                
            case ExperimentStage.DataCollection:
                // 数据采集阶段：检查是否采集了足够的数据点
                if (dataCollector.GetDataPointCount() >= dataCollector.GetRequiredPointsForLines()&&dataCollector.GetIsConfirm())
                {
                    SetStage(ExperimentStage.DataAnalysis);
                }
                break;
                
            case ExperimentStage.DataAnalysis:
                // 数据分析阶段：检查是否完成了分析
                break;
                
        }
    }
    
    IEnumerator DelayEnterDataCollection()
    {
        yield return new WaitForSeconds(3.0f);
        SetStage(ExperimentStage.DataCollection);
        uiPanel.UpdateDataDisplay();
    }

    // 开始实验(按钮调用)
    public void StartExperiment()
    {
        if (!isSelectExp)
        {
            uiPanel.ShowError("请先选择实验过程!");
            return;
        }
        uiManager.StartExperiment();
        isStart = true;
        isReset = false;
    }

    // 重置实验(按钮调用)
    public void ResetExperiment()
    {
        if (!isStart)
        {
            uiPanel.ShowError("请先开始实验!");
            return;
        }
        SetStage(ExperimentStage.Preparation);
        IdealGasSimulation.Instance.Initialization();
        cylinderController.SetPistonPosition(IdealGasSimulation.Instance.GetVolume());
        SetProcess(3);
        dataCollector.ResetData();
        uiManager.ResetUI();
        isReset = true;
        isStart = false;
        isSelectExp = false;
    }

    

    // 切换实验过程(按钮调用)
    public void SetProcess(int process)
    {
        if(isSelectExp)
        {
            uiPanel.ShowError("请先重置实验!");
        }
        IdealGasSimulation.Instance.SetProcess((IdealGasSimulation.ProcessType)process);
        cylinderController.SetCurrentProcess((IdealGasSimulation.ProcessType)process);
        uiManager.SetProcess((IdealGasSimulation.ProcessType)process);
        isSelectExp = true; 
    }
    

    // 获取当前阶段
    public ExperimentStage GetCurrentStage()
    {
        return currentStage;
    }
    
    // 检查操作是否正确
    //public bool IsOperationCorrect()
    //{
    //    switch (currentStage)
    //    {
    //        case ExperimentStage.DataCollection:
    //            // 检查是否在合理的体积范围内操作
    //            float currentVolume = IdealGasSimulation.Instance.GetVolume();
    //            return currentVolume >= IdealGasSimulation.Instance.GetMinVolume() && currentVolume <= IdealGasSimulation.Instance.GetMaxVolume();
    //        default:
    //            return true;
    //    }
    //}


}