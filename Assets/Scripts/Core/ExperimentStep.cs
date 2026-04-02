// 实验步骤枚举
public enum ExperimentStep
{
    Step1_Prepare,    // 准备阶段
    Step2_SetParam,   // 参数设置
    Step3_RunSim,     // 运行仿真
    Step4_Observe,    // 观察数据
    Step5_Finish      // 实验完成
}

// 实验运行状态
public enum ExperimentRunState
{
    Idle,      // 空闲
    Running,   // 运行中
    Paused,    // 暂停
    Finished   // 完成
}

// 用户操作类型（所有可捕捉的操作）
public enum UserActionType
{
    StartExperiment,//0
    PauseExperiment,//1
    ResetExperiment,//2
    JumpToNextStep,//3
    JumpToPrevStep,//4
    ModifyParameter,//5
    ConfirmParam//6
}