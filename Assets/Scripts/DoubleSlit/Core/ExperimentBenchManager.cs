using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable] public class StringEvent : UnityEvent<string> { }

/// <summary>
/// 实验台交互总控制器（3D 移动版）
///
/// ══ 操作方式 ══
///   左键拖拽          → 在水平 XZ 平面内移动（沿光具座前后 + 左右微调）
///   拖拽时滚轮        → 调节高度（Y 轴）
///   Shift + 左键拖拽  → 纯高度调节（Y 轴），鼠标上下对应升降
///   Ctrl  + 左键拖拽  → 纯 X 轴平移（左右对准光轴）
///
/// ══ 核心技术 ══
///   使用"屏幕空间增量"方案代替平面射线法：
///   每帧把鼠标像素增量 × worldPerPixel 转成世界坐标增量，
///   再投影到目标轴（XZ / Y / X），彻底消除相机角度导致的位移放大问题。
/// </summary>
[AddComponentMenu("DoubleSlit/Experiment Bench Manager")]
public class ExperimentBenchManager : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  实验器材引用
    // ══════════════════════════════════════════════

    [Header("── 实验器材引用（必填）──")]
    public ExperimentItem lightSource;
    public ExperimentItem singleSlit;
    public ExperimentItem doubleSlit;
    public ExperimentItem screen;

    // ══════════════════════════════════════════════
    //  光具座 / 光轴设置
    // ══════════════════════════════════════════════

    [Header("── 光具座 & 光轴 ──")]
    [Tooltip("光具座 Transform，用于判断器材排列顺序（前后关系沿其 forward 轴）。")]
    public Transform benchTransform;

    [Tooltip("光具座上器材排列的本地轴方向（默认 Forward = +Z 轴）。\n" +
             "查看场景 Gizmo 中的青色箭头，如方向不对请换选 Right。")]
    public BenchAxisChoice benchAxis = BenchAxisChoice.Forward;

    [Tooltip("光轴的世界 Y 坐标（光源中心高度）。\n" +
             "设定方法：在 Editor 中选中任意器材，查看 Transform.Position.y，填入此处。")]
    public float opticalAxisY = 1.05f;

    [Tooltip("光轴的世界 X 坐标（光轴横向中心，通常为 0 或光具座中线 X）。\n" +
             "设定方法：光具座中心的 X 坐标值。")]
    public float opticalAxisX = 0f;

    // ══════════════════════════════════════════════
    //  3D 运动边界
    // ══════════════════════════════════════════════

    [Header("── 3D 运动范围（世界坐标）──")]
    [Tooltip("器材可移动区域的最小角坐标（世界空间）。\n" +
             "设定方法：把一个器材拖到允许的最左/最低/最前端，记下其世界坐标填入。\n" +
             "X: 左右范围下界  Y: 高度范围下界  Z: 前后范围下界（或用 Gizmo 拖拽调整）")]
    public Vector3 boundsMin = new Vector3(-0.5f, 0.7f, -3.0f);

    [Tooltip("器材可移动区域的最大角坐标（世界空间）。\n" +
             "X: 左右范围上界  Y: 高度范围上界  Z: 前后范围上界")]
    public Vector3 boundsMax = new Vector3(0.5f, 1.6f, 3.0f);

    // ══════════════════════════════════════════════
    //  拖拽设置
    // ══════════════════════════════════════════════

    [Header("── 拖拽灵敏度 ──")]
    [Tooltip("XZ 平面拖拽灵敏度乘数（1.0 = 鼠标移多少像素物体走多少世界单位 × perspCorrect）")]
    [Range(0.5f, 3f)]
    public float xzSensitivity = 1.0f;

    [Tooltip("Shift 拖拽或滚轮调高时的 Y 轴灵敏度（米/像素 或 米/格）")]
    [Range(0.5f, 5f)]
    public float ySensitivity = 1.5f;

    [Tooltip("滚轮每格调节的高度（米）")]
    [Range(0.01f, 0.2f)]
    public float scrollYStep = 0.05f;

    [Tooltip("相邻器材最小间距（米，防止穿透）")]
    [Range(0.05f, 1f)]
    public float minSpacing = 0.22f;

    // ══════════════════════════════════════════════
    //  磁吸辅助
    // ══════════════════════════════════════════════

    [Header("── 磁吸辅助 ──")]
    public bool enableSnapAssist = true;

    [Tooltip("磁吸触发半径（米，3D 距离）")]
    [Range(0.05f, 1f)]
    public float snapRadius = 0.35f;

    [Header("── 推荐位置（世界坐标）──")]
    [Tooltip("各器材推荐位置。X/Y 建议填 opticalAxisX / opticalAxisY（即光轴坐标）。\n" +
             "Z 值：按实验要求设定各器材前后距离。\n" +
             "调试方法：Play 后将器材拖到理想位置，Inspector → 调试 区域会显示当前世界坐标。")]
    public Vector3 snapPosLight = new Vector3(0f, 1.05f, -2.2f);
    public Vector3 snapPosSingle = new Vector3(0f, 1.05f, -0.9f);
    public Vector3 snapPosDouble = new Vector3(0f, 1.05f, 0.1f);
    public Vector3 snapPosScreen = new Vector3(0f, 1.05f, 1.8f);

    // ══════════════════════════════════════════════
    //  验证参数
    // ══════════════════════════════════════════════

    [Header("── 验证容差 ──")]
    [Tooltip("Y 轴容差（米）：偏离光轴高度超过此值视为「高度未对齐」")]
    [Range(0.01f, 0.3f)]
    public float heightTolerance = 0.08f;

    [Tooltip("X 轴容差（米）：偏离光轴中心超过此值视为「横向偏移过大」")]
    [Range(0.01f, 0.3f)]
    public float xAlignTolerance = 0.06f;

    [Tooltip("顺序判定最小间距（米）：前后器材沿光具座 Z 差值需大于此值")]
    [Range(0.05f, 0.5f)]
    public float orderMinGap = 0.12f;

    [Header("── 引导提示 ──")]
    public bool enableStepGuide = true;

    // ══════════════════════════════════════════════
    //  事件回调
    // ══════════════════════════════════════════════

    [Header("── 事件回调 ──")]
    public UnityEvent onExperimentCorrect;/// 实验摆放正确事件（可绑定播放动画等反馈）
    public UnityEvent onExperimentIncorrect;/// 实验摆放错误事件（可绑定播放动画等反馈）
    public StringEvent onHintMessage;// 提示消息事件（参数为提示文本，UI模块可绑定显示在界面上）

    // ══════════════════════════════════════════════
    //  调试（只读）
    // ══════════════════════════════════════════════

    [Header("── 调试（只读，运行时查看）──")]
    [SerializeField] private string _debugDragMode;
    [SerializeField] private Vector3 _debugDragPos;// 当前被拖拽物体的实时位置（世界坐标）
    [SerializeField] private Vector3 _debugLightPos, _debugSSPos, _debugDSPos, _debugScrPos;// 各器材当前位置（世界坐标）

    // ══════════════════════════════════════════════
    //  枚举
    // ══════════════════════════════════════════════

    public enum BenchAxisChoice { Forward, Right, Up }// 光具座轴线选项（默认 Forward = +Z 轴）

    private enum DragMode { XZ, YOnly, XOnly }// 拖拽模式（根据修饰键切换）

    // ══════════════════════════════════════════════
    //  私有字段
    // ══════════════════════════════════════════════

    private Camera _cam;// 主相机引用
    private ExperimentItem _dragging;// 当前被拖拽物体
    private DragMode _dragMode;// 当前拖拽模式
    private Vector2 _prevMouseScreen;   // 上一帧鼠标屏幕位置（Vector2）
    private bool _isDragActive;// 是否正在拖拽    
    private ExperimentItem[] _items;// 所有器材引用
    private Dictionary<ExperimentItem, Vector3> _snapTargets;// 磁吸目标位置字典
    private DoubleSlitSimpleController _expCtrl;// 实验控制器，用于判断当前阶段

    private Coroutine _validateCo;// 延迟验证协程引用（拖拽结束后延迟一小段时间再验证，避免误判）

    // ══════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null)
            Debug.LogError("[BenchManager] 找不到 MainCamera！");

        _expCtrl = FindObjectOfType<DoubleSlitSimpleController>();

        _items = new[] { lightSource, singleSlit, doubleSlit, screen };
        for (int i = 0; i < _items.Length; i++)
            if (_items[i] == null)
                Debug.LogError($"[BenchManager] 器材引用 [{i}] 为空，请在 Inspector 指定！");

        BuildSnapTargets();
    }

    void Start()
    {
        if (enableStepGuide)
            onHintMessage?.Invoke("💡 左键拖拽移动器材(XZ)  |  拖拽时滚轮调整高度  |  Shift拖拽=纯升降  |  Ctrl拖拽=纯左右");
    }

    void Update()
    {
        bool canDrag = _expCtrl == null
            || _expCtrl.CurrentStep == DoubleSlitSimpleController.ExperimentStep.Setup;

        if (!canDrag)
        {
            if (_isDragActive)
            {
                EndDrag();
                onHintMessage?.Invoke("🔒 观察/测量阶段不可拖动物体");
            }
            return;
        }

        if (Input.GetMouseButtonDown(0)) { TryBeginDrag(); return; }
        if (_isDragActive)
        {
            if (Input.GetMouseButton(0)) ContinueDrag();
            else EndDrag();
        }
    }

    // ══════════════════════════════════════════════
    //  拖拽核心（屏幕空间增量法）
    // ══════════════════════════════════════════════

    private void TryBeginDrag()// 尝试开始拖拽，命中一个器材且该器材在管理列表中才算成功开始拖拽
    {
        if (_cam == null) return;// 没有相机无法进行射线检测
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);//
        if (!Physics.Raycast(ray, out RaycastHit hit, 300f)) return;

        ExperimentItem item = hit.collider.GetComponentInParent<ExperimentItem>();
        if (item == null) return;

        bool managed = false;
        foreach (var i in _items) if (i == item) { managed = true; break; }
        if (!managed) return;

        _dragging = item;
        _isDragActive = true;
        _prevMouseScreen = Input.mousePosition;
        item.SetDragging(true);

        if (_validateCo != null) { StopCoroutine(_validateCo); _validateCo = null; }
    }

    private void ContinueDrag()
    {
        if (_dragging == null) return;

        // ── 当前修饰键决定拖拽模式
        _dragMode = Input.GetKey(KeyCode.LeftShift) ? DragMode.YOnly
                  : Input.GetKey(KeyCode.LeftControl) ? DragMode.XOnly
                  : DragMode.XZ;
        _debugDragMode = _dragMode.ToString();

        // ── 鼠标屏幕增量（像素）
        Vector2 currScreen = Input.mousePosition;
        Vector2 mouseDelta = currScreen - _prevMouseScreen;
        _prevMouseScreen = currScreen;

        // ── 世界单位/像素（透视矫正：相机越远，一像素对应越大的世界位移）
        float camDist = Vector3.Distance(_cam.transform.position, _dragging.transform.position);
        float worldPerPx = 2f * camDist * Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f) / Screen.height;

        Vector3 newPos = _dragging.transform.position;

        // ── XZ 平面移动（左键默认）
        if (_dragMode == DragMode.XZ)
        {
            // 相机右/前方向投影到水平面（去掉 Y 分量）
            Vector3 camRight = _cam.transform.right; camRight.y = 0f;
            Vector3 camForward = _cam.transform.forward; camForward.y = 0f;

            // 极端俯视时 XZ 分量接近零，退化为世界轴向
            if (camRight.sqrMagnitude < 0.01f) { camRight = Vector3.right; }
            else { camRight.Normalize(); }
            if (camForward.sqrMagnitude < 0.01f) { camForward = Vector3.forward; }
            else { camForward.Normalize(); }

            newPos += (camRight * mouseDelta.x + camForward * mouseDelta.y)
                      * worldPerPx * xzSensitivity;

            // 滚轮调高度（XZ 模式下同时可用）
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                newPos.y += scroll * scrollYStep * (scroll > 0 ? 1 : -1);
        }
        // ── 纯 Y 轴（Shift）
        else if (_dragMode == DragMode.YOnly)
        {
            newPos.y += mouseDelta.y * worldPerPx * ySensitivity;
        }
        // ── 纯 X 轴（Ctrl）
        else if (_dragMode == DragMode.XOnly)
        {
            Vector3 camRight = _cam.transform.right; camRight.y = 0f;
            if (camRight.sqrMagnitude < 0.01f) camRight = Vector3.right;
            else camRight.Normalize();
            newPos += camRight * mouseDelta.x * worldPerPx * xzSensitivity;
        }

        // ── 限制到 3D 边界
        newPos = ClampToBounds(newPos);

        // ── 排斥（防止与其他器材在 Z 轴穿透）
        newPos = ResolveCollisions(_dragging, newPos);

        _dragging.transform.position = newPos;
        _debugDragPos = newPos;

        // ── 高度导引线更新
        _dragging.UpdateHeightGuide(opticalAxisY, heightTolerance);

        // ── 磁吸提示（靠近时变绿）
        if (enableSnapAssist && _snapTargets.TryGetValue(_dragging, out Vector3 snapPos))
            _dragging.SetSnapHint(Vector3.Distance(newPos, snapPos) < snapRadius);
    }

    private void EndDrag()
    {
        if (_dragging == null) { _isDragActive = false; return; }

        ExperimentItem released = _dragging;
        _dragging = null;
        _isDragActive = false;

        bool isSnapped = false;
        if (enableSnapAssist && _snapTargets.TryGetValue(released, out Vector3 snapPos))
        {
            float dist = Vector3.Distance(released.transform.position, snapPos);
            Debug.Log($"[EndDrag] {released.displayName} 距离推荐位 {dist:F3}m (阈值{snapRadius})");
            if (dist < snapRadius)
            {
                released.transform.position = snapPos;
                onHintMessage?.Invoke($"✔ {released.displayName} 已吸附到推荐位置");
                isSnapped = true;
            }
        }

        released.SetDragging(false);

        if (enableStepGuide && !isSnapped)
            PostDropGuide(released);

        if (_validateCo != null) StopCoroutine(_validateCo);
        _validateCo = StartCoroutine(DeferredValidate());
        Debug.Log($"[EndDrag] 释放 {released.displayName}, 吸附={isSnapped}, 将在0.12s后验证");
    }

    // ══════════════════════════════════════════════
    //  边界 & 碰撞
    // ══════════════════════════════════════════════

    private Vector3 ClampToBounds(Vector3 p) => new Vector3(
        Mathf.Clamp(p.x, boundsMin.x, boundsMax.x),
        Mathf.Clamp(p.y, boundsMin.y, boundsMax.y),
        Mathf.Clamp(p.z, boundsMin.z, boundsMax.z)
    );

    /// <summary>
    /// 排斥：防止器材在光具座轴向（Z）上穿透。
    /// X/Y 不做排斥（可以在不同高度"错位"放置）。
    /// </summary>
    private Vector3 ResolveCollisions(ExperimentItem moving, Vector3 desiredPos)
    {
        float desiredT = GetBenchT(desiredPos);

        for (int iter = 0; iter < 3; iter++)
        {
            bool changed = false;
            foreach (ExperimentItem other in _items)
            {
                if (other == null || other == moving) continue;
                float otherT = GetBenchT(other.transform.position);
                float diff = desiredT - otherT;

                if (Mathf.Abs(diff) < minSpacing)
                {
                    float sign = diff >= 0f ? 1f : -1f;
                    desiredT = otherT + sign * minSpacing;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        // 把修正后的 T 值写回位置（只改 bench 轴向分量，X/Y 保持原值）
        float correctedT = Mathf.Clamp(desiredT, BenchTFromWorld(boundsMin), BenchTFromWorld(boundsMax));
        Vector3 benchDir = BenchDir;
        Vector3 origin = BenchOrigin;
        // 在目标位置上沿 bench 轴方向做修正
        float origT = GetBenchT(desiredPos);
        desiredPos += benchDir * (correctedT - origT);
        return desiredPos;
    }

    // ══════════════════════════════════════════════
    //  验证
    // ══════════════════════════════════════════════

    private IEnumerator DeferredValidate()
    {
        yield return new WaitForSeconds(0.12f);
        Debug.Log("[DeferredValidate] 开始验证...");
        var result = ValidateSetup();
        Debug.Log($"[DeferredValidate] 验证结果: correct={result.isCorrect}, errors={result.errors.Count}");
        if (!result.isCorrect) foreach (var e in result.errors) Debug.Log($"  {e}");
        RefreshDebugPos();
        _validateCo = null;
    }

    public ValidationResult ValidateSetup()
    {
        var result = new ValidationResult();
        if (!AllAssigned()) { result.AddError("存在未指定的器材引用"); return result; }

        float lT = GetBenchT(lightSource.transform.position);
        float ssT = GetBenchT(singleSlit.transform.position);
        float dsT = GetBenchT(doubleSlit.transform.position);
        float scT = GetBenchT(screen.transform.position);

        // 1. 顺序
        if (lT >= ssT - orderMinGap) result.AddError($"❌ {lightSource.displayName} 需排在 {singleSlit.displayName} 前方");
        if (ssT >= dsT - orderMinGap) result.AddError($"❌ {singleSlit.displayName} 需排在 {doubleSlit.displayName} 前方");
        if (dsT >= scT - orderMinGap) result.AddError($"❌ {doubleSlit.displayName} 需排在 {screen.displayName} 前方");

        // 2. 光轴 Y 对齐
        foreach (ExperimentItem item in _items)
        {
            float dy = Mathf.Abs(item.transform.position.y - opticalAxisY);
            if (dy > heightTolerance)
                result.AddError($"❌ {item.displayName} 高度偏差 {dy * 100f:F1}cm（需与光轴对齐 Y={opticalAxisY:F2}）");
        }

        // 3. 光轴 X 对齐
        foreach (ExperimentItem item in _items)
        {
            float dx = Mathf.Abs(item.transform.position.x - opticalAxisX);
            if (dx > xAlignTolerance)
                result.AddError($"❌ {item.displayName} 横向偏差 {dx * 100f:F1}cm（需对准光轴中心 X={opticalAxisX:F2}）");
        }

        // 4. 边界检查
        foreach (ExperimentItem item in _items)
        {
            Vector3 p = item.transform.position;
            if (p.x < boundsMin.x || p.x > boundsMax.x ||
                p.y < boundsMin.y || p.y > boundsMax.y ||
                p.z < boundsMin.z || p.z > boundsMax.z)
                result.AddError($"❌ {item.displayName} 超出实验台范围");
        }

        result.isCorrect = result.errors.Count == 0;

        if (!result.isCorrect)
        {
            Debug.Log("[ValidateSetup] 逐项偏差检查:");
            foreach (var it in _items) { if (it != null) LogAxisDeviation(it); }
        }

        if (result.isCorrect)
        {
            onExperimentCorrect?.Invoke();
            onHintMessage?.Invoke("✅ 放置正确！可以开始观察双缝干涉条纹。");
        }
        else
        {
            onExperimentIncorrect?.Invoke();
            onHintMessage?.Invoke(result.errors[0]);
        }
        return result;
    }

    private void LogAxisDeviation(ExperimentItem item)
    {
        Vector3 pos = item.transform.position;
        float dx = Mathf.Abs(pos.x - opticalAxisX); bool xOk = dx <= xAlignTolerance;
        float dy = Mathf.Abs(pos.y - opticalAxisY); bool yOk = dy <= heightTolerance;
        bool bndX = pos.x >= boundsMin.x && pos.x <= boundsMax.x;
        bool bndY = pos.y >= boundsMin.y && pos.y <= boundsMax.y;
        bool bndZ = pos.z >= boundsMin.z && pos.z <= boundsMax.z;
        Debug.Log($"[AxisCheck] {item.displayName}: pos=({pos.x:F3},{pos.y:F3},{pos.z:F3}) " +
                  $"| Δx={dx:F3}(tol={xAlignTolerance}) {(xOk?"✓":"✗")} " +
                  $"Δy={dy:F3}(tol={heightTolerance}) {(yOk?"✓":"✗")} " +
                  $"Bounds:[X{(bndX?"✓":"✗")} Y{(bndY?"✓":"✗")} Z{(bndZ?"✓":"✗")}] " +
                  $"total={(xOk&&yOk&&bndX&&bndY&&bndZ?"PASS":"FAIL")}");
    }

    private bool IsItemOnOpticalAxis(ExperimentItem item)
    {
        Vector3 pos = item.transform.position;
        if (Mathf.Abs(pos.y - opticalAxisY) > heightTolerance) return false;
        if (Mathf.Abs(pos.x - opticalAxisX) > xAlignTolerance) return false;
        if (pos.x < boundsMin.x || pos.x > boundsMax.x ||
            pos.y < boundsMin.y || pos.y > boundsMax.y ||
            pos.z < boundsMin.z || pos.z > boundsMax.z) return false;
        return true;
    }

    // ══════════════════════════════════════════════
    //  放置后引导提示
    // ══════════════════════════════════════════════

    private void PostDropGuide(ExperimentItem released)
    {
        // 检查该器材的当前三轴偏差，给出针对性提示
        Vector3 pos = released.transform.position;
        float dy = Mathf.Abs(pos.y - opticalAxisY);
        float dx = Mathf.Abs(pos.x - opticalAxisX);

        if (dy > heightTolerance)
        {
            float diff = pos.y - opticalAxisY;
            onHintMessage?.Invoke($"↕ {released.displayName} 高度偏差 {dy * 100f:F1}cm " +
                                  (diff > 0 ? "（偏高，拖拽时滚轮向下）" : "（偏低，拖拽时滚轮向上）"));
            return;
        }
        if (dx > xAlignTolerance)
        {
            float diff = pos.x - opticalAxisX;
            onHintMessage?.Invoke($"↔ {released.displayName} 横向偏差 {dx * 100f:F1}cm " +
                                  (diff > 0 ? "（偏右，Ctrl拖拽向左）" : "（偏左，Ctrl拖拽向右）"));
            return;
        }

        // 顺序检查
        float prevT = float.MinValue;
        foreach (ExperimentItem item in _items)
        {
            if (item == null) continue;
            float t = GetBenchT(item.transform.position);
            if (t <= prevT + orderMinGap)
            {
                onHintMessage?.Invoke(item.itemType switch
                {
                    ExperimentItem.ApparatusType.LightSource => $"💡 {item.displayName} 需放到最前端",
                    ExperimentItem.ApparatusType.SingleSlit => $"🔲 {item.displayName} 需放到光源后方",
                    ExperimentItem.ApparatusType.DoubleSlit => $"🔳 {item.displayName} 需放到单缝后方",
                    ExperimentItem.ApparatusType.Screen => $"📺 {item.displayName} 需放到最末端",
                    _ => ""
                });
                return;
            }
            prevT = t;
        }
    }

    // ══════════════════════════════════════════════
    //  公开功能（UI 按钮绑定）
    // ══════════════════════════════════════════════

    /// <summary>一键自动对齐到推荐位置（完整 3D 对齐）</summary>
    public void AutoAlignAll()
    {
        if (_dragging != null) { _dragging.SetDragging(false); _dragging = null; _isDragActive = false; }
        foreach (ExperimentItem item in _items)
        {
            if (item == null || !_snapTargets.TryGetValue(item, out Vector3 p)) continue;
            item.transform.position = p;
            item.ClearHighlight();
        }
        onHintMessage?.Invoke("🔧 已自动对齐到推荐位置（光轴中心 + 推荐前后距离）");
        if (_validateCo != null) StopCoroutine(_validateCo);
        _validateCo = StartCoroutine(DeferredValidate());
    }

    public void ResetAll()
    {
        if (_dragging != null) { _dragging.SetDragging(false); _dragging = null; _isDragActive = false; }
        foreach (ExperimentItem it in _items) it?.ResetToHome();
        onHintMessage?.Invoke("🔄 已重置，请重新摆放器材");
    }

    public void TriggerValidate() => ValidateSetup();// 手动触发验证（UI 按钮绑定）

    public void SetSnapAssist(bool on)
    {
        enableSnapAssist = on;
        onHintMessage?.Invoke(on ? "磁吸辅助：开启" : "磁吸辅助：关闭");
    }

    /// <summary>沿光具座轴线计算两个器材之间的距离（米）</summary>
    public float GetDistanceAlongBench(ExperimentItem from, ExperimentItem to)
    {
        if (from == null || to == null) return 0f;
        return Mathf.Abs(GetBenchT(to.transform.position) - GetBenchT(from.transform.position));
    }

    // ══════════════════════════════════════════════
    //  光具座轴线工具
    // ══════════════════════════════════════════════

    public enum BenchAxisChoiceDup { Forward, Right, Up }   // 防止命名冲突

    private Vector3 BenchDir
    {
        get
        {
            if (benchTransform == null) return Vector3.forward;
            return benchAxis switch
            {
                BenchAxisChoice.Right => benchTransform.right,
                BenchAxisChoice.Up => benchTransform.up,
                _ => benchTransform.forward
            };
        }
    }

    private Vector3 BenchOrigin
    {
        get
        {
            if (benchTransform == null) return Vector3.zero;
            return benchTransform.position;
        }
    }

    /// <summary>世界坐标投影到光具座轴线，返回标量 T</summary>
    private float GetBenchT(Vector3 worldPos)
        => Vector3.Dot(worldPos - BenchOrigin, BenchDir);

    /// <summary>从世界坐标 boundsMin/Max 的角点反算 T 范围</summary>
    private float BenchTFromWorld(Vector3 corner)
        => GetBenchT(corner);

    private void BuildSnapTargets()// 构建器材到推荐位置的字典
    {
        _snapTargets = new Dictionary<ExperimentItem, Vector3>(4);// 注意：如果某个器材引用未指定，则不加入字典，避免后续使用时 NullReference 错误
        if (lightSource != null) _snapTargets[lightSource] = snapPosLight;
        if (singleSlit != null) _snapTargets[singleSlit] = snapPosSingle;
        if (doubleSlit != null) _snapTargets[doubleSlit] = snapPosDouble;
        if (screen != null) _snapTargets[screen] = snapPosScreen;
    }

    private bool AllAssigned()
    {
        foreach (var it in _items) if (it == null) return false;
        return true;
    }

    private void RefreshDebugPos()
    {
        if (!AllAssigned()) return;
        _debugLightPos = lightSource.transform.position;
        _debugSSPos = singleSlit.transform.position;
        _debugDSPos = doubleSlit.transform.position;
        _debugScrPos = screen.transform.position;
    }

    // ══════════════════════════════════════════════
    //  Gizmo
    // ══════════════════════════════════════════════

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // 3D 边界框（半透明线框）
        Vector3 center = (boundsMin + boundsMax) * 0.5f;
        Vector3 size = boundsMax - boundsMin;
        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Gizmos.DrawWireCube(center, size);

        // 光轴线（绿色水平线）
        Vector3 axisStart = new Vector3(boundsMin.x, opticalAxisY, boundsMin.z);
        Vector3 axisEnd = new Vector3(boundsMax.x, opticalAxisY, boundsMax.z);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.7f);
        Gizmos.DrawLine(axisStart, axisEnd);

        // 推荐位置小立方体
        (Vector3 pos, Color col)[] marks =
        {
            (snapPosLight,  Color.yellow),
            (snapPosSingle, Color.green),
            (snapPosDouble, Color.cyan),
            (snapPosScreen, Color.red),
        };
        foreach (var (p, c) in marks)
        {
            Gizmos.color = c;
            Gizmos.DrawWireCube(p, Vector3.one * 0.1f);
            // 磁吸半径圈
            Gizmos.color = new Color(c.r, c.g, c.b, 0.1f);
            Gizmos.DrawWireSphere(p, snapRadius);
        }

        // 光具座方向箭头（白色）
        if (benchTransform != null)
        {
            Gizmos.color = Color.white;
            Vector3 mid = center; mid.y = opticalAxisY;
            Gizmos.DrawRay(mid, BenchDir * 0.6f);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 光轴 X 中线（浅蓝色垂直面）
        Vector3 p0 = new Vector3(opticalAxisX, boundsMin.y, boundsMin.z);
        Vector3 p1 = new Vector3(opticalAxisX, boundsMax.y, boundsMin.z);
        Vector3 p2 = new Vector3(opticalAxisX, boundsMax.y, boundsMax.z);
        Vector3 p3 = new Vector3(opticalAxisX, boundsMin.y, boundsMax.z);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.08f);
        Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
    }
#endif
}

// ════════════════════════════════════════════════
//  验证结果
// ════════════════════════════════════════════════

public class ValidationResult
{
    public bool isCorrect;
    public System.Collections.Generic.List<string> errors = new(8);
    public void AddError(string m) => errors.Add(m);
    public bool IsItemInError(string name)
    { foreach (var e in errors) if (e.Contains(name)) return true; return false; }
    public override string ToString()
        => isCorrect ? "✅ 放置正确" : string.Join("\n", errors);
}