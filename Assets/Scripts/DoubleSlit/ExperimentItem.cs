using UnityEngine;

/// <summary>
/// 实验器材类型枚举（同时表示正确摆放顺序：0→1→2→3）
/// </summary>
public enum ExperimentItemType
{
    LightSource = 0,   // 光源
    SingleSlit  = 1,   // 单缝
    DoubleSlit  = 2,   // 双缝
    Screen      = 3    // 光屏
}

/// <summary>
/// 挂载在每件可拖拽实验器材上的数据组件。
/// 负责：类型定义、高亮渲染（零GC）、放置状态记录。
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExperimentItem : MonoBehaviour
{
    [Header("器材设置")]
    public ExperimentItemType itemType;

    [Tooltip("拖拽时跟随鼠标的半透明预览物体（为空则自动克隆本物体）")]
    public GameObject ghostOverride;

    [Tooltip("需要高亮的渲染器列表（为空则自动查找子物体中的所有 Renderer）")]
    public Renderer[] highlightRenderers;

    [Tooltip("落入槽位后相对槽中心的本地位置偏移")]
    public Vector3 slotOffset = Vector3.zero;

    [Header("光轴高度")]
    [Tooltip("器材光轴距底座的高度（用于光轴对齐校验），单位：世界坐标")]
    public float opticalAxisHeight = 0.15f;

    [HideInInspector] public int   slotIndex = -1;
    [HideInInspector] public bool  isPlaced  = false;
    [HideInInspector] public Vector3 parkPos;

    public enum HL { None = 0, Hover = 1, Valid = 2, Error = 3, Placed = 4 }

    static readonly Color[] _hlColors = {
        Color.white,
        new Color(1.00f, 0.92f, 0.20f, 1f),
        new Color(0.25f, 1.00f, 0.40f, 1f),
        new Color(1.00f, 0.25f, 0.25f, 1f),
        new Color(0.25f, 0.85f, 1.00f, 1f),
    };
    static readonly float[] _hlEmitIntensity = { 0f, 0.35f, 0.50f, 0.55f, 0.30f };

    MaterialPropertyBlock _mpb;
    static readonly int _propColor = Shader.PropertyToID("_Color");
    static readonly int _propEmit  = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        parkPos = transform.position;
        _mpb    = new MaterialPropertyBlock();
        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public void SetHighlight(HL state)
    {
        if (highlightRenderers == null) return;
        int idx = (int)state;
        Color baseColor = _hlColors[idx];
        Color emitColor = baseColor * _hlEmitIntensity[idx];
        _mpb.SetColor(_propColor, baseColor);
        _mpb.SetColor(_propEmit,  emitColor);
        foreach (var r in highlightRenderers)
            if (r != null) r.SetPropertyBlock(_mpb);
    }

    public float GetOpticalAxisWorldY()
        => transform.position.y + slotOffset.y + opticalAxisHeight;

    /// <summary>器材显示名称（用于 UI 提示）</summary>
    public string displayName => ChineseName();

    /// <summary>设置拖拽状态并更新高亮</summary>
    public void SetDragging(bool isDragging)
    {
        SetHighlight(isDragging ? HL.Hover : (isPlaced ? HL.Placed : HL.None));
    }

    /// <summary>显示吸附提示（当鼠标靠近推荐位置时）</summary>
    public void SetSnapHint(bool show)
    {
        SetHighlight(show ? HL.Valid : (isPlaced ? HL.Placed : HL.None));
    }

    /// <summary>设置验证结果的高亮（成功/错误）</summary>
    public void SetValidationResult(bool isCorrect)
    {
        SetHighlight(isCorrect ? HL.Placed : HL.Error);
    }

    /// <summary>清除高亮效果</summary>
    public void ClearHighlight()
    {
        SetHighlight(HL.None);
    }

    /// <summary>重置器材回到初始停靠位置</summary>
    public void ResetToHome()
    {
        transform.position = parkPos;
        isPlaced = false;
        slotIndex = -1;
        ClearHighlight();
    }

    public string ChineseName() => itemType switch
    {
        ExperimentItemType.LightSource => "光源",
        ExperimentItemType.SingleSlit  => "单缝",
        ExperimentItemType.DoubleSlit  => "双缝",
        ExperimentItemType.Screen      => "光屏",
        _                              => "未知器材"
    };
}
