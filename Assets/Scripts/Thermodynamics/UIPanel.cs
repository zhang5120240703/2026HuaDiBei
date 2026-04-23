using System.Collections;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    // 引用
    public CylinderController cylinderController;
    public DataCollector dataCollector;
    
    // 参数文本
    public TMP_Text pressureText;
    public TMP_Text volumeText;
    public TMP_Text temperatureText;
    public TMP_Text pvProductText;
    //输入滑动条
    public Slider temperatureSlider;
    public Slider volumeSlider;

    //状态按钮
    public Button isothermalButton;
    public Button isobaricButton;
    public Button isochoricButton;

    public TMP_Text processText;
    public TMP_Text statusText;
    public TMP_Text progressText;
    public TMP_Text errorText;
    // 实验状态
    private int currentStep = 1;
    private const int totalSteps = 4;


    //UI面板
    public CanvasGroup statusPanel;//参数面板
    public CanvasGroup inputPanel;//输入面板
    private void Start()
    {
        // 初始化事件监听
        IdealGasSimulation.Instance.OnStateChanged += UpdateStatusDisplay;
        dataCollector.OnDataCollected += UpdateDataDisplay;
        dataCollector.OnAnalysisCompleted += UpdateAnalysisDisplay;

        //滑动条输入事件监听
        temperatureSlider.onValueChanged.AddListener(OnTemperatureSliderChanged);
        volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        
        //初始隐藏参数面板
        statusPanel.alpha = 0;
        //初始隐藏输入面板
        inputPanel.alpha = 0;

        // 初始更新
        UpdateStatusDisplay(IdealGasSimulation.Instance.GetPressure(), IdealGasSimulation.Instance.GetVolume(), IdealGasSimulation.Instance.GetTemperature());
        UpdateStatusText();
        UpdateProgressText();
        UpdateProcessText(IdealGasSimulation.Instance.GetCurrentProcess());
        HideError();
        // 根据初始过程类型设置滑动条显示
        SetTemperatureSliderDisplay(IdealGasSimulation.Instance.GetCurrentProcess());
        SetVolumeSliderDisplay(IdealGasSimulation.Instance.GetCurrentProcess());
    }



    //更新状态显示
    public void UpdateStatusDisplay(float pressure, float volume, float temperature)
    {
        // 更新数据显示
        pressureText.text = "压力: " + pressure.ToString("F3") + " kPa";
        volumeText.text = "体积: " + volume.ToString("F3") + " L";
        temperatureText.text = "温度: " + temperature.ToString("F3") + " K";
        pvProductText.text = "PV乘积: " + (pressure * volume).ToString("F3") + " kPa·L";

        //使用 SetValueWithoutNotify 避免触发 onValueChanged 回调
        // 这样不会在同步 UI 时意外修改模拟状态
        temperatureSlider.SetValueWithoutNotify(temperature);
        volumeSlider.SetValueWithoutNotify(volume);

        // 更新Slider的值
        temperatureSlider.value = temperature; // 同步Slider值
        volumeSlider.value = volume;
    }

    #region Slider改变时调用
    public void OnTemperatureSliderChanged(float value)
    {
        // 使用Slider值来设置温度
        IdealGasSimulation.Instance.SetTemperature(value);

    }
    public void OnPressureSliderChanged(float value)
    {
        // 使用Slider值来设置压强
        IdealGasSimulation.Instance.SetPressure(value);

    }

    public void OnVolumeSliderChanged(float value)
    {
        // 使用Slider值来设置体积
        IdealGasSimulation.Instance.SetVolume(value);

    }
    #endregion



    #region 滑动条显示控制
    //等温状态不显示TemperatureSlider
    private void SetTemperatureSliderDisplay(IdealGasSimulation.ProcessType process)
    {
        bool isActive = process == IdealGasSimulation.ProcessType.Isothermal ? false : true;

        temperatureSlider.gameObject.SetActive(isActive);//显示或隐藏温度输入滑动条
    }

    //等容状态不显示VolumeSlider
    private void SetVolumeSliderDisplay(IdealGasSimulation.ProcessType process)
    {
        bool isActive = process == IdealGasSimulation.ProcessType.Isochoric ? false : true;
        volumeSlider.gameObject.SetActive(isActive);
    }
    #endregion





    public void SetProcess(IdealGasSimulation.ProcessType process)
    {
        SetTemperatureSliderDisplay(process);
        SetVolumeSliderDisplay(process);
        UpdateProcessText(process);
    }



    #region 更新文本
    private void UpdateProcessText(IdealGasSimulation.ProcessType process)
    {
        switch (process)
        {
            case IdealGasSimulation.ProcessType.Isothermal:
                processText.text = "当前过程: 等温过程 (玻意耳定律)";
                break;
            case IdealGasSimulation.ProcessType.Isobaric:
                processText.text = "当前过程: 等压过程 (盖-吕萨克定律)";
                break;
            case IdealGasSimulation.ProcessType.Isochoric:
                processText.text = "当前过程: 等容过程 (查理定律)";
                break;
            case IdealGasSimulation.ProcessType.Null:
                processText.text = "当前过程: 未选择";
                break;
        }
    }
    
    private void UpdateStatusText()
    {
        switch (currentStep)
        {
            case 1:
                statusText.text = "操作指引: 选择实验过程";
                break;
            case 2:
                statusText.text = "操作指引: 点击开始按钮开始实验";
                break;
            case 3:
                statusText.text = "操作指引: 移动活塞或者调整滑动条，系统将自动采集数据";
                break;
            case 4:
                statusText.text = "操作指引: 实验完成，查看图像分析结果(可重置实验)";
                break;

        }
    }
    #endregion

    private void UpdateProgressText()
    {
        progressText.text = "实验进度: " + currentStep + "/" + totalSteps;
    }
    
    public void UpdateDataDisplay()
    {
        int dataCount = dataCollector.GetDataPointCount();
        int requireCount = dataCollector.GetRequiredPointsForLines();
        if (dataCount>= requireCount)
        {
            statusText.text = "数据点收集完毕，点击确认按钮开始进行图像分析绘制";
        }
        else
            statusText.text = "已采集 " + dataCount+"/"+ requireCount+ " 个数据点，继续调整体积";

    }


    public void UpdateAnalysisDisplay()
    {
        // 显示分析结果
        float averagePV = dataCollector.GetAveragePVProduct();
        float error = dataCollector.GetAverageErrorPercentage();
        
        string resultText = "分析结果: " + "\n";
        resultText += "平均PV乘积: " + averagePV.ToString("F2") + " kPa·L\n";
        resultText += "平均误差: " + error.ToString("F2") + "%\n";
        
        // 判断是否验证了定律
        bool verified = false;
        string lawName = "";
        
        switch (IdealGasSimulation.Instance.GetCurrentProcess())
        {
            case IdealGasSimulation.ProcessType.Isothermal:
                verified = dataCollector.IsBoyleLawVerified();
                lawName = "玻意耳定律";
                break;
            case IdealGasSimulation.ProcessType.Isobaric:
                verified = dataCollector.IsCharlesLawVerified();
                lawName = "盖-吕萨克定律";
                break;
            case IdealGasSimulation.ProcessType.Isochoric:
                verified = dataCollector.IsGayLussacLawVerified();
                lawName = "查理定律";
                break;
        }
        
        if (verified)
        {
            resultText += "成功验证了 " + lawName;
        }
        else
        {
            resultText += "未能验证 " + lawName + "，误差超过3%";
        }
        
        statusText.text = resultText;
        currentStep = 4;
        UpdateProgressText();
    }

    #region 错误信息的显示控制
    public void ShowError(string message)
    {
        errorText.gameObject.SetActive(true);
        errorText.text = "错误: " + message;
    }
    
    public void HideError()
    {
        errorText.gameObject.SetActive(false);
    }
    #endregion

    #region 控制使用开始和重置
    public void StartExperiment()
    {
        currentStep = 3;
        statusPanel.alpha = 1;//显示参数面板
        inputPanel.alpha = 1;
        UpdateStatusText();
        UpdateProgressText();
        dataCollector.ResetData();
    }
    
    public void ResetExperiment()
    {
        currentStep = 1;
        statusPanel.alpha = 0;//隐藏参数面板
        dataCollector.ResetData();
        UpdateStatusText();
        UpdateProgressText();
        UpdateProcessText(IdealGasSimulation.Instance.GetCurrentProcess());
        HideError();
    }
    #endregion


    
    public void SetStep(int step)
    {
        currentStep = step;
        UpdateStatusText();
        UpdateProgressText();
    }
    
    public int GetCurrentStep()
    {
        return currentStep;
    }
}