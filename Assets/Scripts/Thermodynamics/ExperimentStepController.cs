using UnityEngine;

public class ExperimentStepController : MonoBehaviour
{
    // 引用
    public UIManager uiManager;
    public DataCollector dataCollector;
    public CylinderController cylinderController;
    public IdealGasSimulation gasSimulation;
    
    // 实验阶段
    public enum ExperimentStage
    {
        Preparation, // 准备阶段
        ParameterSetup, // 参数设置
        DataCollection, // 数据采集
        DataAnalysis, // 数据分析
        Conclusion // 结论总结
    }
    

    private ExperimentStage currentStage = ExperimentStage.Preparation;// 当前实验阶段
    private float stageStartTime;// 当前阶段开始时间
    private const float stageTimeout = 60.0f; // 每个阶段的超时时间
    
    private void Start()
    {
        // 初始化实验阶段
        SetStage(ExperimentStage.Preparation);
    }
    
    private void Update()
    {
        // 检查阶段超时
        if (Time.time - stageStartTime > stageTimeout)
        {
            HandleStageTimeout();
        }
        
        // 检查阶段完成条件
        CheckStageCompletion();
    }
    
    private void SetStage(ExperimentStage stage)
    {
        currentStage = stage;
        stageStartTime = Time.time;
        
        // 更新UI显示
        int step = 0;
        string stageName = "";
        
        switch (stage)
        {
            case ExperimentStage.Preparation:
                step = 1;
                stageName = "准备阶段";
                break;
            case ExperimentStage.ParameterSetup:
                step = 2;
                stageName = "参数设置";
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
                // 准备阶段：检查是否选择了实验过程
                // 自动进入参数设置阶段
                if (Time.time - stageStartTime > 2.0f) // 给用户2秒时间查看
                {
                    SetStage(ExperimentStage.ParameterSetup);
                }
                break;
                
            case ExperimentStage.ParameterSetup:
                // 参数设置阶段：检查是否设置了温度
                // 当用户点击开始按钮时进入数据采集阶段
                // 这里通过UI按钮事件处理
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
                if (dataCollector.GetDataPointCount() >= 3)
                {
                    SetStage(ExperimentStage.Conclusion);
                }
                break;
                
            case ExperimentStage.Conclusion:
                // 结论总结阶段：等待用户查看结果
                // 用户可以选择返回主菜单
                break;
        }
    }
    
    private void HandleStageTimeout()
    {
        Debug.LogWarning("阶段超时: " + currentStage.ToString());
        // 可以添加超时处理逻辑，例如提示用户或自动进入下一阶段
    }
    
    // 开始实验(按钮调用)
    public void StartExperiment()
    {
        SetStage(ExperimentStage.DataCollection);
        uiManager.StartExperiment();
    }

    // 重置实验(按钮调用)
    public void ResetExperiment()
    {
        SetStage(ExperimentStage.Preparation);
        uiManager.ResetUI();
    }
    
    // 切换实验过程
    public void SetProcess(IdealGasSimulation.ProcessType process)
    {
        gasSimulation.SetProcess(process);
        ResetExperiment();
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
                float currentVolume = gasSimulation.GetVolume();
                return currentVolume >= gasSimulation.GetMinVolume() && currentVolume <= gasSimulation.GetMaxVolume();
            default:
                return true;
        }
    }
    
    // 获取当前阶段的操作指引
    public string GetStageInstruction()
    {  
        switch (currentStage)
        {
            case ExperimentStage.Preparation:
                return "欢迎来到理想气体状态方程实验！请选择实验过程。";
            case ExperimentStage.ParameterSetup:
                return "请设置实验温度，然后点击开始按钮。";
            case ExperimentStage.DataCollection:
                return "请移动活塞改变气体体积，系统会自动记录稳定的数据点。";
            case ExperimentStage.DataAnalysis:
                return "系统正在分析数据，请稍候...";
            case ExperimentStage.Conclusion:
                return "实验完成！请查看分析结果。";
            default:
                return "";
        }
    }
}