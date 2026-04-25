using UnityEngine;

/// <summary>
/// 实验器材基础组件（3D 版）
/// 新增：垂直高度导引线（LineRenderer），帮助学生直观感知器材高度偏差
/// </summary>
[DisallowMultipleComponent]
public class ExperimentItem : MonoBehaviour
{
    public enum ApparatusType { LightSource = 0, SingleSlit = 1, DoubleSlit = 2, Screen = 3 }

    // ══════════════════════════════════════════════
    //  Inspector
    // ══════════════════════════════════════════════

    [Header("器材标识")]
    public ApparatusType itemType;
    public string displayName = "器材";

    [Header("视觉反馈颜色")]
    public Color draggingColor = new Color(1f, 0.85f, 0f);
    public Color snapHintColor = new Color(0f, 1f, 0.5f);
    public Color correctColor = new Color(0.2f, 1f, 0.2f);
    public Color errorColor = new Color(1f, 0.25f, 0.25f);

    [Header("渲染器（留空自动收集子节点）")]
    public Renderer[] targetRenderers;

    [Header("鼠标拾取碰撞体（留空自动获取）")]
    public Collider pickCollider;

    [Header("高度导引线（可选，留空则自动创建）")]
    [Tooltip("拖拽时显示从器材到光轴高度的垂直辅助线，帮助学生对准光轴")]
    public LineRenderer heightGuideLine;

    // ══════════════════════════════════════════════
    //  运行时状态（由 Manager 写入）
    // ══════════════════════════════════════════════

    [HideInInspector] public bool isDragging;
    [HideInInspector] public bool isNearSnap;
    [HideInInspector] public bool isCorrect;
    [HideInInspector] public Vector3 homePosition;

    // ══════════════════════════════════════════════
    //  私有字段
    // ══════════════════════════════════════════════

    private MaterialPropertyBlock[] _mpbs;
    static readonly int s_Emission = Shader.PropertyToID("_EmissionColor");// 假设使用的材质支持这些属性，实际项目中可能需要根据具体Shader调整
    static readonly int s_BaseCol = Shader.PropertyToID("_BaseColor");
    static readonly int s_Col = Shader.PropertyToID("_Color");

    private enum VS { None, Dragging, SnapHint, Correct, Error }
    private VS _curVS = VS.None;

    // ══════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════

    void Awake()
    {
        homePosition = transform.position;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        _mpbs = new MaterialPropertyBlock[targetRenderers.Length];
        for (int i = 0; i < _mpbs.Length; i++)
            _mpbs[i] = new MaterialPropertyBlock();

        if (pickCollider == null)
            pickCollider = GetComponentInChildren<Collider>();

        if (pickCollider == null)
            Debug.LogWarning($"[ExperimentItem] '{displayName}' 缺少 Collider！");

        // 自动创建高度导引线
        if (heightGuideLine == null)
            heightGuideLine = CreateHeightGuideLine();
    }

    void Start()
    {
        // 确保导引线初始时隐藏
        if (heightGuideLine != null)
            heightGuideLine.enabled = false;
    }

    // ══════════════════════════════════════════════
    //  公开接口
    // ══════════════════════════════════════════════

    public void SetDragging(bool active)
    {
        isDragging = active;
        isNearSnap = false;
        SetVS(active ? VS.Dragging : VS.None);
        if (heightGuideLine != null) heightGuideLine.enabled = active;
    }

    public void SetSnapHint(bool active)
    {
        if (!isDragging) return;
        isNearSnap = active;
        SetVS(active ? VS.SnapHint : VS.Dragging);
    }

    public void SetValidationResult(bool correct)
    {
        isCorrect = correct;
        if (!isDragging) SetVS(correct ? VS.Correct : VS.Error);
    }

    public void ClearHighlight()
    {
        isDragging = false;
        isNearSnap = false;
        if (heightGuideLine != null) heightGuideLine.enabled = false;
        SetVS(VS.None);
    }

    public void ResetToHome()
    {
        transform.position = homePosition;
        ClearHighlight();
    }

    /// <summary>
    /// 更新高度导引线：从器材当前位置画垂线到光轴高度
    /// colorByError: true=偏差大时变红，false=保持中性色
    /// </summary>
    public void UpdateHeightGuide(float opticalAxisY, float tolerance)
    {
        if (heightGuideLine == null || !isDragging) return;

        Vector3 top = transform.position;
        Vector3 bottom = new Vector3(top.x, opticalAxisY, top.z);
        heightGuideLine.SetPosition(0, top);
        heightGuideLine.SetPosition(1, bottom);

        float dy = Mathf.Abs(top.y - opticalAxisY);
        Color col = dy > tolerance
            ? Color.Lerp(Color.yellow, Color.red, (dy - tolerance) / tolerance)
            : new Color(0.5f, 1f, 0.5f, 0.8f);
        heightGuideLine.startColor = col;
        heightGuideLine.endColor = new Color(col.r, col.g, col.b, 0.3f);
    }

    // ══════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════

    private void SetVS(VS state)
    {
        if (_curVS == state) return;
        _curVS = state;
        Color emit = state switch
        {
            VS.Dragging => draggingColor * 0.7f,
            VS.SnapHint => snapHintColor * 0.9f,
            VS.Correct => correctColor * 0.5f,
            VS.Error => errorColor * 0.5f,
            _ => Color.black
        };
        Color baseCol = state switch
        {
            VS.Dragging => draggingColor,
            VS.SnapHint => snapHintColor,
            VS.Correct => correctColor,
            VS.Error => errorColor,
            _ => Color.white
        };
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;
            targetRenderers[i].GetPropertyBlock(_mpbs[i]);
            _mpbs[i].SetColor(s_Emission, emit);
            _mpbs[i].SetColor(s_BaseCol, baseCol);
            _mpbs[i].SetColor(s_Col, baseCol);
            targetRenderers[i].SetPropertyBlock(_mpbs[i]);
        }
    }

    private LineRenderer CreateHeightGuideLine()
    {
        var go = new GameObject("_HeightGuide");
        go.transform.SetParent(transform.parent); // 挂到同级，不随器材旋转
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.012f;
        lr.endWidth = 0.004f;
        lr.useWorldSpace = true;
        lr.receiveShadows = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 尝试使用粒子/加法混合材质，若无则用默认
        var mat = Resources.Load<Material>("Sprites/Default");
        if (mat != null) lr.material = mat;
        lr.enabled = false;
        return lr;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.35f,
            $"[{itemType}] {displayName}",
            new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 11 });
    }
#endif
}