using System;
using UnityEngine;

/// <summary>
/// 用户操作事件系统
/// 捕捉所有用户输入 → 触发对应交互逻辑

/// </summary>
public class UserActionManager
{
    public static UserActionManager Instance { get; private set; }

    // 用户操作触发事件（全局可监听）
    public event Action<UserActionType> OnUserActionPerformed;

    private readonly ExperimentFlowController _flowCtrl;
    private readonly ExperimentStateManager _stateCtrl;

    public UserActionManager()
    {
        Instance = this;
        _flowCtrl = new ExperimentFlowController();
        _stateCtrl = ExperimentStateManager.Instance;
    }

    #region 捕捉用户操作（所有交互入口）
    /// <summary>
    /// 外部调用：用户触发了某个操作
    /// </summary>
    public void CaptureUserAction(UserActionType actionType)
    {
        // 记录操作
        OnUserActionPerformed?.Invoke(actionType);

        // 执行交互逻辑
        HandleUserAction(actionType);
    }
    #endregion

    #region 核心交互逻辑处理
    private void HandleUserAction(UserActionType actionType)
    {
        var stateCtrl = ExperimentStateManager.Instance;
        if (stateCtrl == null)
        {
            UnityEngine.Debug.LogError("[UserActionManager] ExperimentStateManager.Instance is null! 操作被忽略。");
            return;
        }
        switch (actionType)
        {
            case UserActionType.StartExperiment:
                stateCtrl.StartExperiment();
                break;
            case UserActionType.PauseExperiment:
                stateCtrl.PauseExperiment();
                break;
            case UserActionType.ResetExperiment:
                stateCtrl.ResetExperiment();
                _flowCtrl.ResetFlow();
                break;
            case UserActionType.JumpToNextStep:
                _flowCtrl.NextStep();
                break;
                case UserActionType.JumpToPrevStep:
                _flowCtrl.PrevStep();
                break;
            case UserActionType.ConfirmParam:
                stateCtrl.IsParamValid = true;
                break;
        }
    }
    #endregion

    /// <summary>
    /// 获取流程控制器（给外部使用）
    /// </summary>
    public ExperimentFlowController GetFlowController() => _flowCtrl;


}