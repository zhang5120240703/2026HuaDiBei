using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单摆实验步骤类型定义
/// </summary>
public enum PendulumExperimentStep
{
    None = 0,               // 初始状态
    PrepareEquipment = 1,   // 1. 准备实验器材（单摆、刻度尺、秒表等）
    AdjustLength = 2,       // 2. 调整单摆摆长
    MeasureAngle = 3,       // 3. 测量摆角（小于5°）
    ReleasePendulum = 4,    // 4. 释放单摆（静止释放）
    MeasureTime = 5,        // 5. 测量摆动周期（多次测量取平均）
    ChangeParameter = 6,    // 6. 更换摆长/摆球质量，重复实验
    DataAnalysis = 7,       // 7. 数据分析，推导周期公式
    ExperimentComplete = 8  // 8. 实验完成
}

/// <summary>
/// 单摆实验步骤管理器（提供AI接口）
/// </summary>
public class PendulumStepManager : MonoBehaviour
{
    // 单例实例（方便AI模块全局调用）
    private static PendulumStepManager _instance;
    public static PendulumStepManager Instance => _instance;

    // 当前实验步骤
    private PendulumExperimentStep _currentStep = PendulumExperimentStep.None;
    // 实验步骤执行记录
    private List<PendulumExperimentStep> _stepHistory = new List<PendulumExperimentStep>();

    // 步骤变更事件（AI可监听）
    public event Action<PendulumExperimentStep> OnStepChanged;

    private void Awake()
    {
        // 单例初始化
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留
        }
        else
        {
            Destroy(gameObject);
        }

        // 初始化第一步
        SetInitialStep();
    }

    /// <summary>
    /// 初始化实验初始步骤
    /// </summary>
    private void SetInitialStep()
    {
        _currentStep = PendulumExperimentStep.PrepareEquipment;
        _stepHistory.Add(_currentStep);
        OnStepChanged?.Invoke(_currentStep);
    }

    #region AI 调用接口（核心）
    /// <summary>
    /// 【AI接口】获取当前实验步骤（枚举值）
    /// </summary>
    /// <returns>当前步骤枚举</returns>
    public PendulumExperimentStep GetCurrentStep()
    {
        return _currentStep;
    }

    /// <summary>
    /// 【AI接口】获取当前实验步骤的文字描述
    /// </summary>
    /// <returns>步骤描述字符串</returns>
    public string GetCurrentStepDescription()
    {
        return GetStepDescription(_currentStep);
    }

    /// <summary>
    /// 【AI接口】获取实验步骤执行历史
    /// </summary>
    /// <returns>步骤历史列表</returns>
    public List<PendulumExperimentStep> GetStepHistory()
    {
        return new List<PendulumExperimentStep>(_stepHistory); // 返回副本，防止外部修改
    }

    /// <summary>
    /// 【AI接口】推进到下一步实验
    /// </summary>
    /// <returns>是否推进成功（已到最后一步则返回false）</returns>
    public bool NextStep()
    {
        if (_currentStep == PendulumExperimentStep.ExperimentComplete)
        {
            Debug.LogWarning("实验已完成，无法推进下一步");
            return false;
        }

        // 枚举值+1推进步骤
        _currentStep = (PendulumExperimentStep)((int)_currentStep + 1);
        _stepHistory.Add(_currentStep);
        OnStepChanged?.Invoke(_currentStep);
        Debug.Log($"实验步骤推进：{GetStepDescription(_currentStep)}");
        return true;
    }

    /// <summary>
    /// 【AI接口】回退到上一步实验
    /// </summary>
    /// <returns>是否回退成功（已到第一步则返回false）</returns>
    public bool PreviousStep()
    {
        if (_currentStep == PendulumExperimentStep.PrepareEquipment)
        {
            Debug.LogWarning("已到实验第一步，无法回退");
            return false;
        }

        // 枚举值-1回退步骤
        _currentStep = (PendulumExperimentStep)((int)_currentStep - 1);
        _stepHistory.Add(_currentStep);
        OnStepChanged?.Invoke(_currentStep);
        Debug.Log($"实验步骤回退：{GetStepDescription(_currentStep)}");
        return true;
    }

    /// <summary>
    /// 【AI接口】直接跳转到指定步骤（用于异常恢复/AI主动控制）
    /// </summary>
    /// <param name="targetStep">目标步骤</param>
    /// <returns>是否跳转成功</returns>
    public bool JumpToStep(PendulumExperimentStep targetStep)
    {
        if (!Enum.IsDefined(typeof(PendulumExperimentStep), targetStep))
        {
            Debug.LogError($"无效的实验步骤：{targetStep}");
            return false;
        }

        _currentStep = targetStep;
        _stepHistory.Add(_currentStep);
        OnStepChanged?.Invoke(_currentStep);
        Debug.Log($"实验步骤跳转：{GetStepDescription(_currentStep)}");
        return true;
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 获取步骤对应的文字描述
    /// </summary>
    /// <param name="step">步骤枚举</param>
    /// <returns>描述字符串</returns>
    private string GetStepDescription(PendulumExperimentStep step)
    {
        return step switch
        {
            PendulumExperimentStep.None => "未开始实验",
            PendulumExperimentStep.PrepareEquipment => "准备实验器材（单摆、刻度尺、秒表、铁架台等）",
            PendulumExperimentStep.AdjustLength => "调整单摆摆长（记录当前摆长数值）",
            PendulumExperimentStep.MeasureAngle => "测量并调整摆角（控制在5°以内，保证简谐运动）",
            PendulumExperimentStep.ReleasePendulum => "静止释放单摆（避免初速度影响周期）",
            PendulumExperimentStep.MeasureTime => "测量摆动周期（测量10次全振动时间，取平均值）",
            PendulumExperimentStep.ChangeParameter => "更换摆长/摆球质量，重复上述实验步骤",
            PendulumExperimentStep.DataAnalysis => "数据分析，推导单摆周期公式 T=2π√(L/g)",
            PendulumExperimentStep.ExperimentComplete => "实验完成，整理器材并记录实验结论",
            _ => "未知步骤"
        };
    }

    /// <summary>
    /// 重置实验（AI接口）
    /// </summary>
    public void ResetExperiment()
    {
        _currentStep = PendulumExperimentStep.PrepareEquipment;
        _stepHistory.Clear();
        _stepHistory.Add(_currentStep);
        OnStepChanged?.Invoke(_currentStep);
        Debug.Log("实验已重置，回到初始步骤");
    }
    #endregion
}