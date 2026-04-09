using UnityEngine;

public class Pendulum : MonoBehaviour
{
    [Header("拖拽对象")]
    public Rigidbody ball;       // 摆球的Rigidbody
    public Transform line;       // 摆线的Transform

    [Header("单摆参数")]
    public float pendulumLength = 2f; // 摆长（单位：米）
    [HideInInspector] public float fixedG = 10f; // 固定重力加速度=10
    [HideInInspector] public float theoreticalPeriod; // 理论周期（基于g=10计算）

    [Header("实验结果")]
    public float calculatedG;         // 兼容原有显示，固定为10

    private Camera mainCam;
    private bool isDragging = false;
    public HingeJoint hinge;

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

        // 5. 配置摆球（优化物理参数，匹配g=10的周期）
        ball.useGravity = true;
        ball.isKinematic = false;
        ball.angularDrag = 0.05f; // 降低阻尼，减少周期偏差
        ball.drag = 0.001f;       // 极低线性阻尼
        ball.mass = 1f;

        // 6. 初始化摆球到竖直下垂
        ball.transform.position = transform.position - Vector3.up * pendulumLength;

        // 初始化固定g和理论周期
        calculatedG = fixedG;
        CalculateTheoreticalPeriod();
    }

    void Update()
    {
        //UpdateLineVisual();

        // 鼠标左键按下
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.rigidbody == ball)
                {
                    isDragging = true;
                    ball.isKinematic = true;
                }
            }
        }

        // 拖动中 —— 核心修改：使用 DragControl 的 currentLength 而非固定的 pendulumLength
        // 拖动中 —— 核心修改：锁定Z轴 + 强制XY平面运动
        if (isDragging)
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(Vector3.forward, transform.position.z); // 悬挂点的Z平面

            if (dragPlane.Raycast(ray, out float enterDistance))
            {
                Vector3 mouseWorldPos = ray.GetPoint(enterDistance);
                // 关键：强制鼠标世界坐标的Z轴 = 悬挂点的Z轴，彻底锁死前后移动
                mouseWorldPos.z = transform.position.z;

                Vector3 dirToMouse = mouseWorldPos - transform.position;
                // 同样锁死dirToMouse的Z轴
                dirToMouse.z = 0;

                if (dirToMouse.magnitude > 0.01f)
                {
                    // 改为获取 DragControl 的当前摆长
                    PendulumDragControl dragControl = GetComponent<PendulumDragControl>();
                    float currentLength = dragControl != null ? dragControl.currentLength : pendulumLength;

                    // 计算目标位置并锁死Z轴
                    Vector3 targetPos = transform.position + dirToMouse.normalized * currentLength;
                    targetPos.z = transform.position.z; // 最终位置强制Z轴一致
                    ball.transform.position = targetPos;
                }
            }
        }

        // 鼠标松开
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            ball.isKinematic = false;
            ball.velocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;

            //  拖动结束后，重新校准铰链轴心到悬挂点
            if (hinge != null)
            {
                Vector3 correctAnchor = transform.position - ball.transform.position;
                hinge.anchor = correctAnchor;
            }

            // 新增：激活周期计数
            PendulumCounter counter = FindObjectOfType<PendulumCounter>();
            if (counter != null)
            {
                counter.ActivateSwingCount();
            }
        }
    }

    /// <summary>
    /// 基于固定g=10计算理论周期
    /// </summary>
    [ContextMenu("计算理论周期")]
    public void CalculateTheoreticalPeriod()
    {
        theoreticalPeriod = 2 * Mathf.PI * Mathf.Sqrt(pendulumLength / fixedG);
        calculatedG = fixedG; // 强制固定为10
        Debug.Log($" 单摆参数（g固定为10）：");
        Debug.Log($"   摆长 L = {pendulumLength:F2} m");
        Debug.Log($"   理论周期 T = {theoreticalPeriod:F2} s");
        Debug.Log($"   重力加速度 g = {calculatedG:F2} m/s²");
    }

    /*void UpdateLineVisual()
    {
        if (line == null || ball == null) return;

        line.position = Vector3.Lerp(transform.position, ball.position, 0.5f);
        Vector3 lineDir = ball.position - transform.position;
        line.rotation = Quaternion.LookRotation(lineDir) * Quaternion.Euler(90f, 0f, 0f);
        float actualLength = Vector3.Distance(transform.position, ball.position);
        line.localScale = new Vector3(0.08f, actualLength / 2f, 0.08f);
    }
    
    [ContextMenu("复位单摆")]
    public void ResetPendulum()
    {
        isDragging = false;
        ball.isKinematic = false;
        ball.velocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.transform.position = transform.position - Vector3.up * pendulumLength;

        //  复位时也校准轴心
        if (hinge != null)
        {
            Vector3 correctAnchor = transform.position - ball.transform.position;
            hinge.anchor = correctAnchor;
        }
    }
    */
}