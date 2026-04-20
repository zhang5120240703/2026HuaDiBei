using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 实验台交互总控制器
/// ─ 功能：
///   1. 鼠标拖拽器材（沿光具座轴向限制移动）
///   2. 智能吸附辅助（靠近推荐位置自动吸附）
///   3. 器材放置验证（位置顺序、光轴高度、间距合理性）
///   4. 引导提示（高亮下一个需要放置的器材）
///   5. 自动对齐 / 复位功能
/// ─ 使用方式：
///   在场景中创建空 GameObject，挂载此脚本
///   在 Inspector 中分别指定 4 个器材和光具座引用
/// </summary>
public class ExperimentBenchManager : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector 字段
    // ══════════════════════════════════════════════

    [Header("── 实验器材引用 ──")]
    public ExperimentItem lightSource;
    public ExperimentItem singleSlit;
    public ExperimentItem doubleSlit;
    public ExperimentItem screen;

    [Header("── 光具座设置 ──")]
    [Tooltip("光具座的 Transform（器材沿其 +Z 轴方向排列）")]
    public Transform benchTransform;

    [Tooltip("器材在光具座上的世界 Y 高度（光轴高度）")]
    public float benchItemY = 1.05f;

    [Tooltip("光具座可用范围：从起点到终点的本地 Z 坐标（通常负值→正值）")]
    public float benchZMin = -2.8f;
    public float benchZMax =  2.8f;

    [Header("── 移动辅助 ──")]
    [Tooltip("相邻器材的最小间距（米），防止重叠")]
    public float minItemSpacing = 0.25f;

    [Tooltip("开启吸附辅助（靠近推荐位置时自动磁吸）")]
    public bool enableSnapAssist = true;

    [Tooltip("触发磁吸的半径范围（米）")]
    [Range(0.1f, 1.5f)]
    public float snapRadius = 0.6f;

    [Header("── 推荐位置（光具座本地 Z 坐标）──")]
    [Tooltip("推荐位置仅作辅助，不强制")]
    public float recommendedLightZ      = -2.2f;
    public float recommendedSingleSlitZ = -1.0f;
    public float recommendedDoubleSlitZ = -0.1f;
    public float recommendedScreenZ     =  1.8f;

    [Header("── 验证参数 ──")]
    [Tooltip("高度容差：Y 偏差超过此值视为高度未对齐（米）")]
    public float heightTolerance = 0.08f;

    [Tooltip("顺序容差：Z 差值需大于此值才认为顺序正确（米）")]
    public float orderMinGap = 0.15f;

    [Header("── 引导高亮 ──")]
    [Tooltip("开启「下一步提示」：自动高亮未正确放置的第一个器材")]
    public bool enableStepGuide = true;

    [Header("── 事件回调 ──")]
    public UnityEvent onExperimentCorrect;
    public UnityEvent onExperimentIncorrect;
    public UnityEvent<string> onHintMessage;   // 传递提示文字

    // ══════════════════════════════════════════════
    //  私有字段
    // ══════════════════════════════════════════════

    private Camera          _mainCam;
    private ExperimentItem  _dragging;           // 当前拖拽的器材
    private Plane           _dragPlane;          // 水平拖拽平面（Y 固定）
    private float           _dragLocalZOffset;   // 抓取时的局部 Z 偏移（防跳变）
    private bool            _hasValidated;

    // 4 个器材按顺序排列，便于循环处理
    private ExperimentItem[] _items;

    // 推荐吸附位置（世界坐标，Awake 时计算）
    private Dictionary<ExperimentItem, Vector3> _snapTargets;

    // 验证防抖：放开鼠标后延迟 0.1s 再验证，避免频繁触发
    private Coroutine _validateCoroutine;

    // 上一帧的鼠标位置（用于性能剔除）
    private Vector3 _lastMousePos;

    // ══════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════

    void Awake()
    {
        _mainCam = Camera.main;

        _items = new ExperimentItem[]
        {
            lightSource, singleSlit, doubleSlit, screen
        };

        // 验证引用完整性
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] == null)
                Debug.LogError($"[BenchManager] 器材引用缺失！请在 Inspector 中指定所有 4 个器材。");
        }

        if (benchTransform == null)
            Debug.LogError("[BenchManager] 未指定 benchTransform（光具座），器材将无法约束到光轴！");

        BuildSnapTargets();
    }

    void Start()
    {
        // 初始化引导提示
        if (enableStepGuide)
            StartCoroutine(DelayedGuideUpdate());
    }

    // ★ Update 仅在有鼠标输入时做实质工作
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDrag();
            return;
        }

        if (_dragging != null)
        {
            if (Input.GetMouseButton(0))
            {
                // 性能优化：鼠标未移动则跳过
                if (Input.mousePosition != _lastMousePos)
                {
                    _lastMousePos = Input.mousePosition;
                    ContinueDrag();
                }
            }
            else
            {
                // 鼠标松开
                EndDrag();
            }
        }
    }

    // ══════════════════════════════════════════════
    //  拖拽逻辑
    // ══════════════════════════════════════════════

    private void TryBeginDrag()
    {
        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;

        // 向上查找 ExperimentItem 组件（支持模型有多层子节点）
        ExperimentItem item = hit.collider.GetComponentInParent<ExperimentItem>();
        if (item == null) return;

        // 确认是受管理的器材
        bool managed = false;
        foreach (var i in _items) if (i == item) { managed = true; break; }
        if (!managed) return;

        BeginDrag(item);
    }

    private void BeginDrag(ExperimentItem item)
    {
        _dragging = item;
        item.SetDragging(true);

        // 拖拽平面：水平面，Y = 光轴高度
        _dragPlane = new Plane(Vector3.up, Vector3.up * benchItemY);

        // 记录抓取时的 Z 偏移，防止器材跳变到鼠标正下方
        Ray r = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (_dragPlane.Raycast(r, out float enter))
        {
            Vector3 hit = r.GetPoint(enter);
            if (benchTransform != null)
            {
                float hitLocalZ = benchTransform.InverseTransformPoint(hit).z;
                float itemLocalZ = benchTransform.InverseTransformPoint(item.transform.position).z;
                _dragLocalZOffset = itemLocalZ - hitLocalZ;
            }
        }

        _hasValidated = false;
        _lastMousePos = Input.mousePosition;

        // 打断验证协程
        if (_validateCoroutine != null)
        {
            StopCoroutine(_validateCoroutine);
            _validateCoroutine = null;
        }
    }

    private void ContinueDrag()
    {
        if (_dragging == null) return;

        Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        if (!_dragPlane.Raycast(ray, out float enter)) return;

        Vector3 worldHit = ray.GetPoint(enter);

        // 转到光具座本地坐标，限制只能沿 Z 轴（光轴方向）移动
        float targetLocalZ;
        if (benchTransform != null)
        {
            float rawLocalZ = benchTransform.InverseTransformPoint(worldHit).z;
            targetLocalZ = rawLocalZ + _dragLocalZOffset;
        }
        else
        {
            targetLocalZ = worldHit.z + _dragLocalZOffset;
        }

        // 限制到台面范围
        targetLocalZ = Mathf.Clamp(targetLocalZ, benchZMin, benchZMax);

        // 与其他器材做排斥（防止穿透）
        targetLocalZ = ResolveItemCollisions(_dragging, targetLocalZ);

        // 计算世界坐标（X=0，Y=固定高度）
        Vector3 newPos;
        if (benchTransform != null)
            newPos = benchTransform.TransformPoint(new Vector3(0f, 0f, targetLocalZ));
        else
            newPos = new Vector3(_dragging.transform.position.x, benchItemY, targetLocalZ);
        newPos.y = benchItemY;

        _dragging.transform.position = newPos;

        // 检测是否靠近推荐吸附点
        if (enableSnapAssist && _snapTargets.TryGetValue(_dragging, out Vector3 snapTarget))
        {
            float dist = Mathf.Abs(newPos.z - snapTarget.z); // 仅比较沿轴距离
            _dragging.SetSnapHint(dist < snapRadius);
        }
    }

    private void EndDrag()
    {
        if (_dragging == null) return;

        // 如果靠近推荐位置，执行磁吸
        if (enableSnapAssist && _snapTargets.TryGetValue(_dragging, out Vector3 snapPos))
        {
            float dist = Vector3.Distance(_dragging.transform.position, snapPos);
            if (dist < snapRadius)
            {
                _dragging.transform.position = snapPos;
                onHintMessage?.Invoke($"✓ {_dragging.displayName} 已吸附到推荐位置");
            }
        }

        _dragging.SetDragging(false);
        ExperimentItem released = _dragging;
        _dragging = null;

        // 延迟验证（避免鼠标抖动导致连续触发）
        if (_validateCoroutine != null) StopCoroutine(_validateCoroutine);
        _validateCoroutine = StartCoroutine(DeferredValidate());

        // 更新引导提示
        if (enableStepGuide)
            StartCoroutine(DelayedGuideUpdate());
    }

    // ══════════════════════════════════════════════
    //  碰撞排斥（防止器材相互穿透）
    // ══════════════════════════════════════════════

    private float ResolveItemCollisions(ExperimentItem movingItem, float desiredLocalZ)
    {
        foreach (ExperimentItem other in _items)
        {
            if (other == null || other == movingItem) continue;

            float otherLocalZ = benchTransform != null
                ? benchTransform.InverseTransformPoint(other.transform.position).z
                : other.transform.position.z;

            float diff = desiredLocalZ - otherLocalZ;
            if (Mathf.Abs(diff) < minItemSpacing)
            {
                // 向被拖拽方向推开
                float push = minItemSpacing * Mathf.Sign(diff == 0f ? 1f : diff);
                desiredLocalZ = otherLocalZ + push;
            }
        }
        return desiredLocalZ;
    }

    // ══════════════════════════════════════════════
    //  验证逻辑
    // ══════════════════════════════════════════════

    private IEnumerator DeferredValidate()
    {
        yield return new WaitForSeconds(0.12f);
        ValidateSetup();
        _validateCoroutine = null;
    }

    /// <summary>
    /// 验证当前实验器材摆放是否正确
    /// 返回包含所有错误信息的 ValidationResult
    /// </summary>
    public ValidationResult ValidateSetup()
    {
        ValidationResult result = new ValidationResult();

        if (!AllItemsAssigned())
        {
            result.AddError("存在未指定的实验器材引用，请检查 Inspector");
            return result;
        }

        float lZ  = GetLocalZ(lightSource);
        float ssZ = GetLocalZ(singleSlit);
        float dsZ = GetLocalZ(doubleSlit);
        float scZ = GetLocalZ(screen);

        // ── 1. 顺序检查（光源 → 单缝 → 双缝 → 光屏）
        if (lZ >= ssZ - orderMinGap)
            result.AddError("❌ 光源需在单缝左侧（单缝应在光源后方）");
        if (ssZ >= dsZ - orderMinGap)
            result.AddError("❌ 单缝需在双缝左侧（双缝应在单缝后方）");
        if (dsZ >= scZ - orderMinGap)
            result.AddError("❌ 双缝需在光屏左侧（光屏应在双缝后方）");

        // ── 2. 高度对齐检查（所有器材 Y 坐标应接近）
        foreach (ExperimentItem item in _items)
        {
            float yDiff = Mathf.Abs(item.transform.position.y - benchItemY);
            if (yDiff > heightTolerance)
                result.AddError($"❌ {item.displayName} 高度偏差 {yDiff * 100f:F1}cm（需与光轴对齐）");
        }

        // ── 3. 范围检查（所有器材需在光具座有效范围内）
        foreach (ExperimentItem item in _items)
        {
            float z = GetLocalZ(item);
            if (z < benchZMin || z > benchZMax)
                result.AddError($"❌ {item.displayName} 超出光具座范围");
        }

        result.isCorrect = result.errors.Count == 0;

        // ── 视觉反馈
        ApplyValidationFeedback(result);

        _hasValidated = true;

        if (result.isCorrect)
        {
            onExperimentCorrect?.Invoke();
            onHintMessage?.Invoke("✅ 实验器材放置正确！可以开始观察干涉条纹。");
        }
        else
        {
            onExperimentIncorrect?.Invoke();
            if (result.errors.Count > 0)
                onHintMessage?.Invoke(result.errors[0]);
        }

        return result;
    }

    private void ApplyValidationFeedback(ValidationResult result)
    {
        if (result.isCorrect)
        {
            foreach (ExperimentItem item in _items)
                item.SetValidationResult(true);
        }
        else
        {
            // 分别检查每个器材是否参与了错误
            bool lCorrect  = !result.IsItemInError(lightSource.displayName);
            bool ssCorrect = !result.IsItemInError(singleSlit.displayName);
            bool dsCorrect = !result.IsItemInError(doubleSlit.displayName);
            bool scCorrect = !result.IsItemInError(screen.displayName);

            lightSource.SetValidationResult(lCorrect);
            singleSlit .SetValidationResult(ssCorrect);
            doubleSlit .SetValidationResult(dsCorrect);
            screen     .SetValidationResult(scCorrect);
        }
    }

    // ══════════════════════════════════════════════
    //  引导提示（高亮下一步需要操作的器材）
    // ══════════════════════════════════════════════

    private IEnumerator DelayedGuideUpdate()
    {
        yield return new WaitForSeconds(0.15f);
        UpdateStepGuide();
    }

    private void UpdateStepGuide()
    {
        if (!enableStepGuide) return;

        // 按顺序检测哪个器材最先「不在正确位置」
        ExperimentItem[] ordered = { lightSource, singleSlit, doubleSlit, screen };

        float prevZ = benchZMin - 1f;
        ExperimentItem firstWrong = null;

        for (int i = 0; i < ordered.Length; i++)
        {
            if (ordered[i] == null) continue;
            float z = GetLocalZ(ordered[i]);
            bool orderOK = (z > prevZ + orderMinGap);

            if (!orderOK || !IsInBenchRange(z))
            {
                firstWrong = ordered[i];
                break;
            }
            prevZ = z;
        }

        // 发送提示
        if (firstWrong != null)
        {
            string hint = GetItemPlacementHint(firstWrong);
            onHintMessage?.Invoke(hint);
        }
    }

    private string GetItemPlacementHint(ExperimentItem item)
    {
        return item.itemType switch
        {
            ExperimentItemType.LightSource =>
                $"💡 请将【{item.displayName}】放置在光具座最左端",
            ExperimentItemType.SingleSlit =>
                $"🔲 请将【{item.displayName}】放置在光源右侧",
            ExperimentItemType.DoubleSlit =>
                $"🔳 请将【{item.displayName}】放置在单缝右侧",
            ExperimentItemType.Screen =>
                $"📺 请将【{item.displayName}（光屏）】放置在最右端",
            _ => $"请调整【{item.displayName}】的位置"
        };
    }

    // ══════════════════════════════════════════════
    //  辅助功能（可绑定到 UI 按钮）
    // ══════════════════════════════════════════════

    /// <summary>
    /// 一键自动对齐：将所有器材移动到推荐位置
    /// 可绑定到「自动对齐」按钮
    /// </summary>
    public void AutoAlignAll()
    {
        foreach (ExperimentItem item in _items)
        {
            if (item == null) continue;
            if (_snapTargets.TryGetValue(item, out Vector3 target))
            {
                item.transform.position = target;
                item.ClearHighlight();
            }
        }

        onHintMessage?.Invoke("🔧 已自动对齐到推荐位置，请观察干涉图样。");

        if (_validateCoroutine != null) StopCoroutine(_validateCoroutine);
        _validateCoroutine = StartCoroutine(DeferredValidate());
    }

    /// <summary>
    /// 复位所有器材到实验开始前的位置
    /// 可绑定到「重置实验」按钮
    /// </summary>
    public void ResetAll()
    {
        if (_dragging != null)
        {
            _dragging.SetDragging(false);
            _dragging = null;
        }

        foreach (ExperimentItem item in _items)
            item?.ResetToHome();

        _hasValidated = false;
        onHintMessage?.Invoke("🔄 实验已重置，请重新摆放器材。");

        if (enableStepGuide)
            StartCoroutine(DelayedGuideUpdate());
    }

    /// <summary>
    /// 手动触发验证（可绑定到「检查」按钮）
    /// </summary>
    public void TriggerValidate()
    {
        ValidateSetup();
    }

    /// <summary>
    /// 切换吸附辅助开关（可绑定到 Toggle）
    /// </summary>
    public void SetSnapAssist(bool enabled)
    {
        enableSnapAssist = enabled;
        string state = enabled ? "开启" : "关闭";
        onHintMessage?.Invoke($"磁吸辅助已{state}");
    }

    // ══════════════════════════════════════════════
    //  内部工具
    // ══════════════════════════════════════════════

    private void BuildSnapTargets()
    {
        _snapTargets = new Dictionary<ExperimentItem, Vector3>(4);
        if (benchTransform == null) return;

        AddSnap(lightSource, recommendedLightZ);
        AddSnap(singleSlit,  recommendedSingleSlitZ);
        AddSnap(doubleSlit,  recommendedDoubleSlitZ);
        AddSnap(screen,      recommendedScreenZ);
    }

    private void AddSnap(ExperimentItem item, float localZ)
    {
        if (item == null) return;
        Vector3 worldPos = benchTransform.TransformPoint(new Vector3(0f, 0f, localZ));
        worldPos.y = benchItemY;
        _snapTargets[item] = worldPos;
    }

    private float GetLocalZ(ExperimentItem item)
    {
        if (benchTransform != null)
            return benchTransform.InverseTransformPoint(item.transform.position).z;
        return item.transform.position.z;
    }

    private bool IsInBenchRange(float localZ)
        => localZ >= benchZMin && localZ <= benchZMax;

    private bool AllItemsAssigned()
    {
        foreach (ExperimentItem item in _items)
            if (item == null) return false;
        return true;
    }

    // ══════════════════════════════════════════════
    //  Editor Gizmo
    // ══════════════════════════════════════════════

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (benchTransform == null) return;

        // 绘制光具座有效范围
        Vector3 start = benchTransform.TransformPoint(new Vector3(0, 0, benchZMin));
        Vector3 end   = benchTransform.TransformPoint(new Vector3(0, 0, benchZMax));
        start.y = end.y = benchItemY;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.05f);
        Gizmos.DrawWireSphere(end,   0.05f);

        // 绘制推荐位置标记
        if (_snapTargets == null) return;
        Color[] colors = { Color.yellow, Color.green, Color.blue, Color.red };
        int idx = 0;
        foreach (var kv in _snapTargets)
        {
            Gizmos.color = colors[idx % colors.Length];
            Gizmos.DrawWireCube(kv.Value, Vector3.one * 0.12f);
            idx++;
        }
    }

    void OnDrawGizmosSelected()
    {
        // 绘制吸附半径
        if (_snapTargets == null) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        foreach (var kv in _snapTargets)
            Gizmos.DrawWireSphere(kv.Value, snapRadius);
    }
#endif
}

// ══════════════════════════════════════════════════════
//  验证结果数据类
// ══════════════════════════════════════════════════════

public class ValidationResult
{
    public bool isCorrect;
    public List<string> errors = new List<string>(8);

    public void AddError(string msg) => errors.Add(msg);

    /// <summary>检查错误信息中是否包含指定器材名称</summary>
    public bool IsItemInError(string itemDisplayName)
    {
        foreach (string e in errors)
            if (e.Contains(itemDisplayName)) return true;
        return false;
    }

    public override string ToString()
    {
        if (isCorrect) return "✅ 放置正确";
        return string.Join("\n", errors);
    }
}
