using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单摆实验重置管理器
/// 功能：绑定重置按钮，统一重置摆球位置、摆长角度、周期计数器等实验参数
/// </summary>
public class PendulumResetManager : MonoBehaviour
{
    [Header("绑定核心组件")]
    public Pendulum pendulumCore;          // 核心单摆脚本
    public PendulumCounter periodCounter;  // 周期计数器脚本
    public PendulumDragControl dragControl;// 拖拽控制脚本
    public Button resetButton;             // 重置按钮UI

    [Header("重置配置（已废弃摆长重置）")]
    [HideInInspector] public float defaultLength = 2f;       // 不再使用
    [HideInInspector] public bool resetToDefaultLength = true; // 不再使用

    private void Start()
    {
        // 校验组件绑定
        if (pendulumCore == null) Debug.LogError("未绑定Pendulum核心脚本！");
        if (periodCounter == null) Debug.LogError("未绑定PendulumCounter计数脚本！");
        if (dragControl == null) Debug.LogError("未绑定PendulumDragControl拖拽脚本！");
        if (resetButton == null) Debug.LogError("未绑定重置按钮！");
        else resetButton.onClick.AddListener(OnResetButtonClicked); // 绑定按钮点击事件
    }

    /// <summary>
    /// 重置按钮点击回调（核心修复逻辑）
    /// </summary>
    public void OnResetButtonClicked()
    {
        // 1. 重置周期计数器（优先停止计数）
        ResetCounter();

        // 2. 重置摆球位置+彻底停止运动（核心修复）
        ResetPendulumBall();

        // 3. 校准铰链关节（只做一次，不重复刷新）
        ResetHingeJoint();

        Debug.Log("单摆实验重置完成：摆球回到最低点静止，摆长保持当前值，计数器已重置");
    }

    /// <summary>
    /// 重置周期计数器
    /// </summary>
    private void ResetCounter()
    {
        if (periodCounter == null) return;
        periodCounter.ResetAll(); // 计数器内置逻辑会关闭isSwingActive
    }

    /// <summary>
    /// 重置摆球位置+彻底停止所有运动（核心修复）
    /// </summary>
    private void ResetPendulumBall()
    {
        if (pendulumCore?.ball == null) return;

        Rigidbody ballRb = pendulumCore.ball;

        // 第一步：强制开启运动学，彻底接管刚体
        ballRb.isKinematic = true;

        // 第二步：清零所有运动状态（速度/角速度）
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // 第三步：计算最低点位置（使用当前摆长，不重置）
        float currentLength = dragControl != null ? dragControl.currentLength : pendulumCore.pendulumLength;
        Vector3 targetPos = pendulumCore.transform.position - Vector3.up * currentLength;
        targetPos.z = pendulumCore.transform.position.z; // 锁定Z轴与悬挂点一致

        // 第四步：强制设置位置到最低点
        ballRb.transform.position = targetPos;

        

        // 第五步：延迟1帧关闭运动学（避免刚体立即受重力影响摆动）
        CancelInvoke();
        Invoke(nameof(ReleaseKinematic), 0.01f);
    }

    /// <summary>
    /// 延迟释放运动学状态（防止重置后立即摆动）
    /// </summary>
    private void ReleaseKinematic()
    {
        if (pendulumCore?.ball == null) return;
        pendulumCore.ball.isKinematic = false;
    }

    /// <summary>
    /// 重置铰链关节（适配最低点位置）
    /// </summary>
    private void ResetHingeJoint()
    {
        if (pendulumCore?.ball == null || pendulumCore.hinge == null) return;

        // 校准铰链锚点到最低点
        Vector3 correctAnchor = pendulumCore.transform.position - pendulumCore.ball.transform.position;
        pendulumCore.hinge.anchor = correctAnchor;
        pendulumCore.hinge.connectedAnchor = Vector3.zero;
    }

    /// <summary>
    /// 编辑器快速重置
    /// </summary>
    [ContextMenu("快速重置单摆实验")]
    public void QuickReset()
    {
        OnResetButtonClicked();
    }

    // 防止重复调用延迟函数
    private void OnDestroy()
    {
        CancelInvoke(nameof(ReleaseKinematic));
    }
}