using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 临时测试 UI（IMGUI，无需 Canvas）
/// 挂到场景任意 GameObject 即可，自动查找 ProjectileExperimentController。
///
/// 功能：
///   - 实时显示当前步骤 / 运行状态 / 暂停状态
///   - 每个步骤对应的操作按钮
///   - 参数输入框（Step2）
///   - 事件日志滚动窗口
///   - 全局暂停 / 恢复 / 重置按钮（任意阶段）
/// </summary>
public class TempExperimentUI : MonoBehaviour
{
    // ── 依赖 ─────────────────────────────────────────────────────────
    private ProjectileExperimentController _ctrl;
    private ExperimentStateManager         _stateMgr;
    private ExperimentFlowController       _flowCtrl;

    // ── 当前显示状态（由事件驱动更新）────────────────────────────────
    private ExperimentStep    _currentStep     = ExperimentStep.Step1_Prepare;
    private ExperimentRunState _runState       = ExperimentRunState.Idle;
    private bool              _isPaused        = false;

    // ── Step2 参数输入字段 ────────────────────────────────────────────
    private string _inputVelocity  = "10";
    private string _inputAngle     = "45";
    private string _inputDirX      = "0";
    private string _inputDirZ      = "1";
    private string _inputStartY    = "1";
    private string _inputTimeStep  = "0.02";
    private string _inputTotalTime = "5";

    // ── 事件日志 ─────────────────────────────────────────────────────
    private readonly List<string>  _log           = new List<string>();
    private Vector2                _logScroll     = Vector2.zero;
    private const int              MaxLogLines    = 40;

    // ── 仿真结果（Step3 收到后显示）──────────────────────────────────
    private string _simResultText = "（尚未运行）";

    // ── 观测数据（Step4 收到后显示）──────────────────────────────────
    private string _observeText = "（尚未观测）";

    // ── IMGUI 布局常量 ────────────────────────────────────────────────
    private const float PanelW    = 340f;
    private const float PanelH    = 580f;
    private const float LogW      = 360f;
    private const float LogH      = 400f;
    private const float BtnH      = 34f;
    private const float FieldH    = 24f;
    private const float Pad       = 8f;

    // ── GUIStyle 缓存（避免每帧 new）─────────────────────────────────
    private GUIStyle _styleTitle;
    private GUIStyle _styleStep;
    private GUIStyle _styleLog;
    private GUIStyle _styleError;
    private bool     _stylesReady = false;

    // ══════════════════════════════════════════════════════════════════
    // 生命周期
    // ══════════════════════════════════════════════════════════════════

    private void Start()
    {
        _ctrl = FindObjectOfType<ProjectileExperimentController>();
        if (_ctrl == null)
        {
            AddLog("❌ 未找到 ProjectileExperimentController，请先挂载该脚本！");
            return;
        }

        _stateMgr = ExperimentStateManager.Instance;
        if (UserActionManager.Instance != null)
            _flowCtrl = UserActionManager.Instance.GetFlowController();

        BindEvents();
        AddLog("✅ TempUI 初始化完成，等待实验开始...");
    }

    private void OnDestroy()
    {
        if (_ctrl != null) UnbindEvents();
    }

    // ══════════════════════════════════════════════════════════════════
    // 事件绑定
    // ══════════════════════════════════════════════════════════════════

    private void BindEvents()
    {
        _ctrl.OnStepEntered     += OnStepEntered;
        _ctrl.OnParamLoaded     += OnParamLoaded;
        _ctrl.OnParamError      += OnParamError;
        _ctrl.OnSimulationReady += OnSimulationReady;
        _ctrl.OnObserveData     += OnObserveData;
        _ctrl.OnPaused          += OnPaused;
        _ctrl.OnResumed         += OnResumed;
        _ctrl.OnReset           += OnReset;
        _ctrl.OnFlowError       += OnFlowError;
    }

    private void UnbindEvents()
    {
        _ctrl.OnStepEntered     -= OnStepEntered;
        _ctrl.OnParamLoaded     -= OnParamLoaded;
        _ctrl.OnParamError      -= OnParamError;
        _ctrl.OnSimulationReady -= OnSimulationReady;
        _ctrl.OnObserveData     -= OnObserveData;
        _ctrl.OnPaused          -= OnPaused;
        _ctrl.OnResumed         -= OnResumed;
        _ctrl.OnReset           -= OnReset;
        _ctrl.OnFlowError       -= OnFlowError;
    }

    // ── 事件回调 ─────────────────────────────────────────────────────

    private void OnStepEntered(ExperimentStep step)
    {
        _currentStep = step;
        AddLog($"▶ 进入步骤：{StepName(step)}");
    }

    private void OnParamLoaded(float v, float angle, Vector3 dir, Vector3 pos, float dt, float T)
    {
        _inputVelocity  = v.ToString("F2");
        _inputAngle     = angle.ToString("F1");
        _inputDirX      = dir.x.ToString("F2");
        _inputDirZ      = dir.z.ToString("F2");
        _inputStartY    = pos.y.ToString("F2");
        _inputTimeStep  = dt.ToString("F3");
        _inputTotalTime = T.ToString("F1");
        AddLog($"📥 参数已加载：v={v} θ={angle}° dir=({dir.x},{dir.z}) Y={pos.y}");
    }

    private void OnParamError(string msg)
    {
        AddLog($"⚠️ 参数错误：{msg}");
    }

    private void OnSimulationReady(LaunchParamSnapshot snap, System.Collections.Generic.List<Vector3> pts)
    {
        _simResultText = $"轨迹点数：{pts.Count}\n" +
                         $"v={snap.InitialVelocity}m/s  θ={snap.LaunchAngle}°\n" +
                         $"起点：{snap.StartPosition}";
        AddLog($"🎯 仿真完成：{pts.Count} 个轨迹点");
    }

    private void OnObserveData(float xD, float yD, float total, int count)
    {
        _observeText = $"X 位移：{xD:F2} m\n" +
                       $"Y 位移：{yD:F2} m\n" +
                       $"总路程：{total:F2} m\n" +
                       $"轨迹点：{count}";
        AddLog($"📊 观测数据：X={xD:F2}m  Y={yD:F2}m  总路程={total:F2}m");
    }

    private void OnPaused()
    {
        _isPaused = true;
        AddLog("⏸ 实验已暂停");
    }

    private void OnResumed()
    {
        _isPaused = false;
        AddLog("▶ 实验已恢复");
    }

    private void OnReset()
    {
        _currentStep    = ExperimentStep.Step1_Prepare;
        _runState       = ExperimentRunState.Idle;
        _isPaused       = false;
        _simResultText  = "（尚未运行）";
        _observeText    = "（尚未观测）";
        AddLog("🔄 实验已重置，回到 Step1");
    }

    private void OnFlowError(string msg)
    {
        AddLog($"❌ 流程错误：{msg}");
    }

    // ══════════════════════════════════════════════════════════════════
    // IMGUI 渲染
    // ══════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        EnsureStyles();

        // ── 主控制面板（左侧）────────────────────────────────────────
        GUILayout.BeginArea(new Rect(10, 10, PanelW, PanelH), GUI.skin.box);
        DrawControlPanel();
        GUILayout.EndArea();

        // ── 事件日志（右侧）──────────────────────────────────────────
        GUILayout.BeginArea(
            new Rect(Screen.width - LogW - 10, 10, LogW, LogH), GUI.skin.box);
        DrawLogPanel();
        GUILayout.EndArea();
    }

    // ── 主控制面板内容 ────────────────────────────────────────────────

    private void DrawControlPanel()
    {
        // 标题
        GUILayout.Label("🧪 抛体实验测试面板", _styleTitle);
        GUILayout.Space(4);

        // 状态栏
        DrawStatusBar();
        GUILayout.Space(6);

        // 全局按钮（任意阶段有效）
        DrawGlobalButtons();
        GUILayout.Space(6);

        GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true)); // 分割线
        GUILayout.Space(4);

        // 各步骤专属操作
        switch (_currentStep)
        {
            case ExperimentStep.Step1_Prepare:  DrawStep1(); break;
            case ExperimentStep.Step2_SetParam:  DrawStep2(); break;
            case ExperimentStep.Step3_RunSim:    DrawStep3(); break;
            case ExperimentStep.Step4_Observe:   DrawStep4(); break;
            case ExperimentStep.Step5_Finish:    DrawStep5(); break;
        }
    }

    private void DrawStatusBar()
    {
        // 步骤进度条
        GUILayout.BeginHorizontal();
        for (int i = 0; i <= 4; i++)
        {
            ExperimentStep s = (ExperimentStep)i;
            bool isCurrent = (s == _currentStep);
            GUI.color = isCurrent ? Color.cyan : new Color(0.6f, 0.6f, 0.6f);
            GUILayout.Button(StepShortName(s),
                GUILayout.Height(22), GUILayout.ExpandWidth(true));
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(3);

        // 运行状态 + 暂停状态
        string runStateStr = _stateMgr != null
            ? RunStateName(_stateMgr.CurrentRunState)
            : "—";
        string pauseStr = _isPaused ? " | ⏸ 已暂停" : "";
        GUILayout.Label($"运行状态：{runStateStr}{pauseStr}", _styleStep);
    }

    private void DrawGlobalButtons()
    {
        GUILayout.Label("── 全局控制（任意阶段）", EditorLabel());
        GUILayout.BeginHorizontal();

        if (_isPaused)
        {
            GUI.color = Color.green;
            if (GUILayout.Button("▶ 恢复", GUILayout.Height(BtnH)))
                _ctrl?.RequestResume();
        }
        else
        {
            GUI.color = Color.yellow;
            if (GUILayout.Button("⏸ 暂停", GUILayout.Height(BtnH)))
                _ctrl?.RequestPause();
        }

        GUI.color = new Color(1f, 0.5f, 0.3f);
        if (GUILayout.Button("🔄 重置", GUILayout.Height(BtnH)))
            _ctrl?.RequestReset();

        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    // ── Step1 ─────────────────────────────────────────────────────────

    private void DrawStep1()
    {
        GUILayout.Label("📋 Step1：准备阶段", _styleStep);
        GUILayout.Label("检查实验场景，确认小球、轨迹线等已就位。", EditorLabel());
        GUILayout.Space(8);

        GUI.color = Color.cyan;
        if (GUILayout.Button("✅ 确认准备完成", GUILayout.Height(BtnH + 6)))
            _ctrl?.ConfirmPrepare();
        GUI.color = Color.white;
    }

    // ── Step2 ─────────────────────────────────────────────────────────

    private void DrawStep2()
    {
        GUILayout.Label("⚙️ Step2：参数设置", _styleStep);

        GUILayout.BeginHorizontal();
        GUILayout.Label("初速度 (m/s):", GUILayout.Width(110));
        _inputVelocity = GUILayout.TextField(_inputVelocity, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("仰角 (°):", GUILayout.Width(110));
        _inputAngle = GUILayout.TextField(_inputAngle, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("方向 X / Z:", GUILayout.Width(110));
        _inputDirX = GUILayout.TextField(_inputDirX, GUILayout.Height(FieldH));
        _inputDirZ = GUILayout.TextField(_inputDirZ, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("起点高度 Y:", GUILayout.Width(110));
        _inputStartY = GUILayout.TextField(_inputStartY, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("时间步长 (s):", GUILayout.Width(110));
        _inputTimeStep = GUILayout.TextField(_inputTimeStep, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("最大时长 (s):", GUILayout.Width(110));
        _inputTotalTime = GUILayout.TextField(_inputTotalTime, GUILayout.Height(FieldH));
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUI.color = Color.yellow;
        if (GUILayout.Button("📤 写入参数（预览）", GUILayout.Height(BtnH)))
            PushParamsToController();
        GUI.color = Color.white;

        GUILayout.Space(2);

        GUI.color = Color.cyan;
        if (GUILayout.Button("✅ 确认参数，开始仿真", GUILayout.Height(BtnH + 4)))
        {
            PushParamsToController();
            _ctrl?.ConfirmParam();
        }
        GUI.color = Color.white;
    }

    // ── Step3 ─────────────────────────────────────────────────────────

    private void DrawStep3()
    {
        GUILayout.Label("🚀 Step3：仿真运行中", _styleStep);
        GUILayout.Space(4);
        GUILayout.Label("仿真结果：", EditorLabel());
        GUILayout.Box(_simResultText, GUILayout.ExpandWidth(true));
        GUILayout.Space(4);
        GUILayout.Label("小球动画正在播放，等待落地...\n可使用上方\"暂停/恢复\"按钮控制。",
            EditorLabel());
    }

    // ── Step4 ─────────────────────────────────────────────────────────

    private void DrawStep4()
    {
        GUILayout.Label("📊 Step4：观察数据", _styleStep);
        GUILayout.Space(4);
        GUILayout.Box(_observeText, GUILayout.ExpandWidth(true));
        GUILayout.Space(8);

        GUI.color = Color.cyan;
        if (GUILayout.Button("✅ 观察完毕，进入结束", GUILayout.Height(BtnH + 4)))
            _ctrl?.ConfirmObserved();
        GUI.color = Color.white;
    }

    // ── Step5 ─────────────────────────────────────────────────────────

    private void DrawStep5()
    {
        GUILayout.Label("🏁 Step5：实验结束", _styleStep);
        GUILayout.Space(4);
        GUILayout.Label("实验数据已记录完毕。\n点击\"完成\"将重置并开始新一轮实验。",
            EditorLabel());
        GUILayout.Space(8);

        GUI.color = Color.green;
        if (GUILayout.Button("🎉 完成，再来一次", GUILayout.Height(BtnH + 6)))
            _ctrl?.ConfirmFinish();
        GUI.color = Color.white;
    }

    // ── 日志面板 ──────────────────────────────────────────────────────

    private void DrawLogPanel()
    {
        GUILayout.Label("📋 事件日志", _styleTitle);
        GUILayout.Space(2);

        _logScroll = GUILayout.BeginScrollView(_logScroll,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // 最新日志在最上面
        for (int i = _log.Count - 1; i >= 0; i--)
            GUILayout.Label(_log[i], _styleLog);

        GUILayout.EndScrollView();

        if (GUILayout.Button("清空日志", GUILayout.Height(24)))
            _log.Clear();
    }

    // ══════════════════════════════════════════════════════════════════
    // 工具方法
    // ══════════════════════════════════════════════════════════════════

    /// <summary>读取输入框内容，调用 SetParam 写入控制器</summary>
    private void PushParamsToController()
    {
        if (_ctrl == null) return;

        bool valid = true;
        float.TryParse(_inputVelocity,  out float v);
        float.TryParse(_inputAngle,     out float angle);
        float.TryParse(_inputDirX,      out float dx);
        float.TryParse(_inputDirZ,      out float dz);
        float.TryParse(_inputStartY,    out float sy);
        float.TryParse(_inputTimeStep,  out float dt);
        float.TryParse(_inputTotalTime, out float T);

        // 简单防守：非法或缺省值警告
        if (v <= 0) { AddLog("⚠️ 初速度必须大于0"); valid = false; }
        if (angle < 0 || angle > 90) { AddLog("⚠️ 仰角应在0~90度"); valid = false; }
        if (sy <= 0) { AddLog("⚠️ 起点高度Y必须大于0"); valid = false; }
        if (dt <= 0 || dt > 1) { AddLog("⚠️ 时间步长应在(0,1]秒"); valid = false; }
        if (T <= 0 || T > 60) { AddLog("⚠️ 最大时长应在(0,60]秒"); valid = false; }
        if (Mathf.Abs(dx) < 1e-4f && Mathf.Abs(dz) < 1e-4f) { AddLog("⚠️ 水平方向不能为零"); valid = false; }

        if (!valid) return;

        _ctrl.SetParam(
            velocity:      v,
            angle:         angle,
            direction:     new Vector3(dx, 0f, dz),
            startPosition: new Vector3(0f, sy, 0f),
            timeStep:      dt,
            totalTime:     T);

        AddLog($"📤 参数写入：v={v}  θ={angle}°  dir=({dx},{dz})  Y={sy}  dt={dt}  T={T}");
    }

    private void AddLog(string msg)
    {
        string time = System.DateTime.Now.ToString("HH:mm:ss");
        _log.Add($"[{time}] {msg}");
        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);
        // 滚动到底部（最新）
        _logScroll = new Vector2(0, float.MaxValue);
    }

    // ── 名称映射 ─────────────────────────────────────────────────────

    private static string StepName(ExperimentStep s) => s switch
    {
        ExperimentStep.Step1_Prepare   => "Step1 准备",
        ExperimentStep.Step2_SetParam  => "Step2 参数",
        ExperimentStep.Step3_RunSim    => "Step3 仿真",
        ExperimentStep.Step4_Observe   => "Step4 观察",
        ExperimentStep.Step5_Finish    => "Step5 结束",
        _                              => s.ToString()
    };

    private static string StepShortName(ExperimentStep s) => s switch
    {
        ExperimentStep.Step1_Prepare   => "1.准备",
        ExperimentStep.Step2_SetParam  => "2.参数",
        ExperimentStep.Step3_RunSim    => "3.仿真",
        ExperimentStep.Step4_Observe   => "4.观察",
        ExperimentStep.Step5_Finish    => "5.结束",
        _                              => s.ToString()
    };

    private static string RunStateName(ExperimentRunState s) => s switch
    {
        ExperimentRunState.Idle     => "空闲 💤",
        ExperimentRunState.Running  => "运行中 🟢",
        ExperimentRunState.Paused   => "已暂停 🟡",
        ExperimentRunState.Finished => "已完成 ✅",
        _                           => s.ToString()
    };

    // ── GUIStyle 初始化 ───────────────────────────────────────────────

    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _styleTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white }
        };

        _styleStep = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.cyan }
        };

        _styleLog = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            wordWrap  = true,
            normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };

        _styleError = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal   = { textColor = Color.red }
        };

        _stylesReady = true;
    }

    private static GUIStyle EditorLabel()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal   = { textColor = new Color(0.75f, 0.75f, 0.75f) },
            wordWrap = true
        };
        return s;
    }
}
