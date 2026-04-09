using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单摆实验重置管理器
/// 功能：点击重置按钮后统一重置摆球位置、摆长、摆动角度、周期计数等所有实验参数
/// </summary>
public class PendulumResetManager : MonoBehaviour
{
    [Header("绑定核心组件")]
    public Pendulum pendulumCore;          // 核心单摆脚本
    public PendulumCounter periodCounter;  // 周期计数脚本
    public PendulumDragControl dragControl;// 拖拽控制脚本
    public Button resetButton;             // 重置按钮UI

    [Header("重置参数配置")]
    public float defaultLength = 2f;       // 重置后的默认摆长
    public bool resetToDefaultLength = true; // 是否重置为默认摆长（false则保留当前摆长）

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
    /// 重置按钮点击回调
    /// </summary>
    public void OnResetButtonClicked()
    {
        // 1. 重置周期计数器（清零所有计数数据）
        ResetCounter();

        // 2. 重置摆球物理状态（归位、停止运动）
        ResetPendulumBall();

        // 3. 重置摆长和拖拽控制参数
        ResetLengthAndAngle();

        // 4. 重置铰链关节（确保摆动轴心正确）
        ResetHingeJoint();

        Debug.Log("单摆实验已重置完成！摆球归位，计数清零，可重新开始实验");
    }

    /// <summary>
    /// 重置周期计数器
    /// </summary>
    private void ResetCounter()
    {
        if (periodCounter == null) return;
        periodCounter.ResetAll();
    }

    /// <summary>
    /// 重置摆球位置和物理状态
    /// </summary>
    private void ResetPendulumBall()
    {
        if (pendulumCore?.ball == null) return;

        // 停止摆球所有运动
        Rigidbody ballRb = pendulumCore.ball;
        ballRb.isKinematic = true;          // 暂时冻结物理
        ballRb.velocity = Vector3.zero;     // 清零速度
        ballRb.angularVelocity = Vector3.zero; // 清零角速度

        // 摆球归位到最低点（竖直下垂）
        float targetLength = resetToDefaultLength ? defaultLength : dragControl.currentLength;
        ballRb.transform.position = pendulumCore.transform.position - Vector3.up * targetLength;

        ballRb.isKinematic = false;         // 恢复物理
    }

    /// <summary>
    /// 重置摆长和摆动角度
    /// </summary>
    private void ResetLengthAndAngle()
    {
        if (dragControl == null) return;

        // 重置摆长
        dragControl.currentLength = resetToDefaultLength ? defaultLength : dragControl.currentLength;
        // 重置摆动角度为0（竖直向下）
        dragControl.currentAngle = 0f;
        // 更新摆球位置（确保角度归零）
        dragControl.UpdateBallPosition();
    }

    /// <summary>
    /// 重置铰链关节（确保轴心正确）
    /// </summary>
    private void ResetHingeJoint()
    {
        if (pendulumCore?.ball == null || pendulumCore.hinge == null) return;

        // 重新校准铰链锚点
        Vector3 correctAnchor = pendulumCore.transform.position - pendulumCore.ball.transform.position;
        pendulumCore.hinge.anchor = correctAnchor;
        pendulumCore.hinge.connectedAnchor = Vector3.zero;
    }

    /// <summary>
    /// 编辑器快捷重置
    /// </summary>
    [ContextMenu("快速重置单摆实验")]
    public void QuickReset()
    {
        OnResetButtonClicked();
    }
}