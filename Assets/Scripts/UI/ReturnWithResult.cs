using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂载在 Step5 的"完成并返回"按钮上
/// </summary>
public class ReturnWithResult : MonoBehaviour
{
    public void OnClick()
    {
        if (ExperimentResultBridge.Instance != null)
        {
            ExperimentResultBridge.Instance.returnTime = Time.time;

            if (SimulationDataBuffer.HasValidData())
            {
                ExperimentResultBridge.Instance.xDistance = SimulationDataBuffer.XDistance;
                ExperimentResultBridge.Instance.yDistance = SimulationDataBuffer.YDistance;
                ExperimentResultBridge.Instance.totalDistance = SimulationDataBuffer.TotalDistance;
                ExperimentResultBridge.Instance.trajectoryPointCount = SimulationDataBuffer.TrajectoryPointCount;
            }
        }

        SceneManager.LoadScene("MainMenu");
        ExperimentResultBridge.Instance?.Clear();
}
    }
    // HandleReset() 方法末尾加：
   