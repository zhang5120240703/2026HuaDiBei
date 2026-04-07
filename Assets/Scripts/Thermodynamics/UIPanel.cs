using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPanel : MonoBehaviour
{
    // 引用
    public IdealGasSimulation gasSimulation;
    public DataCollector dataCollector;
    
    // UI元素
    public TMP_Text pressureText;
    public TMP_Text volumeText;
    public TMP_Text temperatureText;
    public TMP_Text pvProductText;
    public TMP_InputField temperatureInput;
    public Button isothermalButton;
    public Button isobaricButton;
    public Button isochoricButton;
    public TMP_Text processText;
    public TMP_Text statusText;
    public TMP_Text progressText;
    public TMP_Text errorText;
    public Button startButton;
    public Button resetButton;
    public Button backButton;
    
    // 实验状态
    private string currentStatus = "准备就绪";
    private int currentStep = 1;
    private const int totalSteps = 5;
    
    private void Start()
    {
        // 初始化事件监听
        gasSimulation.OnStateChanged += UpdateStatusDisplay;
        dataCollector.OnDataCollected += UpdateDataDisplay;
        dataCollector.OnAnalysisCompleted += UpdateAnalysisDisplay;

        // 初始化按钮事件
        //isothermalButton.onClick.AddListener(() => SetProcess(IdealGasSimulation.ProcessType.Isothermal));
        //isobaricButton.onClick.AddListener(() => SetProcess(IdealGasSimulation.ProcessType.Isobaric));
        //isochoricButton.onClick.AddListener(() => SetProcess(IdealGasSimulation.ProcessType.Isochoric));

        // 温度输入事件
        temperatureInput.onValueChanged.AddListener(OnTemperatureInputChanged);
        
        // 控制按钮事件
        startButton.onClick.AddListener(StartExperiment);
        resetButton.onClick.AddListener(ResetExperiment);
        backButton.onClick.AddListener(BackToMainMenu);
        
        // 初始更新
        UpdateStatusDisplay(gasSimulation.GetPressure(), gasSimulation.GetVolume(), gasSimulation.GetTemperature());
        UpdateProcessText();
        UpdateStatusText();
        UpdateProgressText();
        HideError();
    }
    
    public void UpdateStatusDisplay(float pressure, float volume, float temperature)
    {
        // 更新数据显示
        pressureText.text = "压力: " + pressure.ToString("F2") + " kPa";
        volumeText.text = "体积: " + volume.ToString("F2") + " L";
        temperatureText.text = "温度: " + temperature.ToString("F2") + " K";
        pvProductText.text = "PV乘积: " + (pressure * volume).ToString("F2") + " kPa·L";
        
        // 更新温度输入
        temperatureInput.text = temperature.ToString("F1");
    }
    
    private void OnTemperatureInputChanged(string value)
    {
        if (float.TryParse(value, out float temp))
        {
            gasSimulation.SetTemperature(temp);
        }
    }
    
    public void SetProcess(int process)
    {
        gasSimulation.SetProcess((IdealGasSimulation.ProcessType)process);
        UpdateProcessText();
        ResetExperiment();
    }
    
    private void UpdateProcessText()
    {
        switch (gasSimulation.currentProcess)
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
        }
    }
    
    private void UpdateStatusText()
    {
        switch (currentStep)
        {
            case 1:
                statusText.text = "操作指引: 选择实验过程并设置温度";
                break;
            case 2:
                statusText.text = "操作指引: 点击开始按钮开始实验";
                break;
            case 3:
                statusText.text = "操作指引: 移动活塞改变体积，采集数据";
                break;
            case 4:
                statusText.text = "操作指引: 查看数据分析结果";
                break;
            case 5:
                statusText.text = "操作指引: 实验完成，可返回主菜单";
                break;
        }
    }
    
    private void UpdateProgressText()
    {
        progressText.text = "实验进度: " + currentStep + "/" + totalSteps;
    }
    
    public void UpdateDataDisplay()
    {
        int dataCount = dataCollector.GetDataPointCount();
        statusText.text = "已采集 " + dataCount + " 个数据点，继续调整体积";
    }
    
    
    public void UpdateAnalysisDisplay()
    {
        // 显示分析结果
        float averagePV = dataCollector.GetAveragePVProduct();
        float error = dataCollector.GetMaxErrorPercentage();
        
        string resultText = "分析结果: " + "\n";
        resultText += "平均PV乘积: " + averagePV.ToString("F2") + " kPa·L\n";
        resultText += "最大误差: " + error.ToString("F2") + "%\n";
        
        // 判断是否验证了定律
        bool verified = false;
        string lawName = "";
        
        switch (gasSimulation.currentProcess)
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
        currentStep = 5;
        UpdateProgressText();
    }
    
    public void ShowError(string message)
    {
        errorText.text = "错误: " + message;
        errorText.gameObject.SetActive(true);
    }
    
    public void HideError()
    {
        errorText.gameObject.SetActive(false);
    }
    
    public void StartExperiment()
    {
        currentStep = 3;
        UpdateStatusText();
        UpdateProgressText();
        dataCollector.ResetData();
        HideError();
    }
    
    public void ResetExperiment()
    {
        currentStep = 1;
        dataCollector.ResetData();
        UpdateStatusText();
        UpdateProgressText();
        HideError();
    }
    
    public void BackToMainMenu()
    {
        // 这里可以实现返回主菜单的逻辑
        // 例如加载主场景
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    }
    
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