using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║              TempModuleValidator — 模块联调测试脚本               ║
/// ║  用途：在框架（ExperimentStateManager / FlowController 等）       ║
/// ║        尚未完成时，独立验证三个模块的全部对外接口                    ║
/// ║  用完即删：正式联调完成后请从项目中移除本脚本                        ║
/// ╚══════════════════════════════════════════════════════════════════╝
///
/// ──────────────────── 使用方法 ────────────────────
///  Step 1. 将本脚本挂载到场景中任意 GameObject（推荐新建空对象命名为
///          "ModuleValidator"）。
///
///  Step 2. 在 Inspector 中：
///          • Ball Controller   → 拖入挂载了 ProjectileBallController
///                               的小球 GameObject
///          • 其余参数保持默认即可，按需调整
///
///  Step 3. 运行游戏后用键盘触发测试（详见下方按键说明），
///          或在 Inspector 右键本组件 → ContextMenu 按钮点击。
///
/// ──────────────────── 键盘快捷键 ────────────────────
///  [1]  PhysicsSimulationCore  全套测试（合法/非法参数、三种快捷方法）
///  [2]  SimulationDataBuffer   全套测试（写入/读取/清空/校验）
///  [3]  播放动画（需先运行 [1]+[2] 写入数据）
///  [4]  暂停动画
///  [5]  恢复动画
///  [6]  重置小球
///  [7]  SetGroundHeight 专项测试
///  [0]  一键运行全部测试（含自动播放→暂停→恢复→重置流程）
///
/// ──────────────────── 看什么输出 ────────────────────
///  • [PASS] 绿色：接口按预期工作
///  • [FAIL] 红色：接口异常，需检查
///  • [WARN] 黄色：预期触发的 Warning（属于正常测试用例）
///  • Unity Console 左上角过滤 "[Validator]" 可聚焦本脚本日志
/// </summary>
public class TempModuleValidator : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────────

    [Header("必填：小球控制器引用")]
    [Tooltip("拖入挂载了 ProjectileBallController 的小球 GameObject")]
    public ProjectileBallController ballController;

    [Header("物理测试参数（可按需修改）")]
    [Tooltip("合法初速度（m/s）")]
    public float validVelocity = 5f;

    [Tooltip("合法起点高度（y > 0），仅用于纯物理测试（[1][7]），动画测试始终使用小球真实位置")]
    public float validStartY = 2f;

    [Tooltip("斜抛仰角（°）")]
    [Range(0f, 89f)]
    public float launchAngle = 30f;

    [Header("动画测试参数")]
    [Tooltip("自动测试中，动画播放多少秒后自动暂停")]
    public float autoPauseAfterSeconds = 0.8f;

    [Tooltip("暂停多少秒后自动恢复")]
    public float autoResumeAfterSeconds = 1.0f;

    [Tooltip("恢复后多少秒后自动重置")]
    public float autoResetAfterSeconds  = 1.5f;

    // ── 私有状态 ─────────────────────────────────────────────────────

    private const string TAG = "[Validator]";
    private int _passCount;
    private int _failCount;

    // ── Unity 生命周期 ───────────────────────────────────────────────

    private void Start()
    {
        Log("脚本已启动。按 [0]~[7] 触发对应测试，详见脚本顶部注释。");

        if (ballController == null)
            LogWarn("ballController 未赋值，动画相关测试（[3][4][5][6][0]）将跳过。");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) RunPhysicsCoreTests();
        if (Input.GetKeyDown(KeyCode.Alpha2)) RunDataBufferTests();
        if (Input.GetKeyDown(KeyCode.Alpha3)) DoPlayAnimation();
        if (Input.GetKeyDown(KeyCode.Alpha4)) DoPauseAnimation();
        if (Input.GetKeyDown(KeyCode.Alpha5)) DoResumeAnimation();
        if (Input.GetKeyDown(KeyCode.Alpha6)) DoResetBall();
        if (Input.GetKeyDown(KeyCode.Alpha7)) RunSetGroundHeightTest();
        if (Input.GetKeyDown(KeyCode.Alpha0)) StartCoroutine(RunAllTestsCoroutine());
    }

    // ════════════════════════════════════════════════════════════════
    //  工具：获取动画用发射起点
    //  ★ 修复核心：始终使用小球在场景中的真实世界坐标，
    //    而非硬编码的 (0, validStartY, 0)，避免小球瞬移。
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 返回小球在场景中的真实世界坐标，作为动画轨迹的发射起点。
    /// 若 ballController 未赋值，则回退到 (0, validStartY, 0)（仅用于纯物理测试）。
    /// </summary>
    private Vector3 GetBallStartPosition()
    {
        if (ballController != null)
            return ballController.transform.position;

        // 无小球引用时的回退值（纯物理测试时使用）
        return new Vector3(0f, validStartY, 0f);
    }

    /// <summary>
    /// 使用小球真实位置生成动画用轨迹，并写入 SimulationDataBuffer。
    /// ★ 修复：所有动画相关测试统一走此方法，确保 points[0] == 小球初始位置。
    /// </summary>
    private void WriteAnimationTrajectory()
    {
        // ★ 使用小球真实位置作为发射起点，而非硬编码坐标
        Vector3 startPos = GetBallStartPosition();

        var pts = PhysicsSimulationCore.SimulateProjectileMotion(
            validVelocity,
            0f,               // 平抛（仰角=0）
            Vector3.forward,  // 向 +Z 方向发射
            startPos,         // ★ 小球真实世界坐标
            0.02f,
            5f);

        SimulationDataBuffer.UpdateTrajectoryData(
            pts,
            new LaunchParamSnapshot(
                validVelocity, 0f, Vector3.forward,
                startPos, 0.02f, 5f));
    }

    // ════════════════════════════════════════════════════════════════
    //  [1] PhysicsSimulationCore 全套测试
    //  （纯物理验证，不涉及小球位置，起点保持 validStartY 不变）
    // ════════════════════════════════════════════════════════════════

    [ContextMenu("测试 [1] PhysicsSimulationCore")]
    public void RunPhysicsCoreTests()
    {
        BeginGroup("PhysicsSimulationCore 测试");

        // TC1 — 合法平抛（ClassicProjectile）
        {
            var pts = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
            Check("TC1 ClassicProjectile 返回非空列表", pts != null && pts.Count > 0);
            Check("TC1 起点y坐标与 startY 吻合",
                  pts != null && pts.Count > 0 &&
                  Mathf.Approximately(pts[0].y, validStartY));
            if (pts != null && pts.Count > 0)
                Check("TC1 终点y坐标 <= 0（落到地面）",
                      pts[pts.Count - 1].y <= 0.001f);
        }

        // TC2 — 45° 斜抛（OptimalAngleProjectile）
        {
            var pts = PhysicsSimulationCore.OptimalAngleProjectile(validVelocity, validStartY);
            Check("TC2 OptimalAngleProjectile 返回非空列表", pts != null && pts.Count > 0);
            // 45° 斜抛最高点 y 应高于起点
            if (pts != null && pts.Count > 2)
            {
                float maxY = 0f;
                foreach (var p in pts) if (p.y > maxY) maxY = p.y;
                Check("TC2 45°斜抛最高点 y > 起点 y", maxY > validStartY);
            }
        }

        // TC3 — 自定义斜抛（CustomProjectile，沿 X 轴方向）
        {
            var pts = PhysicsSimulationCore.CustomProjectile(
                validVelocity, launchAngle,
                Vector3.right,
                new Vector3(0f, validStartY, 0f));
            Check("TC3 CustomProjectile(沿X轴) 返回非空列表", pts != null && pts.Count > 0);
            // 水平方向沿 X，Z 应保持不变
            if (pts != null && pts.Count > 1)
                Check("TC3 CustomProjectile(沿X轴) Z坐标始终为0",
                      Mathf.Approximately(pts[pts.Count / 2].z, 0f));
        }

        // TC4 — 完整参数接口（SimulateProjectileMotion）
        {
            var pts = PhysicsSimulationCore.SimulateProjectileMotion(
                validVelocity, launchAngle,
                new Vector3(1f, 0f, 1f),          // 对角线方向
                new Vector3(0f, validStartY, 0f),
                0.02f, 5f);
            Check("TC4 SimulateProjectileMotion(对角线方向) 返回非空列表",
                  pts != null && pts.Count > 0);
        }

        // TC5 — 点数上限：极小 timeStep，预期触发 MaxTrajectoryPoints 截断警告
        {
            LogWarn("TC5：以下 LogWarning 属于预期行为（MaxTrajectoryPoints 截断）");
            var pts = PhysicsSimulationCore.SimulateProjectileMotion(
                validVelocity, 0f, Vector3.forward,
                new Vector3(0f, validStartY, 0f),
                0.00001f,   // 极小步长，会触发截断
                5f);
            Check("TC5 极小步长时点数 <= MaxTrajectoryPoints",
                  pts != null && pts.Count <= PhysicsSimulationCore.MaxTrajectoryPoints);
        }

        // TC6 — 非法参数：initialVelocity = 0，预期返回空列表
        {
            LogWarn("TC6：以下 LogWarning 属于预期行为（参数非法）");
            var pts = PhysicsSimulationCore.SimulateProjectileMotion(
                0f, 0f, Vector3.forward,
                new Vector3(0f, validStartY, 0f),
                0.02f, 5f);
            Check("TC6 initialVelocity=0 返回空列表", pts != null && pts.Count == 0);
        }

        // TC7 — 非法参数：起点在地面以下，预期返回空列表
        {
            LogWarn("TC7：以下 LogWarning 属于预期行为（起点 y <= 0）");
            var pts = PhysicsSimulationCore.SimulateProjectileMotion(
                validVelocity, 0f, Vector3.forward,
                new Vector3(0f, -1f, 0f),   // y < 0
                0.02f, 5f);
            Check("TC7 起点y<0 返回空列表", pts != null && pts.Count == 0);
        }

        // TC8 — launchDirection 水平分量为零，预期自动回退 forward
        {
            LogWarn("TC8：以下 LogWarning 属于预期行为（方向回退 forward）");
            var pts = PhysicsSimulationCore.SimulateProjectileMotion(
                validVelocity, 0f,
                Vector3.up,                  // 水平分量 = 0
                new Vector3(0f, validStartY, 0f),
                0.02f, 5f);
            Check("TC8 方向水平分量=0 自动回退后仍返回有效数据",
                  pts != null && pts.Count > 0);
        }

        // TC9 — 落地点精度：最后一个点的 y 应 <= 0（而非过冲到负值）
        {
            var pts = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
            if (pts != null && pts.Count > 0)
            {
                float lastY = pts[pts.Count - 1].y;
                Check($"TC9 落地点 y={lastY:F4} 在 [-0.01, 0.001] 范围内（精确落地）",
                      lastY >= -0.01f && lastY <= 0.001f);
            }
        }

        EndGroup();
    }

    // ════════════════════════════════════════════════════════════════
    //  [2] SimulationDataBuffer 全套测试
    // ════════════════════════════════════════════════════════════════

    [ContextMenu("测试 [2] SimulationDataBuffer")]
    public void RunDataBufferTests()
    {
        BeginGroup("SimulationDataBuffer 测试");

        // 先清空，保证起点干净
        SimulationDataBuffer.ClearData();

        // TC10 — 清空后 HasValidData 应为 false
        Check("TC10 ClearData 后 HasValidData() == false",
              SimulationDataBuffer.HasValidData() == false);
        Check("TC10 ClearData 后 TrajectoryPointCount == 0",
              SimulationDataBuffer.TrajectoryPointCount == 0);

        // TC11 — 写入有效数据（不带 snapshot）
        var pts = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
        SimulationDataBuffer.UpdateTrajectoryData(pts);
        Check("TC11 UpdateTrajectoryData 后 HasValidData() == true",
              SimulationDataBuffer.HasValidData());
        Check("TC11 TrajectoryPointCount == 写入点数",
              SimulationDataBuffer.TrajectoryPointCount == pts.Count);
        Check("TC11 CurrentTrajectoryPoints 引用非空",
              SimulationDataBuffer.CurrentTrajectoryPoints != null);

        // TC12 — 写入带 snapshot 的数据
        var snapshot = new LaunchParamSnapshot(
            validVelocity, 0f, Vector3.forward,
            new Vector3(0f, validStartY, 0f),
            0.02f, 5f);
        SimulationDataBuffer.UpdateTrajectoryData(pts, snapshot);
        Check("TC12 写入 snapshot 后 LastLaunchParams.InitialVelocity 正确",
              Mathf.Approximately(SimulationDataBuffer.LastLaunchParams.InitialVelocity,
                                  validVelocity));
        Check("TC12 写入 snapshot 后 LastLaunchParams.LaunchAngle == 0",
              Mathf.Approximately(SimulationDataBuffer.LastLaunchParams.LaunchAngle, 0f));

        // TC13 — 写入空列表，预期触发 Warning 并清空缓冲区
        LogWarn("TC13：以下 LogWarning 属于预期行为（写入空数据）");
        SimulationDataBuffer.UpdateTrajectoryData(new List<Vector3>());
        Check("TC13 写入空列表后 HasValidData() == false",
              SimulationDataBuffer.HasValidData() == false);

        // TC14 — 写入 null，预期触发 Warning 并清空缓冲区
        LogWarn("TC14：以下 LogWarning 属于预期行为（写入 null）");
        SimulationDataBuffer.UpdateTrajectoryData(null);
        Check("TC14 写入 null 后 HasValidData() == false",
              SimulationDataBuffer.HasValidData() == false);

        // TC15 — ClearData 功能
        SimulationDataBuffer.UpdateTrajectoryData(pts, snapshot); // 先写入
        SimulationDataBuffer.ClearData();
        Check("TC15 ClearData 后 CurrentTrajectoryPoints == null",
              SimulationDataBuffer.CurrentTrajectoryPoints == null);
        Check("TC15 ClearData 后 HasValidData() == false",
              SimulationDataBuffer.HasValidData() == false);
        Check("TC15 ClearData 后 TrajectoryPointCount == 0",
              SimulationDataBuffer.TrajectoryPointCount == 0);

        // TC16 — 多次写入：后写覆盖前写
        var pts1 = PhysicsSimulationCore.ClassicProjectile(3f, validStartY);
        var pts2 = PhysicsSimulationCore.ClassicProjectile(8f, validStartY);
        SimulationDataBuffer.UpdateTrajectoryData(pts1);
        SimulationDataBuffer.UpdateTrajectoryData(pts2);
        Check("TC16 多次写入后 TrajectoryPointCount 等于最后一次写入的点数",
              SimulationDataBuffer.TrajectoryPointCount == pts2.Count);

        EndGroup();

        // ★ 修复：恢复有效数据供后续动画测试使用时，
        //   使用小球真实世界坐标作为起点，而非硬编码 (0, validStartY, 0)。
        //   否则按 [3] 后小球会瞬移到错误位置。
        WriteAnimationTrajectory();
        Log("缓冲区已恢复有效数据（起点=小球真实位置），可直接按 [3] 测试动画播放。");
    }

    // ════════════════════════════════════════════════════════════════
    //  [7] SetGroundHeight 专项测试
    // ════════════════════════════════════════════════════════════════

    [ContextMenu("测试 [7] SetGroundHeight")]
    public void RunSetGroundHeightTest()
    {
        BeginGroup("SetGroundHeight 专项测试");

        // 恢复默认地面高度（测试结束后必须还原）
        PhysicsSimulationCore.SetGroundHeight(0f);

        // TC17 — 地面抬高到 y=0.5，落地点应 <= 0.5
        PhysicsSimulationCore.SetGroundHeight(0.5f);
        var pts05 = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
        if (pts05 != null && pts05.Count > 0)
        {
            float lastY = pts05[pts05.Count - 1].y;
            Check($"TC17 地面=0.5 时终点 y={lastY:F4} <= 0.501",
                  lastY <= 0.501f);
            // 点数应比默认地面（y=0）少（因为更早落地）
            var ptsDefault = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
            // ptsDefault 此时地面=0.5，需先还原才能比较
            PhysicsSimulationCore.SetGroundHeight(0f);
            var ptsGround0 = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
            Check("TC17 地面抬高时轨迹点数 < 默认地面（更早落地）",
                  pts05.Count < ptsGround0.Count);
        }
        else
        {
            PhysicsSimulationCore.SetGroundHeight(0f);
        }

        // TC18 — 地面高度恢复 0 后正常计算
        PhysicsSimulationCore.SetGroundHeight(0f);
        var ptsRestored = PhysicsSimulationCore.ClassicProjectile(validVelocity, validStartY);
        Check("TC18 恢复地面=0 后轨迹正常", ptsRestored != null && ptsRestored.Count > 0);

        EndGroup();
    }

    // ════════════════════════════════════════════════════════════════
    //  [3][4][5][6] ProjectileBallController 单步测试
    // ════════════════════════════════════════════════════════════════

    [ContextMenu("测试 [3] 播放动画")]
    public void DoPlayAnimation()
    {
        if (!CheckBallController()) return;

        // ★ 修复：若缓冲区无数据，使用小球真实位置生成轨迹后再播放，
        //   原来此处写入 ClassicProjectile（起点硬编码为 (0,validStartY,0)）
        //   会导致小球瞬移到场景中完全不同的位置。
        if (!SimulationDataBuffer.HasValidData())
        {
            WriteAnimationTrajectory();
            Log("缓冲区无数据，已以小球真实位置为起点自动写入平抛轨迹。");
        }

        ballController.PlayAnimation();
        Log("[3] PlayAnimation() 已调用。观察小球是否从原地沿抛物线运动 + LineRenderer 轨迹是否绘制。");
    }

    [ContextMenu("测试 [4] 暂停动画")]
    public void DoPauseAnimation()
    {
        if (!CheckBallController()) return;
        ballController.PauseAnimation();
        Log("[4] PauseAnimation() 已调用。小球应停在当前位置。");
    }

    [ContextMenu("测试 [5] 恢复动画")]
    public void DoResumeAnimation()
    {
        if (!CheckBallController()) return;
        ballController.ResumeAnimation();
        Log("[5] ResumeAnimation() 已调用。小球应从暂停位置继续运动。");
    }

    [ContextMenu("测试 [6] 重置小球")]
    public void DoResetBall()
    {
        if (!CheckBallController()) return;
        ballController.ResetBall();
        Log("[6] ResetBall() 已调用。小球应回到初始位置，轨迹线应清空。");
    }

    // ════════════════════════════════════════════════════════════════
    //  [0] 全自动集成测试（协程驱动）
    // ════════════════════════════════════════════════════════════════

    [ContextMenu("测试 [0] 全自动集成测试")]
    public void RunAllTests()
    {
        StartCoroutine(RunAllTestsCoroutine());
    }

    private IEnumerator RunAllTestsCoroutine()
    {
        _passCount = 0;
        _failCount = 0;

        Log("══════════ 全自动集成测试开始 ══════════");

        // Phase 1 — 物理核心
        RunPhysicsCoreTests();
        yield return null;

        // Phase 2 — 数据缓冲区
        RunDataBufferTests();
        yield return null;

        // Phase 3 — SetGroundHeight
        RunSetGroundHeightTest();
        yield return null;

        // Phase 4 — 动画全流程（需要 ballController）
        if (ballController != null)
        {
            BeginGroup("ProjectileBallController 动画流程测试");

            // ★ 修复：使用小球真实位置生成轨迹，确保动画从原地开始
            WriteAnimationTrajectory();

            // 播放
            ballController.PlayAnimation();
            Log($"► PlayAnimation() 已调用，等待 {autoPauseAfterSeconds}s 后暂停...");
            yield return new WaitForSeconds(autoPauseAfterSeconds);

            // 暂停
            ballController.PauseAnimation();
            Log($"► PauseAnimation() 已调用，等待 {autoResumeAfterSeconds}s 后恢复...");
            yield return new WaitForSeconds(autoResumeAfterSeconds);

            // 恢复
            ballController.ResumeAnimation();
            Log($"► ResumeAnimation() 已调用，等待 {autoResetAfterSeconds}s 后重置...");
            yield return new WaitForSeconds(autoResetAfterSeconds);

            // 重置
            ballController.ResetBall();
            Log("► ResetBall() 已调用。");
            yield return new WaitForSeconds(0.5f);

            // TC19 — ResetBall 后缓冲区数据应保持不变（重置不清空缓冲区）
            Check("TC19 ResetBall 不清空 SimulationDataBuffer",
                  SimulationDataBuffer.HasValidData());

            // TC20 — 缓冲区无数据时调用 PlayAnimation 应打印警告而非崩溃
            SimulationDataBuffer.ClearData();
            LogWarn("TC20：以下 LogWarning 属于预期行为（无数据时播放）");
            ballController.PlayAnimation();  // 应打印 Warning 并安全返回
            yield return null;
            Check("TC20 无数据时 PlayAnimation 不崩溃（执行到此处即通过）", true);

            EndGroup();
        }
        else
        {
            LogWarn("ballController 未赋值，跳过动画流程测试（Phase 4）。");
        }

        // 汇总
        yield return null;
        PrintSummary();
    }

    // ════════════════════════════════════════════════════════════════
    //  工具方法
    // ════════════════════════════════════════════════════════════════

    private void BeginGroup(string groupName)
    {
        Debug.Log($"\n{TAG} ──────── {groupName} ────────");
    }

    private void EndGroup()
    {
        Debug.Log($"{TAG} ──────── 本组测试结束 ────────\n");
    }

    private void Check(string desc, bool condition)
    {
        if (condition)
        {
            _passCount++;
            Debug.Log($"<color=green>{TAG} [PASS]</color> {desc}");
        }
        else
        {
            _failCount++;
            Debug.LogError($"{TAG} [FAIL] {desc}");
        }
    }

    private void Log(string msg)
    {
        Debug.Log($"{TAG} {msg}");
    }

    private void LogWarn(string msg)
    {
        Debug.LogWarning($"{TAG} [WARN（预期）] {msg}");
    }

    private bool CheckBallController()
    {
        if (ballController != null) return true;
        Debug.LogWarning($"{TAG} ballController 未赋值，请在 Inspector 中拖入小球 GameObject。");
        return false;
    }

    private void PrintSummary()
    {
        string color = _failCount == 0 ? "green" : "red";
        Debug.Log(
            $"<color={color}>{TAG} ══ 全部测试完成：" +
            $"PASS={_passCount}  FAIL={_failCount} ══</color>");

        if (_failCount > 0)
            Debug.LogError($"{TAG} 有 {_failCount} 项测试未通过，请查看上方 [FAIL] 日志。");
    }
}
