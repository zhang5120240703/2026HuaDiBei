using UnityEngine;

public class UIManager : MonoBehaviour
{
    // 引用
    public IdealGasSimulation gasSimulation;
    public CylinderController cylinderController;
    public DataCollector dataCollector;
    public GraphRenderer graphRenderer;
    public UIPanel uiPanel;
    
    private void Start()
    {
        // 初始化事件监听
        gasSimulation.OnStateChanged += OnStateChanged;
        cylinderController.OnVolumeChanged += OnVolumeChanged;
        cylinderController.OnVolumeRangeExceeded += OnVolumeRangeExceeded;
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
        gasSimulation.SetVolume(newVolume);
        // 通知数据收集器
        dataCollector.OnVolumeChanged(newVolume);
    }
    
    private void OnVolumeRangeExceeded(bool isExceeded)
    {
        // 体积超出范围时显示错误
        if (isExceeded)
        {
            uiPanel.ShowError("体积超出允许范围 (0.5L-2.0L)");
        }

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
        dataCollector.ResetData();
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

    
    // 获取当前实验步骤
    public int GetCurrentStep()
    {
        return uiPanel.GetCurrentStep();
    }
    
    // 设置实验步骤
    public void SetStep(int step)
    {
        uiPanel.SetStep(step);
    }
}