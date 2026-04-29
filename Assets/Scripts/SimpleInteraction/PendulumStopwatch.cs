using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单摆实验电子计时器（秒表功能）
/// 按钮点击状态循环：开始计时 → 停止计时 → 重置归零
/// 支持与重置管理器自动联动，实验重置时计时器自动归零
/// </summary>
public class PendulumStopwatch : MonoBehaviour
{
    [Header("=== UI组件绑定 ===")]
    [Tooltip("计时器的控制按钮（三次点击循环）")]
    public Button timerButton;

    [Tooltip("显示计时数值的文本（支持TMP或普通Text）")]
    public TMP_Text timerDisplayText;      // 优先使用TMP
    // 如果使用普通Text，请将 timerDisplayText 留空，并绑定下方字段
    public Text legacyTimerDisplayText;     // 备选：普通Text组件

    [Header("=== 计时器设置 ===")]
    [Tooltip("计时显示格式：00:00.00 (分:秒.百分秒)")]
    public bool showMinutes = true;         // 是否显示分钟部分
    public int displayDecimals = 2;         // 小数位数（秒后显示几位）

    [Header("=== 当前状态（只读）===")]
    [SerializeField] private TimerState currentState = TimerState.Idle;
    [SerializeField] private float currentTime = 0f;       // 当前累计秒数

    // 计时器状态枚举
    private enum TimerState
    {
        Idle,       // 已重置/未开始，显示0
        Running,    // 正在计时
        Paused      // 已停止（暂停）
    }

    // 避免多次绑定的标志
    private bool isInitialized = false;

    void Start()
    {
        InitializeTimer();
    }

    /// <summary>
    /// 初始化：绑定按钮事件、设置显示、自动关联重置管理器
    /// </summary>
    private void InitializeTimer()
    {
        if (isInitialized) return;

        // 绑定计时按钮事件
        if (timerButton == null)
        {
            Debug.LogError("电子计时器：未绑定控制按钮！");
            enabled = false;
            return;
        }
        timerButton.onClick.RemoveListener(OnTimerButtonClick); // 避免重复绑定
        timerButton.onClick.AddListener(OnTimerButtonClick);

        // 确保有显示组件
        if (timerDisplayText == null && legacyTimerDisplayText == null)
        {
            Debug.LogError("电子计时器：未绑定任何显示文本组件（TMP_Text 或 Text）");
            enabled = false;
            return;
        }

        // 初始显示
        UpdateDisplay(0f);

        // 自动与单摆实验重置管理器联动（可选，增强体验）
        AutoBindToResetManager();

        isInitialized = true;
        Debug.Log("电子计时器初始化完成，状态：Idle，时间：0");
    }

    /// <summary>
    /// 计时器按钮点击逻辑：状态循环
    /// Idle → Running（开始计时）
    /// Running → Paused（停止计时）
    /// Paused → Idle（重置归零）
    /// </summary>
    private void OnTimerButtonClick()
    {
        switch (currentState)
        {
            case TimerState.Idle:
                // 开始计时
                currentState = TimerState.Running;
                Debug.Log("计时器：开始计时");
                break;

            case TimerState.Running:
                // 停止计时
                currentState = TimerState.Paused;
                Debug.Log($"计时器：停止计时 (当前时间 = {currentTime:F2}秒)");
                break;

            case TimerState.Paused:
                // 重置归零
                ResetStopwatch();
                Debug.Log("计时器：重置归零");
                break;
        }
    }

    void Update()
    {
        // 只有在 Running 状态下才累加时间
        if (currentState == TimerState.Running)
        {
            currentTime += Time.deltaTime;
            UpdateDisplay(currentTime);
        }
    }

    /// <summary>
    /// 更新时间显示（支持两种Text组件，优先TMP）
    /// </summary>
    private void UpdateDisplay(float timeInSeconds)
    {
        string formattedTime = FormatTime(timeInSeconds);

        if (timerDisplayText != null)
            timerDisplayText.text = formattedTime;
        else if (legacyTimerDisplayText != null)
            legacyTimerDisplayText.text = formattedTime;
    }

    /// <summary>
    /// 格式化时间字符串，例如 "01:23.45" 或 "123.45"
    /// </summary>
    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int minutes = totalSeconds / 60;
        float remainingSeconds = seconds - totalSeconds;
        int secPart = totalSeconds % 60;
        int hundredths = Mathf.FloorToInt(remainingSeconds * Mathf.Pow(10, displayDecimals));

        if (showMinutes && minutes > 0)
            return $"{minutes:00}:{secPart:00}.{hundredths:D2}";
        else
            return $"{seconds:F2}";
    }

    /// <summary>
    /// 公共方法：重置计时器（时间归零，状态变为 Idle）
    /// 供外部调用（例如重置管理器）
    /// </summary>
    public void ResetStopwatch()
    {
        currentState = TimerState.Idle;
        currentTime = 0f;
        UpdateDisplay(0f);
    }

    /// <summary>
    /// 自动寻找场景中的 PendulumResetManager，将其重置按钮与计时器重置功能绑定
    /// 这样点击实验“重置”按钮时，计时器也会自动归零
    /// </summary>
    private void AutoBindToResetManager()
    {
        PendulumResetManager resetManager = FindObjectOfType<PendulumResetManager>();
        if (resetManager != null && resetManager.resetButton != null)
        {
            // 向实验重置按钮添加监听（不覆盖原有逻辑，仅追加）
            resetManager.resetButton.onClick.AddListener(() => {
                ResetStopwatch();
                Debug.Log("实验重置联动：计时器已归零");
            });
            Debug.Log("电子计时器：已自动绑定实验重置管理器，重置实验时计时器将归零");
        }
        else
        {
            Debug.LogWarning("电子计时器：未找到 PendulumResetManager 或其重置按钮，无法自动联动重置功能（不影响计时器独立使用）");
        }
    }

    /// <summary>
    /// 编辑器下参数变动时刷新显示（辅助调试）
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && timerDisplayText != null)
            UpdateDisplay(currentTime);
    }

    /// <summary>
    /// 可选：提供手动开始、停止、重置的公共接口（方便编辑器调用）
    /// </summary>
    [ContextMenu("手动开始计时")]
    public void StartTimer()
    {
        if (currentState == TimerState.Idle)
            OnTimerButtonClick();
        else
            Debug.LogWarning("计时器无法开始：当前状态不是 Idle，请先重置计时器");
    }

    [ContextMenu("手动停止计时")]
    public void StopTimer()
    {
        if (currentState == TimerState.Running)
            OnTimerButtonClick();
        else
            Debug.LogWarning("计时器无法停止：当前没有正在计时");
    }

    [ContextMenu("手动重置计时器")]
    public void ManualReset()
    {
        ResetStopwatch();
    }
}