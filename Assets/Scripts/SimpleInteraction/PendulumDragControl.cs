using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 用于控制单摆UI滑动条调摆长 + 鼠标拖拽调整角度
/// 功能：滑动条调整摆长 | 角度：鼠标拖拽调整，有最大限制
/// </summary>
public class PendulumDragControl : MonoBehaviour
{
    [Header("=== 单摆核心组件（赋值）===")]
    public Rigidbody pendulumBall;       // 摆球Rigidbody
    public Transform pendulumLine;       // 摆线

    [Header("=== 摆长调节-滑动条（赋值）===")]
    public Slider lengthSlider;          // 摆长调节滑动条

    [Header("=== 摆长配置（仅修改这里）===")]
    [Range(0.1f, 0.5f)]
    public float lengthStep = 0.2f;      // （保留字段，可删除）
    public float minLength = 0.5f;       // 最小摆长（和滑动条Min一致）
    public float maxLength = 2f;         // 最大摆长（和滑动条Max一致）
    [Range(10f, 80f)]
    public float maxSwingAngle = 45f;    // 单摆最大摆动角度（度）

    [Header("=== 实时数据（仅显示）===")]
    public float currentLength;          // 当前实际摆长
    public float currentAngle;           // 当前实际摆动角度（度）

    // 内部状态
    private Camera _mainCam;
    private bool _isDraggingBall;
    private Plane _dragPlane;            // 拖拽平面（固定Z轴，仅XY）

    #region ===================== 【AI 实验数据接口】 =====================
    /// <summary>
    /// 获取当前使用摆长
    /// 单位：米(m)
    /// </summary>
    public float GetCurrentLength() => currentLength;

    /// <summary>
    /// 获取当前摆角
    /// 单位：角度(deg)
    /// </summary>
    public float GetCurrentAngle() => currentAngle;

    /// <summary>
    /// 获取最大可设置摆长
    /// 单位：米(m)
    /// </summary>
    public float GetMaxLength() => maxLength;

    /// <summary>
    /// 获取最小可设置摆长
    /// 单位：米(m)
    /// </summary>
    public float GetMinLength() => minLength;
    #endregion
    void Start()
    {
        // 初始化主相机
        _mainCam = Camera.main ?? FindObjectOfType<Camera>();
        if (_mainCam == null) { Debug.LogError("未找到主相机"); enabled = false; return; }

        // 校验核心组件
        if (pendulumBall == null) { Debug.LogError("未赋值摆球Rigidbody"); enabled = false; return; }
        if (pendulumLine == null) { Debug.LogError("未赋值摆线（Cylinder）Transform"); enabled = false; return; }
        if (lengthSlider == null) { Debug.LogError("未赋值摆长调节滑动条"); enabled = false; return; }

        // 自动给摆球添加Collider（拖拽检测用）
        if (pendulumBall.GetComponent<Collider>() == null)
        {
            pendulumBall.gameObject.AddComponent<SphereCollider>();
            Debug.LogWarning("摆球无Collider，已自动添加");
        }

        // 初始化摆长（绑定滑动条值）
        currentLength = Mathf.Clamp(Vector3.Distance(transform.position, pendulumBall.position), minLength, maxLength);
        lengthSlider.minValue = minLength;   // 同步滑动条最小范围
        lengthSlider.maxValue = maxLength;   // 同步滑动条最大范围
        lengthSlider.value = currentLength;  // 滑动条初始值匹配当前摆长

        // 初始化拖拽平面（固定Z轴，跟随摆悬挂点）
        _dragPlane = new Plane(Vector3.forward, transform.position.z);
        // 绑定滑动条监听事件
        BindSliderEvents();
        // 初始化摆线显示
        UpdatePendulumVisual();

        Debug.Log($"初始化完成 → 当前摆长：{currentLength:F2}m | 最大摆角：{maxSwingAngle}°");
        Debug.Log("操作说明：1. 滑动条调节摆长  2. 鼠标拖拽调整摆角");
    }

    void Update()
    {
        if (pendulumBall == null || pendulumLine == null) return;

        HandleMouseDragAngle();   // 处理鼠标拖动角度
        UpdateCurrentAngle();     // 实时计算当前角度
                                  
        if (!_isDraggingBall)
        {
            UpdatePendulumVisual(); // 非拖动时仍在Update更新
        }
        // 拖动时已在DragBallAdjustAngle中即时更新，避免重复
    }

    #region 功能1：滑动条调节摆长
    /// <summary>
    /// 绑定滑动条值改变事件
    /// </summary>
    void BindSliderEvents()
    {
        lengthSlider.onValueChanged.AddListener(OnLengthSliderChanged);
    }

    /// <summary>
    /// 滑动条值改变时调整摆长
    /// </summary>
    /// <param name="newLength">滑动条的新值</param>
    void OnLengthSliderChanged(float newLength)
    {
        currentLength = Mathf.Clamp(newLength, minLength, maxLength); // 二次校验范围
        UpdateBallPosition();
        SyncLengthToOtherScripts(); // 同步摆长到其他脚本（Pendulum/PendulumCounter）
        Debug.Log($"当前摆长：{currentLength:F2}m");
    }
    #endregion

    #region 功能2：鼠标拖拽调整摆角
    /// <summary>
    /// 处理鼠标拖拽行为，调整摆角
    /// </summary>
    void HandleMouseDragAngle()
    {
        // 鼠标按下：开始拖拽
        // PendulumDragControl.cs 中修改鼠标按下逻辑
        if (Input.GetMouseButtonDown(0) && !_isDraggingBall)
        {
            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 20f) && hit.rigidbody == pendulumBall)
            {
                _isDraggingBall = true;
                pendulumBall.isKinematic = true;
                pendulumBall.velocity = Vector3.zero;
                pendulumBall.angularVelocity = Vector3.zero;
                // 新增：拖动开始时立即同步摆线
                UpdatePendulumVisual();
            }
        }

        // 鼠标松开：停止拖拽
        if (Input.GetMouseButtonUp(0) && _isDraggingBall)
        {
            StopDrag();
        }

        // 拖拽中：调整角度（摆长固定）
        if (_isDraggingBall)
        {
            DragBallAdjustAngle();
        }
    }

    /// <summary>
    /// 拖拽摆球调整角度（限制最大角度）
    /// </summary>
    /// <summary>
    /// 拖动摆球调整角度（限制Z轴）
    /// </summary>
    void DragBallAdjustAngle()
    {
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (!_dragPlane.Raycast(ray, out float enter)) return;

        Vector3 mousePos = ray.GetPoint(enter);
        // 强制摆球位置Z轴 = 悬挂点Z轴
        mousePos.z = transform.position.z;

        Vector3 dir = mousePos - transform.position;
        dir.z = 0; // 仅在XY平面运动
        if (dir.magnitude < 0.01f) return;

        // 计算目标角度（限制最大摆动角度）
        float targetAngle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
        targetAngle = Mathf.Clamp(targetAngle, -maxSwingAngle, maxSwingAngle);
        float rad = targetAngle * Mathf.Deg2Rad;

        // 计算目标位置（强制Z轴一致）
        Vector3 targetPos = new Vector3(
            transform.position.x + Mathf.Sin(rad) * currentLength,
            transform.position.y - Mathf.Cos(rad) * currentLength,
            transform.position.z // 锁定Z轴
        );
        pendulumBall.transform.position = targetPos;

        // 关键修改：拖动时立即更新摆线，无延迟
        UpdatePendulumVisual();
    }

    /// <summary>
    /// 停止拖拽，释放摆球
    /// </summary>
    void StopDrag()
    {
        _isDraggingBall = false;
        Pendulum pendulum = GetComponent<Pendulum>();
        if (pendulum != null && pendulum.hinge != null)
        {
            Vector3 correctAnchor = transform.position - pendulumBall.transform.position;
            pendulum.hinge.anchor = correctAnchor;
        }
        pendulumBall.isKinematic = false;
        Debug.Log($"停止拖拽 → 当前摆角：{currentAngle:F1}°");
        PendulumCounter counter = FindObjectOfType<PendulumCounter>();
        if (counter != null)
        {
            counter.ActivateSwingCount();
        }
    }

    /// <summary>
    /// 实时计算当前摆角（度）
    /// </summary>
    void UpdateCurrentAngle()
    {
        Vector3 dir = pendulumBall.position - transform.position;
        dir.z = 0;
        currentAngle = dir.magnitude < 0.01f ? 0 : Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
    }
    #endregion

    #region 辅助功能：更新摆线/同步摆长/更新摆球位置
    /// <summary>
    /// 实时更新摆线的位置、旋转、缩放
    /// </summary>
    void UpdatePendulumVisual()
    {
        Vector3 lineDir = pendulumBall.position - transform.position;
        float actualLen = lineDir.magnitude;

        // 原逻辑用Lerp插值导致位置延迟，改为直接设置中点
        pendulumLine.position = transform.position + lineDir * 0.5f;
        // 优化旋转计算，避免LookRotation的额外开销
        pendulumLine.rotation = Quaternion.FromToRotation(Vector3.up, lineDir);
        // 直接设置缩放，无插值
        pendulumLine.localScale = new Vector3(0.08f, actualLen / 2f, 0.08f);//设置摆线直径
    }

    /// <summary>
    /// 根据当前摆长更新摆球位置（保持当前角度）
    /// </summary>
    public void UpdateBallPosition()
    {
        // 计算目标位置（基于当前角度和新摆长）
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 targetPos = new Vector3(
            transform.position.x + Mathf.Sin(rad) * currentLength,
            transform.position.y - Mathf.Cos(rad) * currentLength,
            transform.position.z
        );

        // 移动摆球（临时禁用物理）
        pendulumBall.isKinematic = true;
        pendulumBall.transform.position = targetPos;
        pendulumBall.velocity = Vector3.zero;
        pendulumBall.angularVelocity = Vector3.zero;
        pendulumBall.isKinematic = false;

        // 校准铰链关节（确保摆动轴正确）
        Pendulum pendulum = GetComponent<Pendulum>();
        if (pendulum != null && pendulum.hinge != null)
        {
            Vector3 correctAnchor = pendulum.transform.position - pendulumBall.transform.position;
            pendulum.hinge.anchor = correctAnchor;
            pendulum.hinge.connectedAnchor = Vector3.zero;
        }
    }

    /// <summary>
    /// 同步摆长到其他脚本（Pendulum/PendulumCounter）
    /// </summary>
    private void SyncLengthToOtherScripts()
    {
        // 同步到Pendulum脚本
        Pendulum pendulum = GetComponent<Pendulum>();
        if (pendulum != null)
        {
            pendulum.pendulumLength = currentLength;
            pendulum.CalculateTheoreticalPeriod(); // 同步摆长后更新理论周期
        }
        // 同步到周期计数器
        PendulumCounter counter = FindObjectOfType<PendulumCounter>();
        if (counter != null)
        {
            counter.pendulumLength = currentLength;
            counter.CalculateTheoreticalPeriod(); // 同步摆长后更新理论周期
        }
    }
    #endregion

    // 销毁时同步摆长（确保数据一致）
    void OnDestroy()
    {
        Pendulum pendulum = GetComponent<Pendulum>();
        PendulumCounter counter = FindObjectOfType<PendulumCounter>();
        if (pendulum != null) pendulum.pendulumLength = currentLength;
        if (counter != null) counter.pendulumLength = currentLength;
    }
}