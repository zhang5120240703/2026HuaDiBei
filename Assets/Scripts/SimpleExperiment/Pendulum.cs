using UnityEngine;

public class Pendulum : MonoBehaviour
{
    [Header("拖拽对象")]
    public Rigidbody ball;       // 摆球的Rigidbody
    public Transform line;       // 摆线的Transform

    [Header("单摆参数")]
    public float pendulumLength = 2f; // 摆长（单位：米）
    public float periodT = 1.5f;      // 手动输入的周期，用于计算g

    [Header("实验结果")]
    public float calculatedG;         // 计算出的重力加速度

    private Camera mainCam;
    private bool isDragging = false;
    private HingeJoint hinge;

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

        // 5. 配置摆球
        ball.useGravity = true;
        ball.isKinematic = false;
        ball.angularDrag = 0.2f;
        ball.drag = 0f;
        ball.mass = 1f;

        // 6. 初始化摆球到竖直下垂
        ball.transform.position = transform.position - Vector3.up * pendulumLength;
    }

    void Update()
    {
        UpdateLineVisual();

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

        // 拖动中
        if (isDragging)
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(Vector3.forward, transform.position.z);

            if (dragPlane.Raycast(ray, out float enterDistance))
            {
                Vector3 mouseWorldPos = ray.GetPoint(enterDistance);
                Vector3 dirToMouse = mouseWorldPos - transform.position;

                if (dirToMouse.magnitude > 0.01f)
                {
                    ball.transform.position = transform.position + dirToMouse.normalized * pendulumLength;
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
        }
    }

    void UpdateLineVisual()
    {
        if (line == null || ball == null) return;

        line.position = Vector3.Lerp(transform.position, ball.position, 0.5f);
        Vector3 lineDir = ball.position - transform.position;
        line.rotation = Quaternion.LookRotation(lineDir) * Quaternion.Euler(90f, 0f, 0f);
        float actualLength = Vector3.Distance(transform.position, ball.position);
        line.localScale = new Vector3(0.08f, actualLength / 2f, 0.08f);
    }

   /* [ContextMenu("计算重力加速度")]
    public void CalculateGravity()
    {
        if (periodT <= 0)
        {
            Debug.LogError("周期T必须大于0！");
            return;
        }
        calculatedG = (4 * Mathf.PI * Mathf.PI * pendulumLength) / (periodT * periodT);
        Debug.Log($" 单摆实验计算结果：");
        Debug.Log($"   摆长 L = {pendulumLength:F2} m");
        Debug.Log($"   周期 T = {periodT:F2} s");
        Debug.Log($"   计算重力加速度 g = {calculatedG:F2} m/s²");
    }
   */
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
}