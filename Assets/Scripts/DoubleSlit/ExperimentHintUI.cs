using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 实验交互 UI 管理器
/// ─ 功能：
///   1. 在屏幕上显示实时操作提示和验证结果
///   2. 步骤进度指示器（4 个器材的放置状态）
///   3. 控制按钮：自动对齐、重置、检查
///   4. 操作提示淡入淡出动画
/// ─ 使用方式：
///   在 Canvas 下创建 UI 结构后，将此脚本挂载到 Canvas 根节点
///   在 Inspector 中绑定对应 UI 组件引用
/// </summary>
public class ExperimentHintUI : MonoBehaviour
{
    // ══════════════════════════════════════════════
    //  Inspector 字段
    // ══════════════════════════════════════════════

    [Header("── 系统引用 ──")]
    [Tooltip("ExperimentBenchManager 所在 GameObject")]
    public ExperimentBenchManager benchManager;

    [Header("── 提示文本 ──")]
    [Tooltip("屏幕底部的操作提示文本框（TextMeshPro）")]
    public TextMeshProUGUI hintText;

    [Tooltip("提示出现后多少秒自动淡出（0 = 不淡出）")]
    [Range(0f, 10f)]
    public float hintDisplayTime = 4f;

    [Tooltip("淡出动画时长")]
    [Range(0.2f, 2f)]
    public float hintFadeDuration = 0.6f;

    [Header("── 步骤进度 ──")]
    [Tooltip("4 个步骤图标（Image），对应光源/单缝/双缝/光屏")]
    public Image[] stepIcons;           // 长度需为 4

    [Tooltip("步骤标签文本（TextMeshPro），对应图标下的名称")]
    public TextMeshProUGUI[] stepLabels;

    [Tooltip("步骤未完成的颜色")]
    public Color stepPendingColor  = new Color(0.5f, 0.5f, 0.5f, 0.8f);

    [Tooltip("步骤完成后的颜色")]
    public Color stepCompleteColor = new Color(0.2f, 1f, 0.4f, 1f);

    [Tooltip("步骤出错时的颜色")]
    public Color stepErrorColor    = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("── 控制按钮 ──")]
    [Tooltip("「自动对齐」按钮")]
    public Button autoAlignButton;

    [Tooltip("「重置实验」按钮")]
    public Button resetButton;

    [Tooltip("「检查放置」按钮")]
    public Button validateButton;

    [Tooltip("「吸附辅助」开关 Toggle")]
    public Toggle snapToggle;

    [Header("── 验证结果面板 ──")]
    [Tooltip("验证结果显示面板（GameObject，验证后显示）")]
    public GameObject resultPanel;

    [Tooltip("验证结果标题文本")]
    public TextMeshProUGUI resultTitleText;

    [Tooltip("验证详情文本（列出各错误）")]
    public TextMeshProUGUI resultDetailText;

    [Tooltip("结果面板正确时的背景颜色")]
    public Color resultCorrectBg = new Color(0.1f, 0.5f, 0.1f, 0.85f);

    [Tooltip("结果面板错误时的背景颜色")]
    public Color resultErrorBg   = new Color(0.5f, 0.1f, 0.1f, 0.85f);

    [Tooltip("结果面板的 Image 组件（用于修改背景色）")]
    public Image resultPanelBg;

    // ══════════════════════════════════════════════
    //  私有字段
    // ══════════════════════════════════════════════

    private Coroutine _hintFadeCoroutine;
    private CanvasGroup _hintCanvasGroup;   // 用于淡入淡出，需要在 hintText 父节点上

    private static readonly string[] s_StepNames =
        { "光源", "单缝", "双缝", "光屏" };

    // ══════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════

    void Awake()
    {
        // 尝试获取 hintText 父节点上的 CanvasGroup
        if (hintText != null)
            _hintCanvasGroup = hintText.transform.parent?.GetComponent<CanvasGroup>();

        // 初始化步骤标签名称
        if (stepLabels != null)
            for (int i = 0; i < stepLabels.Length && i < s_StepNames.Length; i++)
                if (stepLabels[i] != null)
                    stepLabels[i].text = s_StepNames[i];

        // 隐藏结果面板
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    void Start()
    {
        // 绑定按钮事件
        if (autoAlignButton != null)
            autoAlignButton.onClick.AddListener(OnAutoAlign);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnReset);

        if (validateButton != null)
            validateButton.onClick.AddListener(OnValidate);

        if (snapToggle != null)
        {
            snapToggle.isOn = benchManager != null && benchManager.enableSnapAssist;
            snapToggle.onValueChanged.AddListener(OnSnapToggle);
        }

        // 订阅 Manager 的提示消息事件
        if (benchManager != null)
        {
            benchManager.onHintMessage.AddListener(ShowHint);
            benchManager.onExperimentCorrect.AddListener(OnExperimentCorrect);
            benchManager.onExperimentIncorrect.AddListener(OnExperimentIncorrect);
        }

        // 初始提示
        ShowHint("💡 请将实验器材按顺序摆放到光具座上，拖拽器材到合适位置");
        ResetStepIcons();
    }

    // ══════════════════════════════════════════════
    //  提示文字
    // ══════════════════════════════════════════════

    /// <summary>显示提示文字，带淡入效果</summary>
    public void ShowHint(string message)
    {
        if (hintText == null) return;
        hintText.text = message;

        // 打断上一个淡出协程
        if (_hintFadeCoroutine != null)
        {
            StopCoroutine(_hintFadeCoroutine);
            _hintFadeCoroutine = null;
        }

        if (_hintCanvasGroup != null)
        {
            _hintCanvasGroup.alpha = 1f;

            if (hintDisplayTime > 0f)
                _hintFadeCoroutine = StartCoroutine(FadeOutHint());
        }
    }

    private IEnumerator FadeOutHint()
    {
        yield return new WaitForSeconds(hintDisplayTime);

        float elapsed = 0f;
        while (elapsed < hintFadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_hintCanvasGroup != null)
                _hintCanvasGroup.alpha = 1f - (elapsed / hintFadeDuration);
            yield return null;
        }

        if (_hintCanvasGroup != null)
            _hintCanvasGroup.alpha = 0f;

        _hintFadeCoroutine = null;
    }

    // ══════════════════════════════════════════════
    //  步骤进度图标更新
    // ══════════════════════════════════════════════

    /// <summary>更新步骤图标状态（由外部或验证回调调用）</summary>
    public void UpdateStepIcon(int index, StepStatus status)
    {
        if (stepIcons == null || index < 0 || index >= stepIcons.Length) return;
        if (stepIcons[index] == null) return;

        stepIcons[index].color = status switch
        {
            StepStatus.Complete => stepCompleteColor,
            StepStatus.Error    => stepErrorColor,
            _                   => stepPendingColor
        };
    }

    /// <summary>全部重置为待完成状态</summary>
    public void ResetStepIcons()
    {
        if (stepIcons == null) return;
        for (int i = 0; i < stepIcons.Length; i++)
            UpdateStepIcon(i, StepStatus.Pending);
    }

    public enum StepStatus { Pending, Complete, Error }

    // ══════════════════════════════════════════════
    //  验证结果面板
    // ══════════════════════════════════════════════

    private void OnExperimentCorrect()
    {
        ShowResultPanel(true, "✅ 放置正确！", "实验器材顺序和位置均正确\n请观察光屏上的双缝干涉条纹");

        // 步骤全部标绿
        for (int i = 0; i < 4; i++)
            UpdateStepIcon(i, StepStatus.Complete);
    }

    private void OnExperimentIncorrect()
    {
        // 结果面板由 ShowResultPanel 统一调用
        // 此处仅做步骤图标更新（需要从 Manager 获取细节，简化处理）
    }

    /// <summary>显示或刷新验证结果面板</summary>
    public void ShowResultPanel(bool correct, string title, string detail)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (resultTitleText != null)  resultTitleText.text = title;
        if (resultDetailText != null) resultDetailText.text = detail;

        if (resultPanelBg != null)
            resultPanelBg.color = correct ? resultCorrectBg : resultErrorBg;
    }

    public void HideResultPanel()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════
    //  按钮回调
    // ══════════════════════════════════════════════

    private void OnAutoAlign()
    {
        if (benchManager == null) return;
        HideResultPanel();
        benchManager.AutoAlignAll();
    }

    private void OnReset()
    {
        if (benchManager == null) return;
        HideResultPanel();
        ResetStepIcons();
        benchManager.ResetAll();
    }

    private void OnValidate()
    {
        if (benchManager == null) return;

        ValidationResult result = benchManager.ValidateSetup();

        if (result.isCorrect)
        {
            ShowResultPanel(true, "✅ 放置正确！", "所有器材已正确放置。\n可以开始实验！");
        }
        else
        {
            string detail = string.Join("\n", result.errors);
            ShowResultPanel(false, "⚠ 放置有误", detail);

            // 更新步骤图标
            ResetStepIcons();
            string[] names = { lightSourceName, singleSlitName, doubleSlitName, screenName };
            for (int i = 0; i < names.Length; i++)
                UpdateStepIcon(i,
                    result.IsItemInError(names[i]) ? StepStatus.Error : StepStatus.Complete);
        }
    }

    private void OnSnapToggle(bool value)
    {
        benchManager?.SetSnapAssist(value);
    }

    // ══════════════════════════════════════════════
    //  器材名称缓存（用于步骤图标匹配）
    // ══════════════════════════════════════════════

    private string lightSourceName => benchManager?.lightSource?.ChineseName() ?? "光源";
    private string singleSlitName  => benchManager?.singleSlit?.ChineseName()  ?? "单缝";
    private string doubleSlitName  => benchManager?.doubleSlit?.ChineseName()  ?? "双缝";
    private string screenName      => benchManager?.screen?.ChineseName()      ?? "光屏";
}
