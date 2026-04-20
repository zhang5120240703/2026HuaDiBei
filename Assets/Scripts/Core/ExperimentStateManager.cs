using System;
using UnityEngine;

/// <summary>
/// 全局状态管理器（单例）
/// 控制：运行/暂停/重置/参数合法性
/// </summary>
public class ExperimentStateManager : MonoBehaviour
{
    public static ExperimentStateManager Instance { get; private set; }

    // 当前运行状态
    public ExperimentRunState CurrentRunState { get; private set; }

    // 参数是否合法（外部可写）
    public bool IsParamValid { get; set; }

    // 状态变更事件
    public event Action<ExperimentRunState> OnRunStateChanged;

    private void Awake()
    {
        // 单例保证全局唯一
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }

        CurrentRunState = ExperimentRunState.Idle;
        IsParamValid = false; // 默认参数不合法
    }

    #region 状态控制方法
    public void StartExperiment()
    {
        if (CurrentRunState != ExperimentRunState.Idle) return;
        CurrentRunState = ExperimentRunState.Running;
        OnRunStateChanged?.Invoke(CurrentRunState);
    }

    public void PauseExperiment()
    {
        if (CurrentRunState != ExperimentRunState.Running) return;
        CurrentRunState = ExperimentRunState.Paused;
        OnRunStateChanged?.Invoke(CurrentRunState);
    }

    public void ResetExperiment()
    {
        CurrentRunState = ExperimentRunState.Idle;
        IsParamValid = false;
        OnRunStateChanged?.Invoke(CurrentRunState);
    }

    public void FinishExperiment()
    {
        CurrentRunState = ExperimentRunState.Finished;
        OnRunStateChanged?.Invoke(CurrentRunState);
    }
    #endregion
}
