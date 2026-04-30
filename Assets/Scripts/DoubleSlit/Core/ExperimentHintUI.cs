using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 实验交互 UI 管理器（3D 版）
/// 新增：实时坐标显示、拖拽模式指示、偏差数值面板
/// </summary>
[AddComponentMenu("DoubleSlit/Experiment Hint UI")]
public class ExperimentHintUI : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector
    // ══════════════════════════════════════════════

    [Header("── 核心引用 ──")]
    public ExperimentBenchManager benchManager;

    [Header("── 操作提示文字 ──")]
    public TextMeshProUGUI hintText;
    [Range(0f, 10f)] public float hintKeepTime = 4f;
    [Range(0.1f, 2f)] public float hintFadeTime = 0.5f;

    [Header("── 拖拽模式指示器 ──")]
    [Tooltip("显示当前拖拽模式的文本（XZ / Y轴 / X轴）")]
    public TextMeshProUGUI dragModeText;

    [Tooltip("操作说明标签（常驻，不淡出）")]
    public TextMeshProUGUI controlHintText;

    [Header("── 实时坐标面板（拖拽时显示）──")]
    [Tooltip("拖拽时弹出的坐标面板根节点")]
    public GameObject coordPanel;
    [Tooltip("坐标文本：显示 X / Y / Z 当前值和偏差")]
    public TextMeshProUGUI coordText;

    [Header("── 步骤图标（4个，顺序：光源/单缝/双缝/光屏）──")]
    public Image[] stepIcons;
    public TextMeshProUGUI[] stepLabels;
    public Color stepPendingColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color stepCompleteColor = new Color(0.2f, 1f, 0.35f, 1f);
    public Color stepErrorColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("── 控制按钮 ──")]
    public Button autoAlignButton;
    public Button resetButton;
    public Button validateButton;
    public Toggle snapToggle;

    [Header("── 验证结果面板 ──")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultTitle;
    public TextMeshProUGUI resultDetail;
    public Image resultPanelBg;
    public Button resultCloseButton;
    public Color resultCorrectColor = new Color(0.1f, 0.45f, 0.1f, 0.88f);
    public Color resultErrorColor = new Color(0.45f, 0.1f, 0.1f, 0.88f);

    // ══════════════════════════════════════════════
    //  私有
    // ══════════════════════════════════════════════

    private Coroutine _fadeCo;
    private CanvasGroup _hintCG;
    private ValidationResult _lastResult;

    private static readonly string[] s_Names = { "光源", "透镜", "单缝", "双缝", "光屏" };

    // 坐标面板更新节流（0.06s 刷新一次，不必每帧）
    private float _coordUpdateTimer;
    private const float COORD_INTERVAL = 0.06f;

    // ══════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════

    void Awake()
    {
        if (hintText != null)
            _hintCG = hintText.transform.parent?.GetComponent<CanvasGroup>();

        // 步骤标签初始化
        if (stepLabels != null)
            for (int i = 0; i < stepLabels.Length && i < s_Names.Length; i++)
                if (stepLabels[i] != null) stepLabels[i].text = s_Names[i];

        if (resultPanel != null) resultPanel.SetActive(false);
        if (coordPanel != null) coordPanel.SetActive(false);
        if (dragModeText != null) dragModeText.text = "";
    }

    void Start()
    {
        // 绑定按钮
        autoAlignButton?.onClick.AddListener(OnClickAutoAlign);
        resetButton?.onClick.AddListener(OnClickReset);
        validateButton?.onClick.AddListener(OnClickValidate);
        resultCloseButton?.onClick.AddListener(() => resultPanel?.SetActive(false));

        if (snapToggle != null)
        {
            snapToggle.isOn = benchManager != null && benchManager.enableSnapAssist;
            snapToggle.onValueChanged.AddListener(v => benchManager?.SetSnapAssist(v));
        }

        // 订阅事件
        if (benchManager != null)
        {
            benchManager.onHintMessage.AddListener(ShowHint);
            benchManager.onExperimentCorrect.AddListener(OnCorrect);
            benchManager.onExperimentIncorrect.AddListener(OnIncorrect);
        }

        ResetStepIcons();

        // 常驻操作说明
        if (controlHintText != null)
            controlHintText.text =
                "🖱 拖拽 = XZ移动  |  滚轮 = 升降\n" +
                "Shift+拖拽 = 纯升降  |  Ctrl+拖拽 = 纯左右";
    }

    void Update()
    {
        UpdateCoordPanel();
        UpdateDragModeText();
    }

    // ══════════════════════════════════════════════
    //  实时坐标面板
    // ══════════════════════════════════════════════

    private void UpdateCoordPanel()
    {
        if (coordPanel == null || benchManager == null) return;

        bool anyDragging = false;
        ExperimentItem dragItem = null;
        foreach (var field in new[] { benchManager.lightSource, benchManager.singleSlit,
                                      benchManager.doubleSlit, benchManager.lens,
                                      benchManager.screen })
        {
            if (field != null && field.isDragging) { anyDragging = true; dragItem = field; break; }
        }

        coordPanel.SetActive(anyDragging);
        if (!anyDragging || dragItem == null) return;

        _coordUpdateTimer -= Time.deltaTime;
        if (_coordUpdateTimer > 0f) return;
        _coordUpdateTimer = COORD_INTERVAL;

        Vector3 p = dragItem.transform.position;
        float dy = p.y - benchManager.opticalAxisY;
        float dx = p.x - benchManager.opticalAxisX;

        string dyStr = ColorDeviation(dy, benchManager.heightTolerance, "cm", 100f);
        string dxStr = ColorDeviation(dx, benchManager.xAlignTolerance, "cm", 100f);

        if (coordText != null)
            coordText.text =
                $"<b>{dragItem.displayName}</b>\n" +
                $"X: {p.x:F3}m  偏差 {dxStr}\n" +
                $"Y: {p.y:F3}m  偏差 {dyStr}\n" +
                $"Z: {p.z:F3}m";
    }

    /// <summary>根据偏差大小返回带颜色标签的字符串</summary>
    private string ColorDeviation(float dev, float tolerance, string unit, float scale)
    {
        float abs = Mathf.Abs(dev) * scale;
        string val = $"{(dev >= 0 ? "+" : "")}{dev * scale:F1}{unit}";
        string color = Mathf.Abs(dev) <= tolerance ? "#88FF88" : "#FF6666";
        return $"<color={color}>{val}</color>";
    }

    // ══════════════════════════════════════════════
    //  拖拽模式文字
    // ══════════════════════════════════════════════

    private void UpdateDragModeText()
    {
        if (dragModeText == null) return;

        bool dragging = false;
        if (benchManager != null)
            foreach (var item in new[] { benchManager.lightSource, benchManager.singleSlit,
                                         benchManager.doubleSlit, benchManager.lens,
                                         benchManager.screen })
                if (item != null && item.isDragging) { dragging = true; break; }

        if (!dragging) { dragModeText.text = ""; return; }

        if (Input.GetKey(KeyCode.LeftShift)) dragModeText.text = "↕ 高度调节模式 (Shift)";
        else if (Input.GetKey(KeyCode.LeftControl)) dragModeText.text = "↔ 横向对准模式 (Ctrl)";
        else dragModeText.text = "✥ XZ 平面移动";
    }

    // ══════════════════════════════════════════════
    //  提示文字淡入淡出
    // ══════════════════════════════════════════════

    public void ShowHint(string msg)
    {
        if (hintText == null) return;
        hintText.text = msg;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        if (_hintCG != null)
        {
            _hintCG.alpha = 1f;
            if (hintKeepTime > 0f) _fadeCo = StartCoroutine(FadeHint());
        }
    }

    private IEnumerator FadeHint()
    {
        yield return new WaitForSeconds(hintKeepTime);
        float t = 0f;
        while (t < hintFadeTime)
        {
            t += Time.deltaTime;
            if (_hintCG) _hintCG.alpha = 1f - t / hintFadeTime;
            yield return null;
        }
        if (_hintCG) _hintCG.alpha = 0f;
        _fadeCo = null;
    }

    // ══════════════════════════════════════════════
    //  步骤图标
    // ══════════════════════════════════════════════

    public enum StepStatus { Pending, Complete, Error }

    public void SetStepStatus(int i, StepStatus s)
    {
        if (stepIcons == null || i < 0 || i >= stepIcons.Length || stepIcons[i] == null) return;
        stepIcons[i].color = s switch
        {
            StepStatus.Complete => stepCompleteColor,
            StepStatus.Error => stepErrorColor,
            _ => stepPendingColor
        };
    }

    public void ResetStepIcons()
    {
        if (stepIcons == null) return;
        for (int i = 0; i < stepIcons.Length; i++) SetStepStatus(i, StepStatus.Pending);
    }

    // ══════════════════════════════════════════════
    //  结果面板
    // ══════════════════════════════════════════════

    private void ShowResultPanel(ValidationResult r)
    {
        if (resultPanel == null || r == null) return;
        resultPanel.SetActive(true);

        bool ok = r.isCorrect;
        if (resultPanelBg) resultPanelBg.color = ok ? resultCorrectColor : resultErrorColor;
        if (resultTitle) resultTitle.text = ok ? "✅ 放置正确！" : "⚠ 放置有误";
        if (resultDetail)
            resultDetail.text = ok
                ? "所有器材已对准光轴（高度 + 横向），顺序正确。\n开始实验！"
                : string.Join("\n", r.errors);

        // 步骤图标
        ExperimentItem[] items = { benchManager?.lightSource, benchManager?.lens,
                                   benchManager?.singleSlit,  benchManager?.doubleSlit,
                                   benchManager?.screen };
        for (int i = 0; i < 5; i++)
            SetStepStatus(i, ok ? StepStatus.Complete
                : (r.IsItemInError(items[i]?.displayName ?? "") ? StepStatus.Error : StepStatus.Complete));
    }

    // ══════════════════════════════════════════════
    //  事件回调
    // ══════════════════════════════════════════════

    private void OnCorrect() { for (int i = 0; i < 5; i++) SetStepStatus(i, StepStatus.Complete); }
    private void OnIncorrect() { if (_lastResult != null) ShowResultPanel(_lastResult); }

    // ══════════════════════════════════════════════
    //  按钮
    // ══════════════════════════════════════════════

    private void OnClickAutoAlign()
    {
        resultPanel?.SetActive(false);
        ResetStepIcons();
        benchManager?.AutoAlignAll();
    }

    private void OnClickReset()
    {
        resultPanel?.SetActive(false);
        ResetStepIcons();
        benchManager?.ResetAll();
    }

    private void OnClickValidate()
    {
        if (benchManager == null) return;
        _lastResult = benchManager.ValidateSetup();
        ShowResultPanel(_lastResult);
    }
}