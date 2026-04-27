using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnWithResult : MonoBehaviour
{
    public void OnClick()
    {
        Debug.Log($"[ReturnWithResult] bridge={ExperimentResultBridge.Instance != null}, Instance={ExperimentResultBridge.Instance}");
        Debug.Log($"[ReturnWithResult] OnClick 被调用！Time.time={Time.time}");

        if (ExperimentResultBridge.Instance != null)
        {
            // ★ 先写 returnTime，确保在 LoadScene 之前
            ExperimentResultBridge.Instance.returnTime = Time.time;
            Debug.Log($"[ReturnWithResult] returnTime 已写入={ExperimentResultBridge.Instance.returnTime}");

            if (SimulationDataBuffer.HasValidData())
            {
                ExperimentResultBridge.Instance.xDistance = SimulationDataBuffer.XDistance;
                ExperimentResultBridge.Instance.yDistance = SimulationDataBuffer.YDistance;
                ExperimentResultBridge.Instance.totalDistance = SimulationDataBuffer.TotalDistance;
                ExperimentResultBridge.Instance.trajectoryPointCount = SimulationDataBuffer.TrajectoryPointCount;

                var snap = SimulationDataBuffer.LastLaunchParams;
                ExperimentResultBridge.Instance.velocity = snap.InitialVelocity;
                ExperimentResultBridge.Instance.launchAngle = snap.LaunchAngle;

                Debug.Log($"[ReturnWithResult] 轨迹数据已写入: xDist={SimulationDataBuffer.XDistance}, yDist={SimulationDataBuffer.YDistance}, total={SimulationDataBuffer.TotalDistance}, points={SimulationDataBuffer.TrajectoryPointCount}");
            }

            var bridge = ExperimentResultBridge.Instance;
            var snap2 = SimulationDataBuffer.LastLaunchParams;
            float elapsed = bridge.ElapsedTime;
            string duration = ExperimentResultBridge.FormatDuration(elapsed);

            Debug.Log($"[ReturnWithResult] 用时={elapsed}s, 格式化={duration}");

            if (ExperimentDataManager.Instance != null && SimulationDataBuffer.HasValidData())
            {
                ExperimentDataManager.Instance.AddRecord(
                    experimentName: bridge.experimentName,
                    xDistance: SimulationDataBuffer.XDistance,
                    yDistance: SimulationDataBuffer.YDistance,
                    totalDistance: SimulationDataBuffer.TotalDistance,
                    pointCount: SimulationDataBuffer.TrajectoryPointCount,
                    velocity: snap2.InitialVelocity,
                    angle: snap2.LaunchAngle,
                    duration: duration
                );
                Debug.Log($"[ReturnWithResult] 历史记录已添加: {bridge.experimentName}");
            }
        }

        // ★ 最后才加载场景
        Debug.Log($"[ReturnWithResult] 即将 LoadScene，当前 returnTime={ExperimentResultBridge.Instance?.returnTime}");
        SceneManager.LoadScene("MainMenu");
    }
}