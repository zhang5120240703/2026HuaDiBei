using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抛体小球表现控制器（挂载在小球 GameObject 上）
//
/// 职责：
///   1. 读取 SimulationDataBuffer.CurrentTrajectoryPoints（三维轨迹点）
///   2. 驱动小球沿轨迹点逐帧移动，模拟平抛/斜抛运动
///   3. 用 LineRenderer 实时绘制抛物线轨迹
///   4. 监听框架状态事件，正确响应 开始/暂停/重置
/// 依赖：
///   - SimulationDataBuffer（读取轨迹数据）
///   - ExperimentStateManager（监听运行/暂停/重置）
///   - ExperimentFlowController（监听步骤变化，在 Step3_RunSim 自动播放）
///   - UserActionManager（获取 FlowController 引用）
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(AudioSource))] // 音效新增：自动挂载 AudioSource 组件
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
    public Color trailEndColor = new Color(0f, 1f, 1f, 0f); // 末端渐隐

    [Tooltip("是否在实验开始时自动播放动画（true = 进入 Step3_RunSim 后自动播放）")]
    public bool autoPlayOnStep3 = true;

    [Header("小球外观")]
    [Tooltip("落地时是否播放一次缩放弹跳效果")]
    public bool playLandingBounce = true;

    [Header("落地音效")] // 音效新增
    [Tooltip("落地音效片段（请拖入 AudioClip）")]
    public AudioClip landingSound;

    [Tooltip("落地音效音量（0 = 静音，1 = 最大）")]
    [Range(0f, 1f)]
    public float soundVolume = 0.6f;


    // ── 私有成员 ─────────────────────────────────────────────────────
    private LineRenderer _lineRenderer; 
    private Coroutine _animationCoroutine;
    private Vector3 _originPosition;     // 记录初始位置（用于重置归位）

    private ExperimentStateManager _stateManager;
    private ExperimentFlowController _flowController;

    private AudioSource _audioSource;        // 音效新增：AudioSource 引用

    private bool _isPaused = false;
    private bool _isPlaying = false;

    // GC优化：轨迹渐变色缓存
    private Gradient _trailGradient;
    private GradientColorKey[] _colorKeys = new GradientColorKey[2];
    private GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];

    // ── 生命周期 ─────────────────────────────────────────────────────

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _originPosition = transform.position;
        _audioSource = GetComponent<AudioSource>(); // 音效新增：获取 AudioSource 组件

        ConfigureLineRenderer();
        ConfigureAudioSource(); // 音效新增：初始化 AudioSource 配置
    }

    private void Start()
    {
        // 获取框架引用
        _stateManager = ExperimentStateManager.Instance;

        // UserActionManager 在 ExperimentCoreEntry.Start() 中初始化，
        // 用 WaitForEndOfFrame 等一帧确保单例已就绪
        StartCoroutine(LateBindFrameworkEvents());
    }

    private IEnumerator LateBindFrameworkEvents()
    {
        // 等待一帧，确保 ExperimentCoreEntry.Start() 已执行完毕
        yield return null;

        if (UserActionManager.Instance != null)
        {
            _flowController = UserActionManager.Instance.GetFlowController();

            // 监听步骤变化
            _flowController.OnStepChanged += OnStepChanged;

            // 监听流程错误（可选：用于调试）
            _flowController.OnFlowError += OnFlowError;
        }
        else
        {
            Debug.LogWarning("[ProjectileBallController] 未找到 UserActionManager 实例，" +
                             "步骤自动播放功能将不可用。");
        }

        if (_stateManager != null)
        {
            // 监听运行状态变化
            _stateManager.OnRunStateChanged += OnRunStateChanged;
        }
        else
        {
            Debug.LogWarning("[ProjectileBallController] 未找到 ExperimentStateManager 实例。");
        }
    }

    private void OnDestroy()
    {
        // 取消所有事件监听，防止内存泄漏
        if (_flowController != null)
        {
            _flowController.OnStepChanged -= OnStepChanged;
            _flowController.OnFlowError -= OnFlowError;
        }

        if (_stateManager != null)
            _stateManager.OnRunStateChanged -= OnRunStateChanged;
    }

    // ── 框架事件响应 ─────────────────────────────────────────────────

    /// <summary>
    /// 步骤变化回调：进入 Step3_RunSim 且数据就绪时自动播放
    /// </summary>
    private void OnStepChanged(ExperimentStep step)
    {
        if (step == ExperimentStep.Step3_RunSim && autoPlayOnStep3)
        {
            // 给交互逻辑一帧时间写入 SimulationDataBuffer
            StartCoroutine(WaitAndPlay());
        }
    }

    private IEnumerator WaitAndPlay()
    {
        yield return null; // 等一帧，确保 SimulationDataBuffer 已写入
        PlayAnimation();
    }

    /// <summary>
    /// 运行状态变化回调：处理暂停 / 继续 / 重置
    /// </summary>
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
                // 重置：小球归位，轨迹线清空
                ResetBall();
                break;

            case ExperimentRunState.Finished:
                // 实验结束：保持小球在落点，不做额外操作
                break;
        }
    }

    private void OnFlowError(string errorMsg)
    {
        Debug.LogWarning($"[ProjectileBallController] 流程错误：{errorMsg}");
    }

    // ── 公开控制接口（供交互逻辑或 Inspector 调试按钮调用） ──────────

    /// <summary>
    /// 播放抛体动画。
    /// 自动从 SimulationDataBuffer 读取最新轨迹数据。
    /// 若数据为空则打印警告并跳过。
    /// </summary>
    public void PlayAnimation()
    {
        if (!SimulationDataBuffer.HasValidData())
        {
            Debug.LogWarning("[ProjectileBallController] SimulationDataBuffer 无有效数据，" +
                             "请先调用 PhysicsSimulationCore 计算并写入数据。");
            return;
        }

        // 若已在播放，先停止旧协程
        StopAnimationCoroutine();

        _isPaused = false;
        _isPlaying = true;

        _animationCoroutine = StartCoroutine(
            AnimateAlongTrajectory(SimulationDataBuffer.CurrentTrajectoryPoints));
    }

    /// <summary>
    /// 暂停动画（保留当前位置）
    /// </summary>
    public void PauseAnimation()
    {
        if (!_isPlaying) return;
        _isPaused = true;
        Debug.Log("[ProjectileBallController] 动画已暂停。");
    }

    /// <summary>
    /// 恢复已暂停的动画
    /// </summary>
    public void ResumeAnimation()
    {
        if (!_isPaused) return;
        _isPaused = false;
        Debug.Log("[ProjectileBallController] 动画已恢复。");
    }

    /// <summary>
    /// 重置小球：回到初始位置，清空轨迹线，停止动画
    /// </summary>
    public void ResetBall()
    {
        StopAnimationCoroutine();
        transform.position = _originPosition;
        ClearTrail();
        _isPaused = false;
        _isPlaying = false;
        Debug.Log("[ProjectileBallController] 小球已重置到初始位置。");
    }

    // ── 核心动画协程 ─────────────────────────────────────────────────

    /// <summary>
    /// 核心：驱动小球沿三维轨迹点序列逐步移动，同步绘制轨迹线
    /// </summary>
    private IEnumerator AnimateAlongTrajectory(List<Vector3> points)
    {
        // 截断点数至上限，与 PhysicsSimulationCore 的防御逻辑保持一致
        // 正常情况下 points.Count << MaxTrajectoryPoints，此行无任何性能开销
        int validPointCount = Mathf.Min(points.Count, PhysicsSimulationCore.MaxTrajectoryPoints);

        // 初始化轨迹线（预分配点数）
        _lineRenderer.positionCount = validPointCount;

        // 将所有点先写入（轨迹线预显示完整路径）
        for (int i = 0; i < validPointCount; i++)
            _lineRenderer.SetPosition(i, points[i]);

        // 小球从第一个点出发
        // ★ Bug1 Fix: 同步 _originPosition，确保 ResetBall 能回到物理起点而非场景摆放位置
        _originPosition = points[0];
        transform.position = points[0];

        float waitTime = animationTimeStep / Mathf.Max(playbackSpeed, 0.01f);

        for (int i = 0; i < validPointCount; i++)
        {
            // 暂停检测：暂停时持续等待，直到恢复
            while (_isPaused)
                yield return null;

            // 移动小球到当前轨迹点
            transform.position = points[i];

            // 实时更新轨迹线：已走过的部分高亮（可选：让未走部分半透明）
            UpdateTrailProgress(points, i);

            yield return new WaitForSeconds(waitTime);
        }

        // 动画播放完毕
        _isPlaying = false;
        OnAnimationFinished();
    }

    // ── 轨迹线辅助方法 ───────────────────────────────────────────────

    /// <summary>
    /// 配置 LineRenderer 基础属性
    /// </summary>
    private void ConfigureLineRenderer()
    {
        _lineRenderer.startWidth = trailWidth;
        _lineRenderer.endWidth = trailWidth * 0.3f; // 末端细化
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;

        // 使用标准 Unlit 材质（无需光照），保证轨迹线在任何光照下可见
        if (_lineRenderer.material == null || _lineRenderer.material.name == "Default-Line")
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        // 初始化渐变色
        if (_trailGradient == null)
        {
            _trailGradient = new Gradient();
            _colorKeys[0].color = trailStartColor;
            _colorKeys[0].time = 0f;
            _colorKeys[1].color = trailEndColor;
            _colorKeys[1].time = 1f;
            _alphaKeys[0].alpha = trailStartColor.a;
            _alphaKeys[0].time = 0f;
            _alphaKeys[1].alpha = trailEndColor.a;
            _alphaKeys[1].time = 1f;
            _trailGradient.SetKeys(_colorKeys, _alphaKeys);
        }
    }

    /// <summary>
    /// 更新轨迹线颜色进度（已走过的轨迹段完全不透明，未走的轨迹段半透明）
    /// </summary>
    private void UpdateTrailProgress(List<Vector3> points, int currentIndex)
    {
        // 用渐变色区分已走/未走，使用Gradient避免每帧new Color
        float progress = (float)currentIndex / Mathf.Max(points.Count - 1, 1);
        if (_trailGradient == null)
        {
            // 兜底：初始化一次
            _trailGradient = new Gradient();
            _colorKeys[0].color = trailStartColor;
            _colorKeys[0].time = 0f;
            _colorKeys[1].color = trailEndColor;
            _colorKeys[1].time = 1f;
            _alphaKeys[0].alpha = trailStartColor.a;
            _alphaKeys[0].time = 0f;
            _alphaKeys[1].alpha = trailEndColor.a;
            _alphaKeys[1].time = 1f;
            _trailGradient.SetKeys(_colorKeys, _alphaKeys);
        }
        Color c = _trailGradient.Evaluate(progress);
        _lineRenderer.startColor = _trailGradient.Evaluate(0f);
        _lineRenderer.endColor = c;
    }

    /// <summary>
    /// 清空轨迹线
    /// </summary>
    private void ClearTrail()
    {
        _lineRenderer.positionCount = 0;
    }

    // ── 音效辅助方法 ──────────────────────────────────────────────── 音效新增

    /// <summary>
    /// 配置 AudioSource 基础属性（不循环、不自启、设置初始音量）
    /// </summary>
    private void ConfigureAudioSource()
    {
        if (_audioSource == null) return;

        _audioSource.loop = false;          // 音效不循环
        _audioSource.playOnAwake = false;   // 不自启
        _audioSource.volume = soundVolume;  // 初始音量
    }

    /// <summary>
    /// 播放落地音效（使用 PlayOneShot 避免打断其他音效）
    /// </summary>
    private void PlayLandingSound()
    {
        if (_audioSource == null || landingSound == null) return;

        _audioSource.PlayOneShot(landingSound, soundVolume);
    }

    // ── 动画完成回调 ─────────────────────────────────────────────────

    /// <summary>
    /// 动画播放完毕时调用
    /// </summary>
    private void OnAnimationFinished()
    {
        Debug.Log("[ProjectileBallController] 抛体动画播放完毕。");

        PlayLandingSound(); // 音效新增：落地时触发音效

        if (playLandingBounce)
            StartCoroutine(LandingBounceEffect());

        // 通知框架：实验完成（由小球动画结束驱动 Finish 状态）
        if (_stateManager != null &&
            _stateManager.CurrentRunState == ExperimentRunState.Running)
        {
            _stateManager.FinishExperiment();
        }
    }

    /// <summary>
    /// 落地弹跳缩放效果（纯视觉，不影响物理数据）
    /// </summary>
    private IEnumerator LandingBounceEffect()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.3f;
        float elapsed = 0f;

        // 压缩
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

        // 回弹
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