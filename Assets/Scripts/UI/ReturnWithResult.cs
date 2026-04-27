using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂载在 Step5 的"完成并返回"按钮上
/// 职责：写入所有实验数据到桥接器+历史记录，然后回主菜单
/// </summary>
public class ReturnWithResult : MonoBehaviour
{
    public void OnClick()
    {
        Debug.Log($"[ReturnWithResult] OnClick 被调用！Time.time={Time.time}");

        // ── 1. 写入桥接器 ────────────────────────────
        if (ExperimentResultBridge.Instance != null)
        {
            // 记录结束时间
            ExperimentResultBridge.Instance.returnTime = Time.time;
            Debug.Log($"[ReturnWithResult] returnTime 已写入={ExperimentResultBridge.Instance.returnTime}");

            if (SimulationDataBuffer.HasValidData())
            {
                // 轨迹数据
                ExperimentResultBridge.Instance.xDistance = SimulationDataBuffer.XDistance;
                ExperimentResultBridge.Instance.yDistance = SimulationDataBuffer.YDistance;
                ExperimentResultBridge.Instance.totalDistance = SimulationDataBuffer.TotalDistance;
                ExperimentResultBridge.Instance.trajectoryPointCount = SimulationDataBuffer.TrajectoryPointCount;

                // 参数快照
                var snap = SimulationDataBuffer.LastLaunchParams;
                ExperimentResultBridge.Instance.velocity = snap.InitialVelocity;
                ExperimentResultBridge.Instance.launchAngle = snap.LaunchAngle;

                Debug.Log($"[ReturnWithResult] 轨迹数据已写入: xDist={SimulationDataBuffer.XDistance}, yDist={SimulationDataBuffer.YDistance}, total={SimulationDataBuffer.TotalDistance}, points={SimulationDataBuffer.TrajectoryPointCount}");
            }
        }

        // ── 2. 写入历史记录管理器 ────────────────────
        if (ExperimentResultBridge.Instance != null && SimulationDataBuffer.HasValidData())
        {
            var bridge = ExperimentResultBridge.Instance;
            var snap = SimulationDataBuffer.LastLaunchParams;
            float elapsed = bridge.ElapsedTime;
            string duration = ExperimentResultBridge.FormatDuration(elapsed);

            Debug.Log($"[ReturnWithResult] 用时={elapsed}s, 格式化={duration}");

            if (ExperimentDataManager.Instance != null)
            {
                ExperimentDataManager.Instance.AddRecord(
                    experimentName: bridge.experimentName,
                    xDistance: SimulationDataBuffer.XDistance,
                    yDistance: SimulationDataBuffer.YDistance,
                    totalDistance: SimulationDataBuffer.TotalDistance,
                    pointCount: SimulationDataBuffer.TrajectoryPointCount,
                    velocity: snap.InitialVelocity,
                    angle: snap.LaunchAngle,
                    duration: duration
                );
                Debug.Log($"[ReturnWithResult] 历史记录已添加: {bridge.experimentName}");
            }
        }

        // ── 3. 加载回主菜单 ──────────────────────────
        SceneManager.LoadScene("MainMenu");
    }
}