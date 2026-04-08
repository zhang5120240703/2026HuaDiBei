using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物理模拟数据缓冲区（静态数据中转站）—— 三维版本
///
/// 模块定位：物理模拟模块对外暴露数据的唯一出口。
///   【写入方】交互逻辑：在调用 PhysicsSimulationCore 后写入 UpdateTrajectoryData()
///   【读取方】UI绘制模块，AI分析模块：通过 CurrentTrajectoryPoints 消费数据
///   【清空方】重置事件时，由交互逻辑或系统集成调用 ClearData()
///   【小球端】ProjectileBallController 通过 HasValidData() + CurrentTrajectoryPoints 驱动动画
///
/// 三维适配说明：
/// 
///   三维版本：List&lt;Vector3&gt;（世界空间三维坐标，由 PhysicsSimulationCore 计算得出）
///
/// 设计原则：静态类，全局唯一，不持有 MonoBehaviour，不操作任何 GameObject 或 UI。
/// </summary>
using System.Text;
using System.IO;
public static class SimulationDataBuffer
{
    // ── 核心数据属性 ─────────────────────────────────────────────────

    /// <summary>
    /// 当前最新计算出的三维轨迹点列表（世界坐标，只读）。
    /// 供 UI绘制模块、AI分析模块、ProjectileBallController 读取。
    /// 为 null 或空列表表示当前无有效数据。
    /// </summary>
    public static List<Vector3> CurrentTrajectoryPoints { get; private set; }

    /// <summary>
    /// 以小球发射原点为坐标系原点的相对轨迹点列表（只读）。
    ///
    /// 换算规则：
    ///   RelativePoint[i] = CurrentTrajectoryPoints[i] - CurrentTrajectoryPoints[0]
    ///   每个分量保留一位小数（四舍五入）。
    ///
    /// 含义：
    ///   • (0.0, 0.0, 0.0) 始终是小球发射起点
    ///   • X / Y / Z 分量分别表示相对于起点的东西 / 高低 / 南北位移（单位：米）
    ///
    /// 适用场景：AI分析模块、数据导出、UI数值展示等需要"以小球为原点"的场景。
    /// 数据为空时返回 null。
    /// </summary>
    public static List<Vector3> RelativeTrajectoryPoints { get; private set; }

    /// <summary>
    /// 本次相对坐标系所使用的世界空间原点（即小球发射起点的世界坐标，只读）。
    /// 供需要将相对坐标还原为世界坐标的模块使用：
    ///   世界坐标 = RelativeTrajectoryPoints[i] + LocalOrigin
    /// 数据清空后重置为 Vector3.zero。
    /// </summary>
    public static Vector3 LocalOrigin { get; private set; }

    /// <summary>
    /// 本次计算所用的发射参数快照（只读）。
    /// 供 AI分析模块或 UI 展示参数信息时读取，无需重新传参。
    /// </summary>
    public static LaunchParamSnapshot LastLaunchParams { get; private set; }

    /// <summary>
    /// 小球在世界坐标 X 轴方向的位移（取绝对值，单位：米）。
    /// 由轨迹起点和终点计算：|end.x - start.x|
    /// </summary>
    public static float XDistance { get; private set; }

    /// <summary>
    /// 小球在世界坐标 Y 轴方向的位移（取绝对值，单位：米）。
    /// 由轨迹起点和终点计算：|end.y - start.y|
    /// </summary>
    public static float YDistance { get; private set; }

    /// <summary>
    /// 小球沿轨迹的总行进距离（路径长度，单位：米）。
    /// 累加相邻轨迹点间距离得出。
    /// </summary>
    public static float TotalDistance { get; private set; }

    // ── 公开方法 ─────────────────────────────────────────────────────

    /// <summary>
    /// 写入最新三维轨迹数据，同时自动生成以小球原点为坐标系的相对坐标列表。
    /// 调用时机：PhysicsSimulationCore.SimulateProjectileMotion 计算完成后，
    ///           由交互逻辑模块负责调用。
    /// </summary>
    /// <param name="newPoints">最新计算的三维轨迹点列表（世界坐标）</param>
    /// <param name="snapshot">
    ///   可选：本次发射参数快照。传入后可供其他模块查询本次实验参数。
    /// </param>
    public static void UpdateTrajectoryData(
        List<Vector3> newPoints,
        LaunchParamSnapshot snapshot = default)
    {
        if (newPoints == null || newPoints.Count == 0)
        {
            Debug.LogWarning("[SimulationDataBuffer] UpdateTrajectoryData 收到空数据，轨迹缓冲区已被清空。");
            CurrentTrajectoryPoints = new List<Vector3>();
            RelativeTrajectoryPoints = null;
            LocalOrigin = Vector3.zero;
            XDistance = 0f;
            YDistance = 0f;
            TotalDistance = 0f;
            return;
        }

        CurrentTrajectoryPoints = newPoints;
        LastLaunchParams = snapshot;

        // 计算位移与路径长度
        var start = newPoints[0];
        var end = newPoints[newPoints.Count - 1];

        XDistance = Mathf.Abs(end.x - start.x);
        YDistance = Mathf.Abs(end.y - start.y);

        // 性能优化：手动展开距离计算，避免Vector3.Distance
        float pathLen = 0f;
        for (int i = 1; i < newPoints.Count; i++)
        {
            Vector3 a = newPoints[i - 1];
            Vector3 b = newPoints[i];
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float dz = b.z - a.z;
            pathLen += Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        TotalDistance = pathLen;

        // ── 生成相对坐标列表 ──────────────────────────────────────────
        LocalOrigin = start;
        RelativeTrajectoryPoints = new List<Vector3>(newPoints.Count);
        for (int i = 0; i < newPoints.Count; i++)
        {
            Vector3 delta = newPoints[i] - start;
            RelativeTrajectoryPoints.Add(RoundVector3(delta, 1));
        }

        Debug.Log($"[SimulationDataBuffer] 三维轨迹数据已更新，" +
                  $"共 {CurrentTrajectoryPoints.Count} 个点。 " +
                  $"起点={start}  终点={end} " +
                  $"XDistance={XDistance:F3}m  YDistance={YDistance:F3}m  TotalDistance={TotalDistance:F3}m\n" +
                  $"[SimulationDataBuffer] 相对坐标已生成（原点={LocalOrigin}）：" +
                  $"首点={RelativeTrajectoryPoints[0]}  " +
                  $"末点={RelativeTrajectoryPoints[RelativeTrajectoryPoints.Count - 1]}");
    }

    /// <summary>
    /// 清空轨迹数据及参数快照。
    /// 调用时机：用户触发"重置实验"后，由交互逻辑或系统集成模块调用。
    ///
    /// 对应框架重置事件链：
    ///   UserActionManager → ResetExperiment → ResetFlow → SimulationDataBuffer.ClearData()
    /// </summary>
    public static void ClearData()
    {
        CurrentTrajectoryPoints = null;
        RelativeTrajectoryPoints = null;
        LocalOrigin = Vector3.zero;
        LastLaunchParams = default;
        XDistance = 0f;
        YDistance = 0f;
        TotalDistance = 0f;
        Debug.Log("[SimulationDataBuffer] 三维轨迹数据已清空（实验重置）。");
    }

    // ── 便捷查询方法 ─────────────────────────────────────────────────

    /// <summary>
    /// 判断当前是否存在有效轨迹数据。
    /// UI模块、AI模块、ProjectileBallController 在读取前建议先调用此方法。
    /// </summary>
    public static bool HasValidData() =>
        CurrentTrajectoryPoints != null && CurrentTrajectoryPoints.Count > 0;

    /// <summary>
    /// 获取轨迹总点数（数据为空时返回 0）。
    /// </summary>
    public static int TrajectoryPointCount =>
        CurrentTrajectoryPoints?.Count ?? 0;

    // ── 私有工具方法 ─────────────────────────────────────────────────

    /// <summary>
    /// 将 Vector3 的每个分量四舍五入到指定小数位数。
    /// </summary>
    /// <param name="v">输入向量</param>
    /// <param name="decimals">保留小数位数（1 = 保留一位小数）</param>
    // 性能优化：查表法替代Mathf.Pow
    private static readonly float[] Pow10 = {1f, 10f, 100f, 1000f, 10000f, 100000f};
    private static Vector3 RoundVector3(Vector3 v, int decimals)
    {
        float factor = (decimals >= 0 && decimals < Pow10.Length) ? Pow10[decimals] : Mathf.Pow(10f, decimals);
        return new Vector3(
            Mathf.Round(v.x * factor) / factor,
            Mathf.Round(v.y * factor) / factor,
            Mathf.Round(v.z * factor) / factor);
    }

    /// <summary>
    /// 导出当前轨迹点为CSV文件（世界坐标+相对坐标）
    /// </summary>
    public static void ExportTrajectoryToCSV(string path)
    {
        if (CurrentTrajectoryPoints == null || CurrentTrajectoryPoints.Count == 0)
        {
            Debug.LogWarning("[SimulationDataBuffer] 无有效轨迹数据，无法导出CSV。");
            return;
        }
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Index,WorldX,WorldY,WorldZ,RelativeX,RelativeY,RelativeZ");
        for (int i = 0; i < CurrentTrajectoryPoints.Count; i++)
        {
            Vector3 w = CurrentTrajectoryPoints[i];
            Vector3 r = (RelativeTrajectoryPoints != null && i < RelativeTrajectoryPoints.Count) ? RelativeTrajectoryPoints[i] : Vector3.zero;
            sb.AppendFormat("{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}\n", i, w.x, w.y, w.z, r.x, r.y, r.z);
        }
        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SimulationDataBuffer] 轨迹数据已导出到CSV: {path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SimulationDataBuffer] 导出CSV失败: {ex.Message}");
        }
    }
}

// ── 发射参数快照结构体 ────────────────────────────────────────────────
/// <summary>
/// 本次发射参数的只读快照，随轨迹数据一同存入 SimulationDataBuffer。
/// 供 AI分析模块 等读取实验参数时使用，无需额外传参。
/// </summary>
[System.Serializable]
public struct LaunchParamSnapshot
{
    public float InitialVelocity; // 初始速度（m/s）
    public float LaunchAngle;     // 仰射角度（°）
    public Vector3 LaunchDirection; // 水平发射方向（世界空间）
    public Vector3 StartPosition;   // 发射起点（世界坐标）
    public float TimeStep;        // 时间步长（s）
    public float TotalTime;       // 最大计算时长（s）

    public LaunchParamSnapshot(
        float velocity,
        float angle,
        Vector3 direction,
        Vector3 startPos,
        float timeStep,
        float totalTime)
    {
        InitialVelocity = velocity;
        LaunchAngle = angle;
        LaunchDirection = direction;
        StartPosition = startPos;
        TimeStep = timeStep;
        TotalTime = totalTime;
    }
}