using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抛体实验完整实现脚本（按 ExperimentStep 驱动）
///
/// ── 架构说明 ──────────────────────────────────────────────────────────
/// 本脚本【不继承 InteractionModuleBase】，原因：
///   InteractionModuleBase 是纯 C# 抽象类，无法使用协程和 MonoBehaviour 生命周期。
///   本脚本的核心是"等待用户输入 → 执行 → 等待下一个输入"的协程驱动异步流，
///   必须是 MonoBehaviour。两者职责不重叠，继承无意义。
///
/// ── 与 UI 的对接方式（两条线）────────────────────────────────────────
/// 【UI → 脚本】UI 调用公开方法：
///     SetParam(...)       写入参数（Step2 滑杆 / 输入框 OnValueChanged 时调）
///     ConfirmPrepare()    Step1：确认准备就绪
///     ConfirmParam()      Step2：确认参数（内部会校验）
///     ConfirmObserved()   Step4：确认观察完毕
///     ConfirmFinish()     Step5：确认结束
///     RequestPause()      任意阶段：暂停
///     RequestResume()     任意阶段：恢复
///     RequestReset()      任意阶段：重置回 Step1
///
/// 【脚本 → UI】UI 监听事件，更新显示：
///     OnStepEntered       进入新步骤，切换 UI 面板
///     OnParamError        参数校验失败，显示错误文字
///     OnSimulationReady   仿真数据就绪，可显示轨迹预览
///     OnObserveData       Step4 观测数据，刷新数据面板
///     OnPaused / OnResumed    暂停/恢复按钮状态切换
///     OnReset             UI 全部恢复初始状态
///     OnFlowError         流程跳转失败提示
///
/// ── 暂停机制 ─────────────────────────────────────────────────────────
/// 所有"等待用户"协程均通过 WaitForSignal() 轮询：
///   - _isPaused == true 时，即便用户点击了"确认"，协程也不会继续推进
///   - Step3 仿真运行阶段，暂停额外调用 ballController.PauseAnimation()
///   - 重置请求在所有等待点均有检测，确保协程安全退出
/// </summary>
public class ProjectileExperimentController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════
    // Inspector 配置
    // ══════════════════════════════════════════════════════════════════

    [Header("默认发射参数")]
    [Tooltip("初始速度（m/s）")]
    public float defaultVelocity = 10f;

    [Tooltip("仰角（°），0 = 平抛，45 = 最优斜抛")]
    [Range(0f, 90f)]
    public float defaultAngle = 0f;

    [Tooltip("水平发射方向（XZ 平面，自动忽略 Y 分量）")]
    public Vector3 defaultDirection = Vector3.forward;

    [Tooltip("发射起点世界坐标（Y 必须大于地面高度 0）")]
    public Vector3 defaultStartPosition = new Vector3(0f, 1f, 0f);

    [Tooltip("物理计算时间步长（s），越小越精确，建议 0.01 ~ 0.05")]
    public float defaultTimeStep = 0.02f;
    // 注意：时间步长过大可能导致轨迹不平滑，甚至错过碰撞点；过小则计算量增加，可能导致性能问题。
    [Tooltip("最大模拟时长（s），超过此时间停止计算")]
    public float defaultTotalTime = 5f;

    [Header("依赖（留空则自动查找）")]
    [Tooltip("场景中的抛体小球控制器")]
    public ProjectileBallController ballController;

    // ══════════════════════════════════════════════════════════════════
    // UI 事件接口（UI 层订阅这些事件）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>进入新步骤，UI 据此切换面板 / 高亮步骤条</summary>
    public event Action<ExperimentStep> OnStepEntered;

    /// <summary>参数校验失败，string = 错误描述（UI 显示红字提示）</summary>
    public event Action<string> OnParamError;

    /// <summary>
    /// UI 主动读取当前参数时触发（进入 Step2 时发送一次，让 UI 填充默认值）
    /// 参数顺序：velocity, angle, direction, startPosition, timeStep, totalTime
    /// </summary>
    public event Action<float, float, Vector3, Vector3, float, float> OnParamLoaded;

    /// <summary>
    /// Step3 仿真计算完成，携带参数快照和三维轨迹点列表
    /// UI 可用于绘制轨迹预览或显示计算结果
    /// </summary>
    public event Action<LaunchParamSnapshot, List<Vector3>> OnSimulationReady;

    /// <summary>
    /// Step4 观测数据推送（进入 Step4 时发送一次）
    /// float xDist, float yDist, float totalDist, int pointCount
    /// </summary>
    public event Action<float, float, float, int> OnObserveData;

    /// <summary>暂停，UI 将"暂停"按钮切换为"继续"，禁用确认类按钮</summary>
    public event Action OnPaused;

    /// <summary>恢复，UI 将"继续"按钮切换为"暂停"，启用确认类按钮</summary>
    public event Action OnResumed;

    /// <summary>实验被重置，UI 全部恢复到初始状态</summary>
    public event Action OnReset;

    /// <summary>流程跳转失败或其他流程错误，string = 错误描述</summary>
    public event Action<string> OnFlowError;

    /// <summary>
    /// 滚轮调整起点高度后触发，float = 新的 Y 坐标（已夹到合法范围）。
    /// UI 层订阅此事件以同步输入框显示；ProjectileBallController 通过
    /// SetPreviewHeight 同步小球预览位置（由本类内部直接调用，不通过事件）。
    /// </summary>
    public event Action<float> OnStartHeightChanged;

    // ══════════════════════════════════════════════════════════════════
    // 运行时参数（由 UI 通过 SetParam 写入）
    // ══════════════════════════════════════════════════════════════════

    private float _velocity;
    private float _angle;
    private Vector3 _direction;
    private Vector3 _startPosition;
    private float _timeStep;
    private float _totalTime;

    // ══════════════════════════════════════════════════════════════════
    // 框架依赖
    // ══════════════════════════════════════════════════════════════════

    private ExperimentStateManager _stateManager;
    private ExperimentFlowController _flowController;

    // ★ Bug3 Fix: 缓存事件处理器，确保 OnDestroy 能正确解绑（直接 new lambda 无法匹配已绑定的实例）
    private Action<string> _flowErrorHandler;

    // ══════════════════════════════════════════════════════════════════
    // 流程控制内部状态
    // ══════════════════════════════════════════════════════════════════

    // 各步骤"用户已确认"信号（由对应 Confirm* 方法置为 true）
    private bool _step1Confirmed;
    private bool _step2Confirmed;
    private bool _step4Confirmed;
    private bool _step5Confirmed;

    // 全局暂停标志（任意阶段的 WaitForSignal 均检测此标志）
    private bool _isPaused;

    // 重置请求标志（任意阶段的 WaitForSignal 均检测此标志，触发后重启主循环）
    private bool _resetRequested;

    // 是否正处于 Step3 仿真运行阶段（用于 Pause/Resume 时额外控制球的动画）
    private bool _inStep3Sim;

    // ── 滚轮高度调节常量 ─────────────────────────────────────────────
    /// <summary>起点高度最小值（m），必须高于地面（GroundY = 0）</summary>
    private const float HeightScrollMin = 0.1f;
    /// <summary>起点高度最大值（m），防止调节过高脱出实验场景</summary>
    private const float HeightScrollMax = 50f;

    // ══════════════════════════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // 将参数初始化为 Inspector 默认值
        ResetParamsToDefault();
    }

    private void Start()
    {
        _stateManager = ExperimentStateManager.Instance;
        if (_stateManager == null)
        {
            Debug.LogError("[ExperimentController] 未找到 ExperimentStateManager！" +
                           "请确保场景中已挂载并激活该组件。");
            return;
        }

        // 等一帧再绑定（确保 ExperimentCoreEntry.Awake 已运行）
        StartCoroutine(LateBindAndStartFlow());
    }

    private IEnumerator LateBindAndStartFlow()
    {
        yield return null;

        // 获取 FlowController 引用
        if (UserActionManager.Instance != null)
        {
            _flowController = UserActionManager.Instance.GetFlowController();
            // ★ Bug3 Fix: 用字段缓存 handler，OnDestroy 才能正确解绑
            _flowErrorHandler = msg => OnFlowError?.Invoke(msg);
            _flowController.OnFlowError += _flowErrorHandler;
        }
        else
        {
            Debug.LogWarning("[ExperimentController] 未找到 UserActionManager，" +
                             "步骤跳转将依赖内部直接状态切换。");
        }

        // 自动查找小球控制器
        if (ballController == null)
            ballController = FindObjectOfType<ProjectileBallController>();

        // ★ Bug2 Fix: 禁用 ballController 的自动播放，避免 DoStep3 直接调用 PlayAnimation 后
        //   ballController 的 OnStepChanged(Step3) 再触发一次 WaitAndPlay，导致动画被重启
        if (ballController != null)
            ballController.autoPlayOnStep3 = false;

        // 启动主实验循环（支持 Reset 后重跑）
        StartCoroutine(MainExperimentLoop());
    }

    private void OnDestroy()
    {
        // ★ Bug3 Fix: 使用缓存的 handler 字段解绑，而非重新创建 lambda（新 lambda 不等于已绑定的实例）
        if (_flowController != null && _flowErrorHandler != null)
            _flowController.OnFlowError -= _flowErrorHandler;
    }

    // ══════════════════════════════════════════════════════════════════
    // ── UI 公开接口 ──────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    // ─── Step1 ────────────────────────────────────────────────────────

    /// <summary>
    /// [Step1 · UI调用]
    /// 告知脚本准备工作已完成（设备检查、场景就绪等），可以进入参数设置阶段。
    /// 暂停期间调用无效，需先 RequestResume()。
    /// </summary>
    public void ConfirmPrepare()
    {
        if (_flowController != null &&
            _flowController.CurrentStep != ExperimentStep.Step1_Prepare)
        {
            OnFlowError?.Invoke("当前不在准备阶段（Step1），无法确认准备。");
            return;
        }
        _step1Confirmed = true;
        Debug.Log("[ExperimentController] Step1 确认：准备就绪。");
    }

    // ─── Step2 ────────────────────────────────────────────────────────

    /// <summary>
    /// [Step2 · UI调用]
    /// 实时写入实验参数。任何字段传 null 则保留上一次的值。
    /// 建议在 Slider.OnValueChanged / InputField.OnEndEdit 回调中调用。
    /// </summary>
    /// <param name="velocity">初始速度（m/s），必须 > 0</param>
    /// <param name="angle">仰角（°），有效范围 [0, 90]</param>
    /// <param name="direction">水平发射方向（Y 分量会被忽略）</param>
    /// <param name="startPosition">发射起点（Y 必须 > 0）</param>
    /// <param name="timeStep">时间步长（s），建议 [0.01, 0.1]</param>
    /// <param name="totalTime">最大仿真时长（s），建议 [1, 20]</param>
    public void SetParam(
        float? velocity = null,
        float? angle = null,
        Vector3? direction = null,
        Vector3? startPosition = null,
        float? timeStep = null,
        float? totalTime = null)
    {
        // null安全和参数校验
        if (velocity.HasValue && velocity.Value > 0) _velocity = velocity.Value;
        if (angle.HasValue && angle.Value >= 0 && angle.Value <= 90) _angle = angle.Value;
        if (direction.HasValue && (Mathf.Abs(direction.Value.x) > 1e-4f || Mathf.Abs(direction.Value.z) > 1e-4f)) _direction = direction.Value;
        if (startPosition.HasValue && startPosition.Value.y > 0) _startPosition = startPosition.Value;
        if (timeStep.HasValue && timeStep.Value > 0 && timeStep.Value <= 1) _timeStep = timeStep.Value;
        if (totalTime.HasValue && totalTime.Value > 0 && totalTime.Value <= 60) _totalTime = totalTime.Value;
    }

    /// <summary>
    /// [Step2 · UI调用]
    /// 确认参数，内部进行合法性校验。
    /// 校验失败 → 触发 OnParamError（UI 显示错误原因），流程不推进。
    /// 校验通过 → 进入 Step3 仿真阶段。
    /// 暂停期间调用无效。
    /// </summary>
    public void ConfirmParam()
    {
        if (_flowController != null &&
            _flowController.CurrentStep != ExperimentStep.Step2_SetParam)
        {
            OnFlowError?.Invoke("当前不在参数设置阶段（Step2），无法确认参数。");
            return;
        }

        string err = ValidateParams();
        if (!string.IsNullOrEmpty(err))
        {
            OnParamError?.Invoke(err);
            Debug.LogWarning($"[ExperimentController] 参数校验失败：{err}");
            return;
        }

        // 通知状态管理器参数合法
        _stateManager.IsParamValid = true;
        _step2Confirmed = true;
        Debug.Log("[ExperimentController] Step2 确认：参数合法，准备运行仿真。");
    }

    // ─── Step3 无需 UI 主动确认，全自动执行 ──────────────────────────
    // 暂停/继续通过 RequestPause / RequestResume 控制。

    // ─── Step4 ────────────────────────────────────────────────────────

    /// <summary>
    /// [Step4 · UI调用]
    /// 告知脚本观察数据面板已查看完毕，可以进入实验结束阶段。
    /// 暂停期间调用无效。
    /// </summary>
    public void ConfirmObserved()
    {
        if (_flowController != null &&
            _flowController.CurrentStep != ExperimentStep.Step4_Observe)
        {
            OnFlowError?.Invoke("当前不在观察阶段（Step4），无法确认观察。");
            return;
        }
        _step4Confirmed = true;
        Debug.Log("[ExperimentController] Step4 确认：观察完毕。");
    }

    // ─── Step5 ────────────────────────────────────────────────────────

    /// <summary>
    /// [Step5 · UI调用]
    /// 确认实验结束，触发 OnReset 事件（UI 恢复初始状态），
    /// 流程自动循环回 Step1（如需彻底退出，调用 RequestReset 后不再确认即可）。
    /// </summary>
    public void ConfirmFinish()
    {
        if (_flowController != null &&
            _flowController.CurrentStep != ExperimentStep.Step5_Finish)
        {
            OnFlowError?.Invoke("当前不在结束阶段（Step5），无法确认完成。");
            return;
        }
        _step5Confirmed = true;
        Debug.Log("[ExperimentController] Step5 确认：实验结束。");
    }

    // ─── 通用控制接口（任意阶段均有效）──────────────────────────────
    /// <summary>
    /// [任意阶段 · UI调用] 暂停实验。
    /// - Step1/2/4/5（等待用户输入阶段）：即使用户已点击"确认"，流程也不会推进
    /// - Step3（仿真动画运行阶段）：额外暂停小球动画
    /// </summary>
    public void RequestPause()
    {
        if (_isPaused) return;
        _isPaused = true;

        // Step3 仿真阶段：暂停小球动画
        if (_inStep3Sim)
            ballController?.PauseAnimation();

        // ★ Bug Fix: 暂停时不改变 StateManager 状态。
        // 原框架 ExperimentStateManager 缺少 ResumeExperiment() 方法，
        // 如果在暂停时将状态改为 Paused，恢复后状态机无法回到 Running，
        // 导致动画播放完毕时也无法转移至 Finished，WaitForCondition 将永远卡住。
        // 暂停的语义已通过 _isPaused 和 ballController.PauseAnimation() 保证。
        // if (_stateManager.CurrentRunState == ExperimentRunState.Running)
        //     _stateManager.PauseExperiment();

        OnPaused?.Invoke();
        Debug.Log("[ExperimentController] ⏸ 实验已暂停。");
    }
    /// <summary>
    /// [任意阶段 · UI调用] 恢复已暂停的实验。
    /// Step3 阶段会同时恢复小球动画。
    /// </summary>
    public void RequestResume()
    {
        if (!_isPaused) return;
        _isPaused = false;

        // Step3 仿真阶段：恢复小球动画
        if (_inStep3Sim)
            ballController?.ResumeAnimation();

        OnResumed?.Invoke();
        Debug.Log("[ExperimentController] ▶ 实验已恢复。");
    }

    /// <summary>
    /// [任意阶段 · UI调用] 重置实验。
    /// 无论当前处于哪个步骤，均安全中断并回到 Step1。
    /// 同时清空轨迹数据、重置小球位置、恢复参数到默认值。
    /// </summary>
    public void RequestReset()
    {
        _resetRequested = true;
        // 强制解除暂停，让等待中的协程能够检测到 reset 标志并安全退出
        _isPaused = false;
        Debug.Log("[ExperimentController] 🔄 收到重置请求，正在中断当前流程...");
    }

    // ─── 滚轮高度调节接口 ─────────────────────────────────────────────

    /// <summary>
    /// [Step1/Step2 · UI调用] 通过鼠标滚轮调节小球发射起点的 Y 坐标。
    ///
    /// 调用方（TempExperimentUI.Update）负责：
    ///   • 检测 Input.GetAxis("Mouse ScrollWheel")
    ///   • 乘以灵敏度系数后传入 delta
    ///   • 只在 Step1/Step2 阶段调用（步骤过滤在 UI 侧完成）
    ///
    /// 本方法负责：
    ///   1. 将新高度夹到 [HeightScrollMin, HeightScrollMax]
    ///   2. 写入 _startPosition.y
    ///   3. 触发 OnStartHeightChanged 事件（UI 同步输入框显示）
    ///   4. 调用 ballController.SetPreviewHeight() 同步小球预览位置
    /// </summary>
    /// <param name="delta">高度增量（米），正值向上，负值向下</param>
    public void AdjustStartHeightByScroll(float delta)
    {
        float newY = Mathf.Clamp(_startPosition.y + delta, HeightScrollMin, HeightScrollMax);

        // 若已经到达边界且增量方向相同，直接跳过（避免无意义触发）
        if (Mathf.Approximately(newY, _startPosition.y)) return;

        _startPosition.y = newY;

        // 通知 UI 同步输入框
        OnStartHeightChanged?.Invoke(newY);

        // 通知小球控制器更新预览位置（视觉反馈，不触发仿真）
        ballController?.SetPreviewHeight(newY);

        Debug.Log($"[ExperimentController] 🖱️ 滚轮调整起点高度 → Y = {newY:F2} m");
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 主实验循环 ────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 主循环：依次执行每个步骤协程。
    /// 任何步骤中途检测到 _resetRequested，立即执行 HandleReset() 并重新开始。
    /// Step5 完成后也会触发重置（实验可重复进行）。
    /// </summary>
    private IEnumerator MainExperimentLoop()
    {
        while (true)
        {
            // ── 初始化本轮实验状态 ──────────────────────────────────
            _resetRequested = false;
            _isPaused = false;
            _inStep3Sim = false;
            ResetConfirmFlags();

            // ── Step1：准备阶段 ─────────────────────────────────────
            yield return DoStep1_Prepare();
            if (_resetRequested) { HandleReset(); continue; }

            // ── Step2：参数设置 ─────────────────────────────────────
            yield return DoStep2_SetParam();
            if (_resetRequested) { HandleReset(); continue; }

            // ── Step3：运行仿真 ─────────────────────────────────────
            yield return DoStep3_RunSim();
            if (_resetRequested) { HandleReset(); continue; }

            // ── Step4：观察数据 ─────────────────────────────────────
            yield return DoStep4_Observe();
            if (_resetRequested) { HandleReset(); continue; }

            // ── Step5：实验结束 ─────────────────────────────────────
            yield return DoStep5_Finish();

            // Step5 完成（无论是否 reset）均执行重置，开始下一轮
            HandleReset();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 各步骤具体实现 ────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Step1：准备阶段
    /// 职责：通知 UI 进入准备面板，等待用户完成场景检查后点击"确认准备"
    /// </summary>
    private IEnumerator DoStep1_Prepare()
    {
        Debug.Log("[ExperimentController] ═══ Step1：准备阶段 ═══");

        // 通知 UI 切换到 Step1 面板
        OnStepEntered?.Invoke(ExperimentStep.Step1_Prepare);

        // 等待用户调用 ConfirmPrepare()（同时支持暂停 / 重置中断）
        yield return WaitForSignal(() => _step1Confirmed);
        if (_resetRequested) yield break;

        // 推进到 Step2
        bool ok = _flowController?.NextStep() ?? true;
        if (!ok)
            Debug.LogWarning("[ExperimentController] Step1→Step2 跳转被流程控制器拒绝。");
    }

    /// <summary>
    /// Step2：参数设置阶段
    /// 职责：
    ///   1. 向 UI 推送当前参数默认值（让输入框/滑杆显示默认数值）
    ///   2. 等待用户调整参数并调用 ConfirmParam()
    ///   ConfirmParam() 内部校验通过后才会解除阻塞
    /// </summary>
    private IEnumerator DoStep2_SetParam()
    {
        Debug.Log("[ExperimentController] ═══ Step2：参数设置 ═══");

        OnStepEntered?.Invoke(ExperimentStep.Step2_SetParam);

        // 推送当前参数到 UI（让输入控件显示当前值）
        OnParamLoaded?.Invoke(
            _velocity, _angle, _direction, _startPosition, _timeStep, _totalTime);

        // 重置合法性标志，确保本轮参数重新确认
        _stateManager.IsParamValid = false;

        // 等待用户设置并 ConfirmParam()
        yield return WaitForSignal(() => _step2Confirmed);
        if (_resetRequested) yield break;

        bool ok = _flowController?.NextStep() ?? true;
        if (!ok)
            Debug.LogWarning("[ExperimentController] Step2→Step3 跳转被流程控制器拒绝。");
    }

    /// <summary>
    /// Step3：运行仿真阶段
    /// 职责：
    ///   1. 调用 PhysicsSimulationCore 计算三维轨迹（失败则退回 Step2 重新设参数，形成重试循环）
    ///   2. 写入 SimulationDataBuffer
    ///   3. 启动 ExperimentStateManager（→ Running）并驱动小球动画
    ///   4. 立即推进到 Step4（Step4 要求状态为 Running）
    ///   5. 阻塞等待动画播放完毕（状态变为 Finished），期间支持暂停/重置
    /// </summary>
    private IEnumerator DoStep3_RunSim()
    {
        Debug.Log("[ExperimentController] ═══ Step3：运行仿真 ═══");

        OnStepEntered?.Invoke(ExperimentStep.Step3_RunSim);


        List<Vector3> trajectoryPoints = null;

        while (true)
        {
            // ── 1. 物理计算 ─────────────────────────────────────────
            Debug.Log($"[ExperimentController] 开始物理计算：v={_velocity}m/s  θ={_angle}°  " +
                      $"dir={_direction}  start={_startPosition}  dt={_timeStep}s  T={_totalTime}s");

            trajectoryPoints = PhysicsSimulationCore.SimulateProjectileMotion(
                _velocity, _angle, _direction, _startPosition, _timeStep, _totalTime);

            if (trajectoryPoints != null && trajectoryPoints.Count > 0)
                break; // 计算成功，退出重试循环

            // ── 计算失败：退回 Step2 重新设参数 ─────────────────────---
            string err = "物理仿真失败：参数不合法或起点低于地面，请重新设置参数。";
            OnParamError?.Invoke(err);
            Debug.LogError($"[ExperimentController] {err}");

            // 滚回 Step2
            _step2Confirmed = false;
            _stateManager.IsParamValid = false;
            _flowController?.PrevStep();                                  // Step3 → Step2

            OnStepEntered?.Invoke(ExperimentStep.Step2_SetParam);
            OnParamLoaded?.Invoke(_velocity, _angle, _direction, _startPosition, _timeStep, _totalTime);

            // 等待用户重新确认参数
            yield return WaitForSignal(() => _step2Confirmed);
            if (_resetRequested) yield break;

            // 用户确认后重新推进到 Step3，然后重试计算
            _flowController?.NextStep();                                  // Step2 → Step3
            OnStepEntered?.Invoke(ExperimentStep.Step3_RunSim);
            // 继续 while 循环重试
        }

        if (_resetRequested) yield break;

        // ── 2. 写入数据缓冲区 ────────────────────────────────────────
        var snapshot = new LaunchParamSnapshot(
            _velocity, _angle, _direction, _startPosition, _timeStep, _totalTime);
        SimulationDataBuffer.UpdateTrajectoryData(trajectoryPoints, snapshot);

        // 通知 UI：仿真数据就绪，可显示轨迹预览
        OnSimulationReady?.Invoke(snapshot, trajectoryPoints);

        // ── 3. 启动状态机 + 驱动小球动画 ────────────────────────────
        _stateManager.StartExperiment();      // Idle → Running
        _inStep3Sim = true;
        ballController?.PlayAnimation();      // 开始沿轨迹移动（autoPlayOnStep3 已关闭，此处是唯一触发点）

        // ── 4. 立即推进到 Step4（Step4 前置条件要求 Running 状态）───
        bool ok = _flowController?.NextStep() ?? true;
        if (!ok)
            Debug.LogWarning("[ExperimentController] Step3→Step4 跳转被流程控制器拒绝。");

        // ── 5. 等待动画完成（状态变为 Finished）──────────────────────
        // 期间：
        //   - 暂停：ballController.PauseAnimation() 已在 RequestPause() 中调用
        //   - 恢复：ballController.ResumeAnimation() 已在 RequestResume() 中调用
        //   - 重置：检测到 _resetRequested 后安全退出
        yield return WaitForCondition(
            () => _stateManager.CurrentRunState == ExperimentRunState.Finished);

        _inStep3Sim = false;
    }

    /// <summary>
    /// Step4：观察数据阶段
    /// 职责：
    ///   1. 从 SimulationDataBuffer 提取关键数据推送给 UI（数据面板刷新）
    ///   2. 等待用户查看后调用 ConfirmObserved()
    /// </summary>
    private IEnumerator DoStep4_Observe()
    {
        Debug.Log("[ExperimentController] ═══ Step4：观察数据 ═══");

        OnStepEntered?.Invoke(ExperimentStep.Step4_Observe);

        // 推送观测数据到 UI
        if (SimulationDataBuffer.HasValidData())
        {
            OnObserveData?.Invoke(
                SimulationDataBuffer.XDistance,
                SimulationDataBuffer.YDistance,
                SimulationDataBuffer.TotalDistance,
                SimulationDataBuffer.TrajectoryPointCount);

            Debug.Log($"[ExperimentController] 观测数据：" +
                      $"X位移={SimulationDataBuffer.XDistance:F2}m  " +
                      $"Y位移={SimulationDataBuffer.YDistance:F2}m  " +
                      $"总路程={SimulationDataBuffer.TotalDistance:F2}m  " +
                      $"轨迹点={SimulationDataBuffer.TrajectoryPointCount}");
        }

        // 等待用户确认观察完毕（同时支持暂停/重置）
        yield return WaitForSignal(() => _step4Confirmed);
        if (_resetRequested) yield break;

        bool ok = _flowController?.NextStep() ?? true;
        if (!ok)
            Debug.LogWarning("[ExperimentController] Step4→Step5 跳转被流程控制器拒绝。");
    }

    /// <summary>
    /// Step5：实验结束阶段
    /// 职责：
    ///   1. 通知 UI 进入结束面板（可显示实验总结）
    ///   2. 等待用户点击"完成"或"再来一次"（均调用 ConfirmFinish）
    ///   主循环在此协程返回后自动执行 HandleReset 并重新开始
    /// </summary>
    private IEnumerator DoStep5_Finish()
    {
        Debug.Log("[ExperimentController] ═══ Step5：实验结束 ═══");

        OnStepEntered?.Invoke(ExperimentStep.Step5_Finish);

        // 等待用户确认结束（支持暂停）
        yield return WaitForSignal(() => _step5Confirmed);
        // 不需要检测 resetRequested，主循环会在 DoStep5 返回后处理
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 重置处理 ─────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 执行完整重置：状态机 / 流程控制器 / 数据缓冲区 / 小球 / 参数
    /// 由主循环在检测到 _resetRequested 或 Step5 完成后调用
    /// </summary>
    private void HandleReset()
    {
        Debug.Log("[ExperimentController] 🔄 执行重置...");

        _inStep3Sim = false;
        _isPaused = false;

        // 重置状态机
        _stateManager?.ResetExperiment();

        // 重置流程控制器（回到 Step1）
        _flowController?.ResetFlow();

        // 清空轨迹数据
        SimulationDataBuffer.ClearData();

        // 重置小球
        ballController?.ResetBall();

        // 恢复默认参数（如需保留上次参数，删除此行）
        ResetParamsToDefault();

        // 通知 UI 恢复初始显示
        OnReset?.Invoke();

        Debug.Log("[ExperimentController] ✅ 重置完成，即将从 Step1 重新开始。");
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 协程辅助工具 ─────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 等待用户信号（"确认"类操作）。
    /// 暂停期间即使 condition 为 true 也不会推进（保证 Pause 语义一致）。
    /// 检测到重置请求时立即退出（由调用方 yield break）。
    /// </summary>
    private IEnumerator WaitForSignal(Func<bool> condition)
    {
        while (true)
        {
            if (_resetRequested) yield break;

            // 暂停期间忽略所有确认信号，等待 Resume
            if (!_isPaused && condition())
                yield break;

            yield return null;
        }
    }

    /// <summary>
    /// 等待某个条件成立（用于 Step3 等待动画完成等场景）。
    /// 暂停期间挂起（不推进计时），恢复后继续检测。
    /// </summary>
    private IEnumerator WaitForCondition(Func<bool> condition)
    {
        while (true)
        {
            if (_resetRequested) yield break;
            if (condition()) yield break;

            // 暂停期间仅轮询，不做其他操作（动画已被 ballController 暂停）
            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 参数校验 ─────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 校验当前实验参数，返回空字符串表示合法，否则返回错误描述。
    /// </summary>
    private string ValidateParams()
    {
        if (_velocity <= 0f)
            return $"初始速度必须大于 0（当前：{_velocity} m/s）";

        if (_angle < 0f || _angle > 90f)
            return $"仰角必须在 [0°, 90°] 范围内（当前：{_angle}°）";

        if (_startPosition.y <= 0f)
            return $"发射起点高度必须大于地面（Y > 0），当前 Y = {_startPosition.y}";

        if (_timeStep <= 0f || _timeStep > 1f)
            return $"时间步长应在 (0, 1] 秒之间（当前：{_timeStep} s）";

        if (_totalTime <= 0f || _totalTime > 60f)
            return $"最大仿真时长应在 (0, 60] 秒之间（当前：{_totalTime} s）";

        Vector3 hDir = new Vector3(_direction.x, 0f, _direction.z);
        if (hDir.sqrMagnitude < 0.0001f)
            return "发射方向的水平分量（XZ）接近零向量，请设置有效的水平方向。";

        return string.Empty; // 校验通过
    }

    // ══════════════════════════════════════════════════════════════════
    // ── 内部工具 ─────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════

    private void ResetConfirmFlags()
    {
        _step1Confirmed = false;
        _step2Confirmed = false;
        _step4Confirmed = false;
        _step5Confirmed = false;
    }

    private void ResetParamsToDefault()
    {
        _velocity = defaultVelocity;
        _angle = defaultAngle;
        _direction = defaultDirection;
        _startPosition = defaultStartPosition;
        _timeStep = defaultTimeStep;
        _totalTime = defaultTotalTime;
    }
}

/*
 ╔══════════════════════════════════════════════════════════════════════════╗
 ║                        UI 接线速查表                                     ║
 ╠══════════════╦══════════════════════╦═══════════════════════════════════╣
 ║  步骤        ║  UI → 脚本（调用）   ║  脚本 → UI（监听事件）             ║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ Step1 准备   ║ ConfirmPrepare()     ║ OnStepEntered(Step1_Prepare)      ║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ Step2 参数   ║ SetParam(...)        ║ OnStepEntered(Step2_SetParam)     ║
 ║ (滚轮调节小球高度
   不适合用协程，在UI
侧通过update实现)             ║ ConfirmParam()       ║ OnParamLoaded(v,θ,dir,pos,dt,T)   ║
 ║              ║                      ║ OnParamError(errorMsg)            ║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ Step3 仿真   ║ （无需确认，自动）   ║ OnStepEntered(Step3_RunSim)          ║
 ║              ║                      ║ OnSimulationReady(snapshot,points)║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ Step4 观察   ║ ConfirmObserved()    ║ OnStepEntered(Step4_Observe)      ║
 ║              ║                      ║ OnObserveData(xD,yD,total,count)  ║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ Step5 结束   ║ ConfirmFinish()      ║ OnStepEntered(Step5_Finish)       ║
 ╠══════════════╬══════════════════════╬═══════════════════════════════════╣
 ║ 任意阶段     ║ RequestPause()       ║ OnPaused()                        ║
 ║              ║ RequestResume()      ║ OnResumed()                       ║
 ║              ║ RequestReset()       ║ OnReset()                         ║
 ║              ║                      ║ OnFlowError(msg)                  ║
 ╚══════════════╩══════════════════════╩═══════════════════════════════════╝

 挂载方式：
   将本脚本挂载到场景中任意 GameObject（建议挂到 ExperimentCoreEntry 同一个对象）。
   ballController 可留空，脚本会自动 FindObjectOfType 查找。
/*
 ExperimentStateManager 的 Resume 说明：
   原框架 ExperimentStateManager 无 ResumeExperiment() 方法。
   如果在 RequestPause() 中调用 PauseExperiment() 将状态置为 Paused，
   恢复时由于无法调用 ResumeExperiment()，状态将卡在 Paused，
   导致小球落地后无法触发状态转移至 Finished，WaitForCondition 死循环。
   因此，本脚本在暂停时不调用 PauseExperiment()，仅通过 ballController.PauseAnimation() 暂停动画，
   并由 _isPaused 标志控制流程挂起。小球落地后 StateManager 仍可正常从 Running 转移至 Finished。
*/