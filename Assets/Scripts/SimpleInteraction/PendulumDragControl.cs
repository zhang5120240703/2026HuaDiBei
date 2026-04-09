using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单摆控制脚本：UI按钮调摆长 + 鼠标拖摆球调角度
/// 摆长：点击±按钮增减 | 角度：鼠标拖摆球左右（严格限制）

/// </summary>
public class PendulumDragControl : MonoBehaviour
{
    [Header("=== 单摆核心对象（必赋值）===")]
    public Rigidbody pendulumBall;       // 摆球Rigidbody
    public Transform pendulumLine;       // 摆线

    [Header("=== 摆长调节-UI按钮（必赋值）===")]
    public Button btnLengthAdd;          // 摆长+ 按钮
    public Button btnLengthMinus;        // 摆长- 按钮

    [Header("=== 调节参数（可视化修改）===")]
    [Range(0.1f, 0.5f)]
    public float lengthStep = 0.2f;      // 点击按钮的摆长增减步长（米）
    [Range(0.5f, 5f)]
    public float minLength = 0.5f;       // 最小摆长（防止过短）
    [Range(0.5f, 5f)]
    public float maxLength = 3f;         // 最大摆长（防止过长）
    [Range(10f, 80f)]
    public float maxSwingAngle = 45f;    // 最大摆动角度（±N度，严格限制）

    [Header("=== 实时数据（只读）===")]
    public float currentLength;          // 当前实际摆长
    public float currentAngle;           // 当前实际摆动角度（度）

    // 内部状态
    private Camera _mainCam;
    private bool _isDraggingBall;
    private Plane _dragPlane;            // 角度拖动平面（固定Z轴，仅XY）

    void Start()
    {
        // 初始化主相机
        _mainCam = Camera.main ?? FindObjectOfType<Camera>();
        if (_mainCam == null) { Debug.LogError("未找到主相机！"); enabled = false; return; }

        // 强制校验核心对象
        if (pendulumBall == null) { Debug.LogError("请赋值摆球Rigidbody！"); enabled = false; return; }
        if (pendulumLine == null) { Debug.LogError("请赋值摆线（Cylinder）Transform！"); enabled = false; return; }
        if (btnLengthAdd == null || btnLengthMinus == null) { Debug.LogError("请赋值摆长±UI按钮！"); enabled = false; return; }

        // 摆球自动补全Collider（射线检测必须）
        if (pendulumBall.GetComponent<Collider>() == null)
        {
            pendulumBall.gameObject.AddComponent<SphereCollider>();
            Debug.LogWarning("摆球无Collider，已自动添加");
        }

        // 初始化摆长（基于悬挂点到摆球的初始距离）
        currentLength = Mathf.Clamp(Vector3.Distance(transform.position, pendulumBall.position), minLength, maxLength);
        // 初始化拖动平面（固定Z轴，避免摆球飘走）
        _dragPlane = new Plane(Vector3.forward, transform.position.z);
        // 绑定UI按钮点击事件
        BindButtonEvents();
        // 复位单摆到竖直下垂
        //ResetPendulum();

        Debug.Log($"初始化完成 → 当前摆长：{currentLength:F2}m | 最大摆角：{maxSwingAngle}°");
        Debug.Log("操作规则：1. 点击UI按钮调摆长  2. 鼠标拖摆球调角度");
    }

    void Update()
    {
        if (pendulumBall == null || pendulumLine == null) return;

        HandleMouseDragAngle();   // 鼠标拖摆球调角度
        UpdatePendulumVisual();   // 实时更新摆线显示
        UpdateCurrentAngle();     // 实时计算当前角度
    }

    #region 核心1：绑定UI按钮，点击增减摆长
    /// <summary>
    /// 绑定摆长±按钮的点击事件
    /// </summary>
    void BindButtonEvents()
    {
        btnLengthAdd.onClick.AddListener(OnLengthAdd);
        btnLengthMinus.onClick.AddListener(OnLengthMinus);
    }

    /// <summary>
    /// 点击摆长+按钮：摆长增加，摆球向下，摆线变长
    /// </summary>
    void OnLengthAdd()
    {
        currentLength = Mathf.Clamp(currentLength + lengthStep, minLength, maxLength);
        UpdateBallPosition();
        Debug.Log($"摆长+ → 当前摆长：{currentLength:F2}m");
    }

    /// <summary>
    /// 点击摆长-按钮：摆长减少，摆球向上，摆线变短
    /// </summary>
    void OnLengthMinus()
    {
        currentLength = Mathf.Clamp(currentLength - lengthStep, minLength, maxLength);
        UpdateBallPosition();
        Debug.Log($"摆长- → 当前摆长：{currentLength:F2}m");
    }

    /// <summary>
    /// 根据当前摆长，更新摆球位置（竖直上下，保留当前角度）
    /// </summary>
    void UpdateBallPosition()
    {
        // 保留当前角度，仅竖直上下移动摆球
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 targetPos = new Vector3(
            transform.position.x + Mathf.Sin(rad) * currentLength,
            transform.position.y - Mathf.Cos(rad) * currentLength,
            transform.position.z
        );
        // 固定摆球位置，清空物理速度避免晃动
        pendulumBall.isKinematic = true;
        pendulumBall.transform.position = targetPos;
        pendulumBall.velocity = Vector3.zero;
        pendulumBall.angularVelocity = Vector3.zero;
        pendulumBall.isKinematic = false;
    }
    #endregion

    #region 核心2：鼠标拖摆球调角度（严格限制，仅左右）
    /// <summary>
    /// 处理鼠标拖动摆球，仅调节摆动角度
    /// </summary>
    void HandleMouseDragAngle()
    {
        

        // 鼠标按下：开始拖动
        if (Input.GetMouseButtonDown(0) && !_isDraggingBall)
        {
            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 20f) && hit.rigidbody == pendulumBall)
            {
                _isDraggingBall = true;
                pendulumBall.isKinematic = true;
                pendulumBall.velocity = Vector3.zero;
            }
        }

        // 鼠标松开：停止拖动，恢复物理
        if (Input.GetMouseButtonUp(0) && _isDraggingBall)
        {
            StopDrag();
        }

        // 拖动中：调节角度（摆长固定）
        if (_isDraggingBall)
        {
            DragBallAdjustAngle();
        }
    }

    /// <summary>
    /// 拖动摆球调节角度，严格限制最大角度
    /// </summary>
    void DragBallAdjustAngle()
    {
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (!_dragPlane.Raycast(ray, out float enter)) return;

        Vector3 mousePos = ray.GetPoint(enter);
        mousePos.z = transform.position.z;
        Vector3 dir = mousePos - transform.position;
        dir.z = 0;
        if (dir.magnitude < 0.01f) return;

        // 计算角度并严格限制
        float targetAngle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
        targetAngle = Mathf.Clamp(targetAngle, -maxSwingAngle, maxSwingAngle);
        float rad = targetAngle * Mathf.Deg2Rad;

        // 摆长固定，仅更新角度对应的位置
        Vector3 targetPos = new Vector3(
            transform.position.x + Mathf.Sin(rad) * currentLength,
            transform.position.y - Mathf.Cos(rad) * currentLength,
            transform.position.z
        );
        pendulumBall.transform.position = targetPos;
    }

    /// <summary>
    /// 停止拖动，恢复单摆自然摆动
    /// </summary>
    void StopDrag()
    {
        _isDraggingBall = false;
        pendulumBall.isKinematic = false;
        Debug.Log($"停止调角度 → 当前摆角：{currentAngle:F1}°");
    }

    /// <summary>
    /// 实时计算当前摆动角度（度）
    /// </summary>
    void UpdateCurrentAngle()
    {
        Vector3 dir = pendulumBall.position - transform.position;
        dir.z = 0;
        currentAngle = dir.magnitude < 0.01f ? 0 : Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
    }
    #endregion

    #region 辅助：更新摆线/复位单摆
    /// <summary>
    /// 实时更新摆线的位置、旋转、缩放

    /// </summary>
    void UpdatePendulumVisual()
    {
        Vector3 lineDir = pendulumBall.position - transform.position;
        float actualLen = lineDir.magnitude;

        // 摆线位置：悬挂点和摆球的中点
        pendulumLine.position = transform.position + lineDir * 0.5f;
        // 摆线旋转：精准朝向摆球
        pendulumLine.rotation = Quaternion.FromToRotation(Vector3.up, lineDir);
        // 摆线缩放：长度同步，粗细固定
        pendulumLine.localScale = new Vector3(0.08f, actualLen / 2f, 0.08f);
    }

    /// <summary>
    /// 复位单摆：竖直下垂（角度0°），保留当前摆长
   
    /// </summary>
   /* [ContextMenu("复位单摆（竖直下垂）")]
    public void ResetPendulum()
    {
        _isDraggingBall = false;
        currentAngle = 0;
        // 摆球回到悬挂点正下方
        pendulumBall.isKinematic = true;
        pendulumBall.transform.position = transform.position - Vector3.up * currentLength;
        pendulumBall.velocity = Vector3.zero;
        pendulumBall.angularVelocity = Vector3.zero;
        pendulumBall.isKinematic = false;
    }
   */
    #endregion
   
    // 销毁时同步摆长到原有计数器，保证g值计算准确
    void OnDestroy()
    {
        Pendulum pendulum = GetComponent<Pendulum>();
        PendulumCounter counter = FindObjectOfType<PendulumCounter>();
        if (pendulum != null) pendulum.pendulumLength = currentLength;
        if (counter != null) counter.pendulumLength = currentLength;
    }
}