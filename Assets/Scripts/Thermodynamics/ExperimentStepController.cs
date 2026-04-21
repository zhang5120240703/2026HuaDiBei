using UnityEngine;

public class ExperimentStepController : MonoBehaviour
{
    // 引用
    public UIManager uiManager;
    public DataCollector dataCollector;
    public CylinderController cylinderController;
    public UIPanel uiPanel;
    // 实验步骤跳转标志
    private bool isSelectExp = false;
    private bool isStart=false;
    private bool isReset=false;
    private bool isConfirm=false;

    // 实验阶段
    public enum ExperimentStage
    {
        Preparation, // 准备阶段
        Confirmation, // 确认实验过程
        DataCollection, // 数据采集
        DataAnalysis, // 数据分析 
        Conclusion // 结论总结
    }
    // 手动确认计数（数据采集时用户需要确认三次）
    private const int requiredConfirms = 3;
    private int confirmCount = 0;

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
        string stageName = "";
        
        switch (stage)
        {
            case ExperimentStage.Preparation:
                step = 1;
                stageName = "准备阶段";
                break;
            case ExperimentStage.Confirmation:
                step = 2;
                stageName = "确认实验过程";
                break;
            case ExperimentStage.DataCollection:
                step = 3;
                stageName = "数据采集";
                break;
            case ExperimentStage.DataAnalysis:
                step = 4;
                stageName = "数据分析";
                break;
            case ExperimentStage.Conclusion:
                step = 5;
                stageName = "结论总结";
                break;
        }
        
        uiManager.SetStep(step);
        Debug.Log("进入" + stageName);
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
                if(isStart)
                    SetStage(ExperimentStage.DataCollection);
                break;
                
            case ExperimentStage.DataCollection:
                // 数据采集阶段：检查是否采集了足够的数据点
                if (dataCollector.GetDataPointCount() >= 3)
                {
                    SetStage(ExperimentStage.DataAnalysis);
                }
                break;
                
            case ExperimentStage.DataAnalysis:
                // 数据分析阶段：检查是否完成了分析
                // 当数据采集完成时自动进入结论总结阶段
                

                break;
                
            case ExperimentStage.Conclusion:
                // 结论总结阶段：等待用户查看结果
                break;
        }
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
            uiPanel.ShowError("请先开始实验过!");
            return;
        }
        SetStage(ExperimentStage.Preparation);
        IdealGasSimulation.Instance.Initialization();
        cylinderController.SetPistonPosition(IdealGasSimulation.Instance.GetVolume());
        SetProcess(3);
        StopDataCollectionMode();
        dataCollector.ResetData();
        uiManager.ResetUI();
        isReset = true;
        isStart = false;
        isSelectExp = false;
    }

    // 确认参数(按钮调用)
    public void ConfirmParameter()
    {
        if (currentStage != ExperimentStage.DataCollection)
        {
            uiPanel.ShowError("当前不在数据采集阶段，无法确认参数。");
            return;
        }

        dataCollector.AddDataPoint();
        confirmCount++;
        // 更新 UI 提示
        if (uiPanel != null && uiPanel.statusText != null)
            uiPanel.statusText.text = $"已确认 {confirmCount}/{requiredConfirms} 次参数。";

        // 当达到所需确认次数时，停止手动模式并进入下一阶段（数据分析）
        if (confirmCount >= requiredConfirms)
        {
            // 停止手动采集
            StopDataCollectionMode();

        }
    }
    // 切换实验过程
    public void SetProcess(int process)
    {
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
    public bool IsOperationCorrect()
    {
        switch (currentStage)
        {
            case ExperimentStage.DataCollection:
                // 检查是否在合理的体积范围内操作
                float currentVolume = IdealGasSimulation.Instance.GetVolume();
                return currentVolume >= IdealGasSimulation.Instance.GetMinVolume() && currentVolume <= IdealGasSimulation.Instance.GetMaxVolume();
            default:
                return true;
        }
    }

    // 停止数据采集模式
    private void StopDataCollectionMode()
    {

        // 重置计数与提示
        confirmCount = 0;
        if (uiPanel != null && uiPanel.statusText != null)
            uiPanel.statusText.text = "数据采集已停止";
    }

    // 获取当前阶段的操作指引
    //public string GetStageInstruction()
    //{  
    //    switch (currentStage)
    //    {
    //        case ExperimentStage.Preparation:
    //            return "欢迎来到理想气体状态方程实验！请选择实验过程。";
    //        case ExperimentStage.ParameterSetup:
    //            return "请设置实验温度，然后点击开始按钮。";
    //        case ExperimentStage.DataCollection:
    //            return "请移动活塞改变气体体积，系统会自动记录稳定的数据点。";
    //        case ExperimentStage.DataAnalysis:
    //            return "系统正在分析数据，请稍候...";
    //        case ExperimentStage.Conclusion:
    //            return "实验完成！请查看分析结果。";
    //        default:
    //            return "";
    //    }
    //}
}