using System.Collections;
using UnityEngine;

public class UI_Manager : MonoBehaviour
{
    // 引用
    public CylinderController cylinderController;
    public DataCollector dataCollector;
    public GraphRenderer graphRenderer;
    public UIPanel uiPanel;
    
    private void Start()
    {
        // 初始化事件监听
        IdealGasSimulation.Instance.OnStateChanged += OnStateChanged;
        cylinderController.OnVolumeChanged += OnVolumeChanged;
        cylinderController.OnVolumeRangeExceeded += OnVolumeRangeExceeded;
        cylinderController.OnInteractionStateChanged += dataCollector.SetUserInteracting;
        dataCollector.OnDataCollected += OnDataCollected;
        dataCollector.OnAnalysisCompleted += OnAnalysisCompleted;

    }


    private void OnStateChanged(float pressure, float volume, float temperature)
    {
        // 状态变化时更新UI
        uiPanel.UpdateStatusDisplay(pressure, volume, temperature);
    }
    
    private void OnVolumeChanged(float newVolume)
    {
        // 体积变化时更新气体状态
        IdealGasSimulation.Instance.SetVolume(newVolume);
        // 通知数据收集器
        dataCollector.OnVolumeChanged(newVolume);
    }
    
    private void OnVolumeRangeExceeded(bool isExceeded)
    {
        // 体积超出范围时显示错误
        if (isExceeded)
        {
            StartCoroutine(ShowErrorTemporarily("无法移动，体积将超出允许范围 (0.2L-2.0L)"));
        }

    }
    
    IEnumerator  ShowErrorTemporarily(string message, float duration = 2f)
    {
        uiPanel.ShowError(message);
        yield return new WaitForSeconds(duration);
        uiPanel.HideError();
    }

    private void OnDataCollected()
    {
        // 数据采集完成时更新UI和图表
        uiPanel.UpdateDataDisplay();
        graphRenderer.UpdateGraphs();
    }
    
    private void OnAnalysisCompleted()
    {
        // 数据分析完成时更新UI
        uiPanel.UpdateAnalysisDisplay();
        graphRenderer.UpdateGraphs();
    }
    
    // 重置所有UI和数据
    public void ResetUI()
    {
        graphRenderer.ResetGraphs();
        uiPanel.ResetExperiment();
    }
    
    // 开始实验
    public void StartExperiment()
    {
        uiPanel.StartExperiment();
        dataCollector.ResetData();
        graphRenderer.ResetGraphs();
    }
    
    // 切换实验过程
    public void SetProcess(IdealGasSimulation.ProcessType process)
    {
        uiPanel.SetProcess(process);
        ResetUI();
    }

    

    
    // 设置实验步骤
    public void SetStep(int step)
    {
        uiPanel.SetStep(step);
    }
}
