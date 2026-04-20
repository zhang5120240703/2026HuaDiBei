using System;

/// <summary>
/// 实验流程控制器（核心：步骤跳转 + 条件校验）

/// </summary>
public class ExperimentFlowController
{
    // 当前步骤
    public ExperimentStep CurrentStep { get; private set; }

    // 步骤跳转完成事件（外部UI/系统监听）
    public event Action<ExperimentStep> OnStepChanged;

    // 流程错误事件（条件不满足）
    public event Action<string> OnFlowError;

    public ExperimentFlowController()
    {
        // 初始进入第一步
        CurrentStep = ExperimentStep.Step1_Prepare;
    }

    #region 核心：步骤跳转（带条件判断）
    /// <summary>
    /// 跳转到指定步骤（带条件校验）
    /// </summary>
    public bool JumpToStep(ExperimentStep targetStep)
    {
        // 条件1：不能跳转到无效步骤
        if (!IsValidStep(targetStep))
        {
            OnFlowError?.Invoke("目标步骤无效");
            return false;
        }

        // 条件2：当前步骤 → 目标步骤 是否允许跳转
        if (!CanJumpFromCurrentToTarget(CurrentStep, targetStep))
        {
            OnFlowError?.Invoke($"无法从 {CurrentStep} 跳转到 {targetStep}");
            return false;
        }

        // 条件3：目标步骤的前置条件是否满足
        if (!CheckStepPreCondition(targetStep))
        {
            OnFlowError?.Invoke($"步骤 {targetStep} 前置条件不满足");
            return false;
        }

        // 所有条件通过 → 执行跳转
        CurrentStep = targetStep;
        OnStepChanged?.Invoke(CurrentStep);
        return true;
    }

    /// <summary>
    /// 下一步
    /// </summary>
    public bool NextStep()
    {
        return JumpToStep(CurrentStep + 1);
    }

    /// <summary>
    /// 上一步
    /// </summary>
    public bool PrevStep()
    {
        return JumpToStep(CurrentStep - 1);
    }
    #endregion

    #region 条件判断逻辑（可拓展）
    private bool IsValidStep(ExperimentStep step) => step >= ExperimentStep.Step1_Prepare && step <= ExperimentStep.Step5_Finish;

    /// <summary>
    /// 步骤跳转规则（核心业务逻辑）
    /// </summary>
    private bool CanJumpFromCurrentToTarget(ExperimentStep from, ExperimentStep to)
    {
        // 允许自由回退
        if (to < from) return true;

        // 前进必须连续
        return to == from + 1;
    }

    /// <summary>
    /// 每个步骤的前置条件判断
    /// </summary>
    private bool CheckStepPreCondition(ExperimentStep step)
    {
        return step switch
        {
            ExperimentStep.Step3_RunSim => ExperimentStateManager.Instance.IsParamValid, // 参数必须合法
            ExperimentStep.Step4_Observe => ExperimentStateManager.Instance.CurrentRunState == ExperimentRunState.Running, // 必须运行过
            ExperimentStep.Step5_Finish => ExperimentStateManager.Instance.CurrentRunState == ExperimentRunState.Finished,
            _ => true
        };
    }
    #endregion

    /// <summary>
    /// 重置整个实验流程
    /// </summary>
    public void ResetFlow()
    {
        CurrentStep = ExperimentStep.Step1_Prepare;
        OnStepChanged?.Invoke(CurrentStep);
    }
}
