using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(AudioSource))]
public class ProjectileBallController : MonoBehaviour
{
    [Header("动画参数")]
    [Tooltip("每个轨迹点之间的等待时间（秒），应与 PhysicsSimulationCore 的 timeStep 保持一致")]
    public float animationTimeStep = 0.02f;

    [Tooltip("动画播放速度倍率（1 = 原速，2 = 二倍速）")]
    [Range(0.1f, 10f)]
    public float playbackSpeed = 1f;

    [Header("轨迹线配置")]
    [Tooltip("轨迹线宽度")]
    public float trailWidth = 0.05f;

    [Tooltip("轨迹线颜色（起点）")]
    public Color trailStartColor = Color.cyan;

    [Tooltip("轨迹线颜色（终点）")]
    public Color trailEndColor = new Color(0f, 1f, 1f, 0f);

    [Tooltip("是否在实验开始时自动播放动画（true = 进入 Step3_RunSim 后自动播放）")]
    public bool autoPlayOnStep3 = true;

    [Header("小球外观")]
    [Tooltip("落地时是否播放一次缩放弹跳效果")]
    public bool playLandingBounce = true;

    [Header("落地音效")]
    [Tooltip("落地音效片段（请拖入 AudioClip）")]
    public AudioClip landingSound;

    [Tooltip("落地音效音量（0 = 静音，1 = 最大）")]
    [Range(0f, 1f)]
    public float soundVolume = 0.6f;

    // ── 【新增】高度控制参数 ──────────────────────────────────────────

    [Header("高度控制")]
    [Tooltip("滚轮每格滚动对应的高度变化量（单位：米，精确到 0.1）")]
    public float heightScrollStep = 0.1f;

    [Tooltip("小球可设置的最小高度（世界坐标 Y，须高于地面）")]
    public float minHeight = 0.1f;

    [Tooltip("小球可设置的最大高度（世界坐标 Y）")]
    public float maxHeight = 20f;

    /// <summary>
    /// 当前小球初始高度（世界坐标 Y，只读）。
    /// 由鼠标滚轮调整，精确到一位小数。
    /// 可供外部模块（如 UI 数值显示）读取。
    /// </summary>
    public float CurrentHeight => _originPosition.y;

    // ── 私有成员 ─────────────────────────────────────────────────────

    private LineRenderer _lineRenderer;
    private Coroutine _animationCoroutine;
    private Vector3 _originPosition;

    private ExperimentStateManager _stateManager;
    private ExperimentFlowController _flowController;
    private AudioSource _audioSource;

    private bool _isPaused = false;
    private bool _isPlaying = false;

    // ── 生命周期 ─────────────────────────────────────────────────────

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _originPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        ConfigureLineRenderer();
        ConfigureAudioSource();
    }

    private void Start()
    {
        _stateManager = ExperimentStateManager.Instance;
        StartCoroutine(LateBindFrameworkEvents());
    }

    private void Update()
    {
        HandleHeightScroll();
    }

    private IEnumerator LateBindFrameworkEvents()
    {
        yield return null;

        if (UserActionManager.Instance != null)
        {
            _flowController = UserActionManager.Instance.GetFlowController();
            _flowController.OnStepChanged += OnStepChanged;
            _flowController.OnFlowError += OnFlowError;
        }
        else
        {
            Debug.LogWarning("[ProjectileBallController] 未找到 UserActionManager 实例，步骤自动播放功能将不可用。");
        }

        if (_stateManager != null)
            _stateManager.OnRunStateChanged += OnRunStateChanged;
        else
            Debug.LogWarning("[ProjectileBallController] 未找到 ExperimentStateManager 实例。");
    }

    private void OnDestroy()
    {
        if (_flowController != null)
        {
            _flowController.OnStepChanged -= OnStepChanged;
            _flowController.OnFlowError -= OnFlowError;
        }
        if (_stateManager != null)
            _stateManager.OnRunStateChanged -= OnRunStateChanged;
    }

    // ── 【新增】鼠标滚轮高度控制 ─────────────────────────────────────

    /// <summary>
    /// 每帧检测鼠标滚轮输入，调整小球初始高度。
    ///
    /// 触发条件（同时满足）：
    ///   1. 当前不在播放动画（_isPlaying == false）
    ///   2. 有滚轮输入（Input.mouseScrollDelta.y != 0）
    ///
    /// 精度保证：
    ///   目标高度 = Round((当前高度 + 滚动量 × step) × 10) / 10
    ///   确保每次调整结果严格保留一位小数，不会因浮点累积产生误差。
    ///
    /// 调整后立即更新：
    ///   - _originPosition.y（重置归位目标同步更新）
    ///   - transform.position（小球实时位置，即时可见）
    /// </summary>
    private void HandleHeightScroll()
    {
        if (_isPlaying) return;

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        // 四舍五入到一位小数，彻底消除浮点累积误差
        float rawY = _originPosition.y + scrollDelta * heightScrollStep;
        float snapped = Mathf.Round(rawY * 10f) / 10f;
        float newY = Mathf.Clamp(snapped, minHeight, maxHeight);

        if (Mathf.Approximately(newY, _originPosition.y)) return;

        _originPosition = new Vector3(_originPosition.x, newY, _originPosition.z);
        transform.position = _originPosition;

        Debug.Log($"[ProjectileBallController] 小球高度已调整 → Y = {newY:F1} m");
    }

    // ── 框架事件响应 ─────────────────────────────────────────────────

    private void OnStepChanged(ExperimentStep step)
    {
        if (step == ExperimentStep.Step3_RunSim && autoPlayOnStep3)
            StartCoroutine(WaitAndPlay());
    }

    private IEnumerator WaitAndPlay()
    {
        yield return null;
        PlayAnimation();
    }

    private void OnRunStateChanged(ExperimentRunState state)
    {
        switch (state)
        {
            case ExperimentRunState.Running:
                if (_isPaused) ResumeAnimation();
                break;
            case ExperimentRunState.Paused:
                PauseAnimation();
                break;
            case ExperimentRunState.Idle:
                ResetBall();
                break;
            case ExperimentRunState.Finished:
                break;
        }
    }

    private void OnFlowError(string errorMsg)
    {
        Debug.LogWarning($"[ProjectileBallController] 流程错误：{errorMsg}");
    }

    // ── 公开控制接口 ──────────────────────────────────────────────────

    public void PlayAnimation()
    {
        if (!SimulationDataBuffer.HasValidData())
        {
            Debug.LogWarning("[ProjectileBallController] SimulationDataBuffer 无有效数据，请先调用 PhysicsSimulationCore 计算并写入数据。");
            return;
        }

        StopAnimationCoroutine();
        _isPaused = false;
        _isPlaying = true;

        _animationCoroutine = StartCoroutine(
            AnimateAlongTrajectory(SimulationDataBuffer.CurrentTrajectoryPoints));
    }

    public void PauseAnimation()
    {
        if (!_isPlaying) return;
        _isPaused = true;
        Debug.Log("[ProjectileBallController] 动画已暂停。");
    }

    public void ResumeAnimation()
    {
        if (!_isPaused) return;
        _isPaused = false;
        Debug.Log("[ProjectileBallController] 动画已恢复。");
    }

    /// <summary>
    /// 重置小球：回到 _originPosition（保留用户最后设置的高度），清空轨迹线，停止动画。
    /// 注意：重置不会还原高度，高度由用户通过滚轮显式控制。
    /// </summary>
    public void ResetBall()
    {
        StopAnimationCoroutine();
        transform.position = _originPosition;
        ClearTrail();
        _isPaused = false;
        _isPlaying = false;
        Debug.Log($"[ProjectileBallController] 小球已重置到初始位置（高度 Y = {_originPosition.y:F1} m）。");
    }

    // ── 核心动画协程 ─────────────────────────────────────────────────

    private IEnumerator AnimateAlongTrajectory(List<Vector3> points)
    {
        int validPointCount = Mathf.Min(points.Count, PhysicsSimulationCore.MaxTrajectoryPoints);

        _lineRenderer.positionCount = validPointCount;
        for (int i = 0; i < validPointCount; i++)
            _lineRenderer.SetPosition(i, points[i]);

        transform.position = points[0];

        float waitTime = animationTimeStep / Mathf.Max(playbackSpeed, 0.01f);

        for (int i = 0; i < validPointCount; i++)
        {
            while (_isPaused)
                yield return null;

            transform.position = points[i];
            UpdateTrailProgress(points, i);

            yield return new WaitForSeconds(waitTime);
        }

        _isPlaying = false;
        OnAnimationFinished();
    }

    // ── 轨迹线辅助方法 ───────────────────────────────────────────────

    private void ConfigureLineRenderer()
    {
        _lineRenderer.startWidth = trailWidth;
        _lineRenderer.endWidth = trailWidth * 0.3f;
        _lineRenderer.startColor = trailStartColor;
        _lineRenderer.endColor = trailEndColor;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;

        if (_lineRenderer.material == null || _lineRenderer.material.name == "Default-Line")
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void UpdateTrailProgress(List<Vector3> points, int currentIndex)
    {
        float progress = (float)currentIndex / Mathf.Max(points.Count - 1, 1);

        _lineRenderer.startColor = new Color(
            trailStartColor.r, trailStartColor.g, trailStartColor.b, trailStartColor.a);
        _lineRenderer.endColor = new Color(
            trailEndColor.r, trailEndColor.g, trailEndColor.b,
            trailEndColor.a * (1 - progress));
    }

    private void ClearTrail()
    {
        _lineRenderer.positionCount = 0;
    }

    // ── 音效辅助方法 ──────────────────────────────────────────────────

    private void ConfigureAudioSource()
    {
        if (_audioSource == null) return;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        _audioSource.volume = soundVolume;
    }

    private void PlayLandingSound()
    {
        if (_audioSource == null || landingSound == null) return;
        _audioSource.PlayOneShot(landingSound, soundVolume);
    }

    // ── 动画完成回调 ─────────────────────────────────────────────────

    private void OnAnimationFinished()
    {
        Debug.Log("[ProjectileBallController] 抛体动画播放完毕。");
        PlayLandingSound();

        if (playLandingBounce)
            StartCoroutine(LandingBounceEffect());

        if (_stateManager != null &&
            _stateManager.CurrentRunState == ExperimentRunState.Running)
            _stateManager.FinishExperiment();
    }

    private IEnumerator LandingBounceEffect()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            transform.localScale = Vector3.Lerp(
                originalScale,
                new Vector3(originalScale.x * 1.4f, originalScale.y * 0.5f, originalScale.z * 1.4f),
                t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            transform.localScale = Vector3.Lerp(
                new Vector3(originalScale.x * 1.4f, originalScale.y * 0.5f, originalScale.z * 1.4f),
                originalScale,
                t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    // ── 内部工具 ─────────────────────────────────────────────────────

    private void StopAnimationCoroutine()
    {
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        _isPlaying = false;
    }
}     