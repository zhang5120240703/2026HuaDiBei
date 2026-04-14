using UnityEngine;

public class Pendulum : MonoBehaviour
{
    [Header("拖拽对象")]
    public Rigidbody ball;       // 摆球的Rigidbody
    public Transform line;       // 摆线的Transform

    [Header("单摆参数")]
    public float pendulumLength = 2f; // 摆长（单位：米）
    [HideInInspector] public float fixedG = 9.8f; // 固定重力加速度=9.8
    [HideInInspector] public float theoreticalPeriod; // 理论周期（基于g=9.8计算）

    [Header("实验结果")]
    public float calculatedG;         // 兼容原有显示，固定为9.8

    private Camera mainCam;
    public HingeJoint hinge;

  
    #region ===================== 【AI 实验数据接口】 =====================
    /// <summary>
    /// 获取当前摆长
    /// 单位：米(m)
    /// </summary>
    public float GetPendulumLength() => pendulumLength;

    /// <summary>
    /// 获取固定重力加速度
    /// 单位：m/s²
    /// </summary>
    public float GetGravityValue() => fixedG;

    /// <summary>
    /// 获取摆球当前世界坐标
    /// 用于AI判断位置、姿态、是否正常摆动
    /// </summary>
    public Vector3 GetCurrentBallPosition() => ball != null ? ball.position : Vector3.zero;
    #endregion
    void Start()
    {
        mainCam = Camera.main;

        // 1. 给摆球添加/获取铰链关节
        hinge = ball.GetComponent<HingeJoint>();
        if (hinge == null)
            hinge = ball.gameObject.AddComponent<HingeJoint>();

        // 2. 给悬挂点添加Rigidbody（HingeJoint必须连接两个刚体）
        Rigidbody fixRb = GetComponent<Rigidbody>();
        if (fixRb == null)
            fixRb = gameObject.AddComponent<Rigidbody>();

        // 3. 配置悬挂点：固定不动
        fixRb.isKinematic = true;
        fixRb.useGravity = false;

        // 4. 配置铰链关节
        hinge.connectedBody = fixRb;          // 连接到悬挂点
        hinge.axis = new Vector3(0, 0, 1);    // 绕Z轴左右摆动（正确轴）
        hinge.useMotor = false;
        hinge.enableCollision = false;

        //  关键：设置铰链锚点 = 悬挂点在摆球局部空间的位置
        Vector3 anchorPoint = transform.position - ball.transform.position;
        hinge.anchor = anchorPoint;
        //  关键：确保摆动轴绝对正确
        hinge.autoConfigureConnectedAnchor = false;
        hinge.connectedAnchor = Vector3.zero;

        // 5. 配置摆球（优化物理参数，匹配g=9.8的周期）
        ball.useGravity = true;
        ball.isKinematic = false;
        ball.angularDrag = 0.08f; // 降低阻尼，减少周期偏差
        ball.drag = 0.002f;       // 极低线性阻尼
        ball.mass = 1f;

        // 6. 初始化摆球到竖直下垂
        ball.transform.position = transform.position - Vector3.up * pendulumLength;

        // 初始化固定g和理论周期
        calculatedG = fixedG;
        CalculateTheoreticalPeriod();
    }

    
    void Update()
    {
       
    }

    /// <summary>
    /// 基于固定g=9.8计算理论周期
    /// </summary>
    [ContextMenu("计算理论周期")]
    public void CalculateTheoreticalPeriod()
    {
        theoreticalPeriod = 2 * Mathf.PI * Mathf.Sqrt(pendulumLength / fixedG);
        calculatedG = fixedG; // 强制固定为9.8
        Debug.Log($" 单摆参数（g固定为9.8）：");
        Debug.Log($"   摆长 L = {pendulumLength:F2} m");
        Debug.Log($"   理论周期 T = {theoreticalPeriod:F2} s");
        Debug.Log($"   重力加速度 g = {calculatedG:F2} m/s²");
    }
}