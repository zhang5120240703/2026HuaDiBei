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

        // 2. 重置摆球位置+彻底停止运动
        ResetPendulumBall();

        // 3. 校准铰链关节（只做一次，不重复刷新）
        ReconnectHingeAfterReset();

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
    /// 重置摆球位置+彻底停止所有运动（断开铰链）
    /// </summary>
    private void ResetPendulumBall()
    {
        if (pendulumCore?.ball == null) return;

        Rigidbody ballRb = pendulumCore.ball;
        HingeJoint hinge = pendulumCore.hinge;

        // 1. 临时断开铰链连接（避免关节施加约束力）
        if (hinge != null)
        {
            hinge.connectedBody = null;
            hinge.autoConfigureConnectedAnchor = true; // 临时自动配置，后续恢复
        }

        // 2. 强制开启运动学，彻底接管刚体
        ballRb.isKinematic = true;

        // 3. 清零所有运动状态
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // 4. 计算最低点位置（使用当前摆长）
        float currentLength = dragControl != null ? dragControl.currentLength : pendulumCore.pendulumLength;
        Vector3 targetPos = pendulumCore.transform.position - Vector3.up * currentLength;
        targetPos.z = pendulumCore.transform.position.z;
        ballRb.transform.position = targetPos;

        // 5. 同步变换旋转（确保无初始角偏差）
        ballRb.transform.rotation = Quaternion.identity; // 或根据悬挂方向调整

        // 6. 重新连接铰链（并正确配置锚点）
        ReconnectHingeAfterReset();

        // 7. 延迟释放运动学（确保铰链配置已应用）
        CancelInvoke();
        Invoke(nameof(ReleaseKinematic), 0.02f);
    }

    /// <summary>
    /// 重新连接铰链并校准锚点（在重置位置后调用）
    /// </summary>
    private void ReconnectHingeAfterReset()
    {
        if (pendulumCore?.hinge == null || pendulumCore?.ball == null) return;

        HingeJoint hinge = pendulumCore.hinge;
        Rigidbody fixedRb = pendulumCore.GetComponent<Rigidbody>();
        if (fixedRb == null)
        {
            fixedRb = pendulumCore.gameObject.AddComponent<Rigidbody>();
            fixedRb.isKinematic = true;
            fixedRb.useGravity = false;
        }

        // 重新设置连接体
        hinge.connectedBody = fixedRb;
        hinge.axis = new Vector3(0, 0, 1);
        hinge.useMotor = false;
        hinge.enableCollision = false;

        // 关键：手动计算锚点（悬挂点在摆球局部空间的位置）
        Vector3 anchorPoint = pendulumCore.transform.position - pendulumCore.ball.transform.position;
        hinge.anchor = anchorPoint;
        hinge.connectedAnchor = Vector3.zero;
        hinge.autoConfigureConnectedAnchor = false;

        // 确保铰链角度复位为0
        
    }

    /// <summary>
    /// 延迟释放运动学（可改为协程等待固定更新）
    /// </summary>
    private void ReleaseKinematic()
    {
        if (pendulumCore?.ball == null) return;
        pendulumCore.ball.isKinematic = false;
        // 再次强制清零速度，防止物理引擎在激活瞬间添加意外速度
        pendulumCore.ball.velocity = Vector3.zero;
        pendulumCore.ball.angularVelocity = Vector3.zero;
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