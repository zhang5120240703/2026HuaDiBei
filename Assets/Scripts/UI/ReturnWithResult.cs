using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnWithResult : MonoBehaviour
{
    public void OnClick()
    {
        // ── 1. 从临时存储写入历史记录管理器 ──
        if (SessionDataStore.HasData && ExperimentDataManager.Instance != null && ExperimentResultBridge.Instance != null)
        {
            string expName = ExperimentResultBridge.Instance.experimentName;

            // 遍历本次流程所有数据，逐条存入历史
            foreach (var record in SessionDataStore.Records)
            {
                ExperimentDataManager.Instance.AddRecord(
                    experimentName: expName,
                    xDistance: record.xDistance,
                    yDistance: record.yDistance,
                    totalDistance: record.totalDistance,
                    pointCount: record.pointCount,
                    velocity: record.velocity,
                    angle: record.angle,
                    duration: "" // 每条具体用时在这里没法精确算，统一留空，总结面板用总用时
                );
            }

            // ── 2. 写入桥接器（给总结面板用） ──
            var last = SessionDataStore.Records[SessionDataStore.Count - 1];
            ExperimentResultBridge.Instance.returnTime = Time.time;
            ExperimentResultBridge.Instance.xDistance = last.xDistance;
            ExperimentResultBridge.Instance.yDistance = last.yDistance;
            ExperimentResultBridge.Instance.totalDistance = last.totalDistance;
            ExperimentResultBridge.Instance.trajectoryPointCount = last.pointCount;
            ExperimentResultBridge.Instance.velocity = last.velocity;
            ExperimentResultBridge.Instance.launchAngle = last.angle;
        }

        // ── 3. 清空临时存储 ──
        SessionDataStore.Clear();

        // ── 4. 返回主菜单 ──
        SceneManager.LoadScene("MainMenu");
    }
}