using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LCOscillation : MonoBehaviour
{
    // 物理参数：电感、电容、振荡速度
    [Header("物理参数")]
    public float inductance = 50f;
    public float capacitance = 100f;
    private float omega;
    private float runTime;

    // 波形显示：速度、粗细、大小
    [Header("波形控制")]
    public float waveSpeed = 0.000001f;
    public int waveCycleCount = 1;
    public float lineWidth = 4f;
    public float waveMaxHeight = 80f;

    // 变阻器、电压、电流
    [Header("滑动变阻器")]
    public Slider resistorSlider;
    public float rValue = 0.5f;
    public float maxVoltage = 10f;
    public float maxCurrent = 5f;

    // 当前电压、电流
    [Header("实时数据")]
    public float curVoltage;
    public float curCurrent;

    // 实验状态
    [Header("实验状态")]
    public bool circuitOK = false;
    private bool powerOn = false;
    private bool experimentFinished = false; // 实验是否完全结束

    // 灯光
    [Header("灯光")]
    public Light magneticLight;

    // 按钮
    [Header("按钮")]
    public Button circuitSwitch;
    public Button resetBtn;

    // 显示文本
    [Header("UI文本")]
    public TextMeshProUGUI tipText;
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI currentText;
    public TextMeshProUGUI paramText;

    // 公式验证
    [Header("公式验证")]
    public TMP_InputField formulaInput;
    public Button submitFormulaBtn;
    public TextMeshProUGUI formulaResultText;
    private readonly string rightFormula = "f=1/(2π√LC)";

    private Texture2D bgTexture; // 波形背景


    void Start()
    {
        // 初始化
        runTime = 0;
        CalcOmega();

        // 创建半透明背景
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        bgTexture.Apply();

        // 关闭灯光
        if (magneticLight != null) magneticLight.intensity = 0;

        // 初始化滑块
        if (resistorSlider != null)
        {
            resistorSlider.interactable = false;
            resistorSlider.value = rValue;
            resistorSlider.onValueChanged.AddListener(ResistorChange);
        }

        // 绑定按钮事件
        if (circuitSwitch != null) circuitSwitch.onClick.AddListener(SwitchClick);
        if (resetBtn != null) resetBtn.onClick.AddListener(ResetAll);
        if (submitFormulaBtn != null) submitFormulaBtn.onClick.AddListener(CheckFormula);

        UpdateUI_Data();
        tipText.text = "请先完成电路连接";
    }

    void Update()
    {
        // 实验运行时，更新时间
        if (circuitOK && powerOn)
        {
            runTime += Time.deltaTime * waveSpeed;
            UpdateEffect();
        }
    }

    // 绘制波形（电压、电流）
    void OnGUI()
    {
        if (!circuitOK || !powerOn) return;

        float screenW = Screen.width;
        float waveAreaW = screenW * 0.8f;
        float waveAreaH = waveMaxHeight * 3f;
        float waveAreaX = (screenW - waveAreaW) / 2;
        float waveAreaY = Screen.height * 0.15f;

        // 画背景
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(waveAreaX, waveAreaY, waveAreaW, waveAreaH), bgTexture);

        float voltageCenterY = waveAreaY + waveAreaH * 0.25f;
        float currentCenterY = waveAreaY + waveAreaH * 0.75f;
        float waveHalfHeight = waveMaxHeight * 0.4f;

        // 画电压波形
        GUI.color = Color.cyan;
        GUI.Label(new Rect(waveAreaX - 60, voltageCenterY - 12, 60, 25), "电压");
        DrawLine(new Vector2(waveAreaX, voltageCenterY), new Vector2(waveAreaX + waveAreaW, voltageCenterY), 1f);
        for (int i = 0; i < waveAreaW; i += 2)
        {
            float t = (float)i / waveAreaW;
            float phase = t * Mathf.PI * 2 * waveCycleCount + runTime * omega;
            float y = Mathf.Cos(phase) * waveHalfHeight;
            DrawLine(
                new Vector2(waveAreaX + i, voltageCenterY + y),
                new Vector2(waveAreaX + i + 2, voltageCenterY + Mathf.Cos(phase + 0.01f) * waveHalfHeight),
                lineWidth);
        }

        // 画电流波形
        GUI.color = Color.green;
        GUI.Label(new Rect(waveAreaX - 60, currentCenterY - 12, 60, 25), "电流");
        DrawLine(new Vector2(waveAreaX, currentCenterY), new Vector2(waveAreaX + waveAreaW, currentCenterY), 1f);
        for (int i = 0; i < waveAreaW; i += 2)
        {
            float t = (float)i / waveAreaW;
            float phase = t * Mathf.PI * 2 * waveCycleCount + runTime * omega;
            float y = Mathf.Sin(phase) * waveHalfHeight;
            DrawLine(
                new Vector2(waveAreaX + i, currentCenterY + y),
                new Vector2(waveAreaX + i + 2, currentCenterY + Mathf.Sin(phase + 0.01f) * waveHalfHeight),
                lineWidth);
        }
    }

    // 画线工具
    void DrawLine(Vector2 p1, Vector2 p2, float width)
    {
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        float len = Vector2.Distance(p1, p2);
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width / 2, len, width), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, p1);
    }

    // 计算振荡频率
    void CalcOmega()
    {
        float L = inductance * 0.001f;
        float C = capacitance * 0.000001f;
        omega = 1f / Mathf.Sqrt(L * C);
    }

    // 电路连接完成
    public void OnCircuitComplete()
    {
        circuitOK = true;
        resistorSlider.interactable = true;
        tipText.text = "电路已连接，请打开电源开关";
    }

    // 开关按钮点击
    void SwitchClick()
    {
        if (!circuitOK)
        {
            tipText.text = "请先完成电路连接";
            return;
        }

        powerOn = !powerOn;
        runTime = 0;

        if (powerOn)
            tipText.text = "实验运行中，观察LC振荡波形";
        else
        {
            tipText.text = "实验已暂停";
            CloseEffect();
        }
    }

    // 调节电阻
    void ResistorChange(float val)
    {
        if (!circuitOK) return;
        rValue = val;
        UpdateUI_Data();
    }

    // 更新电压、电流显示
    void UpdateUI_Data()
    {
        curVoltage = maxVoltage * rValue;
        curCurrent = maxCurrent * rValue;
        voltageText.text = $"电压：{curVoltage:F2} V";
        currentText.text = $"电流：{curCurrent:F2} A";
        paramText.text = $"电感L：{inductance} mH\n电容C：{capacitance} μF";
    }

    // 更新灯光亮度
    void UpdateEffect()
    {
        if (magneticLight != null)
            magneticLight.intensity = curCurrent * 1.2f;
    }

    // 关闭灯光
    void CloseEffect()
    {
        if (magneticLight != null) magneticLight.intensity = 0;
    }

    // 重置实验
    void ResetAll()
    {
        powerOn = false;
        circuitOK = false;
        experimentFinished = false;
        runTime = 0;
        rValue = 0.5f;

        resistorSlider.value = 0.5f;
        resistorSlider.interactable = false;

        CloseEffect();
        UpdateUI_Data();
        tipText.text = "实验已重置，请重新连接电路";

        formulaResultText.text = "";
        formulaInput.text = "";
    }

    // 检查公式是否正确
    void CheckFormula()
    {
        string input = formulaInput.text.Trim().Replace(" ", "");
        string right = rightFormula.Replace(" ", "");

        if (input == right)
        {
            formulaResultText.text = "✅ 答案正确";
            formulaResultText.color = Color.green;

            // ==============================
            // 实验完全结束 → 调用AI
            // ==============================
            experimentFinished = true;
            CallAIAfterExperimentComplete();
        }
        else
        {
            formulaResultText.text = "❌ 答案错误，正确公式：f=1/(2π√LC)";
            formulaResultText.color = Color.red;
        }
    }

    // ======================================
    // 【AI 接口】
    // 只有实验全部完成才会运行这里
    // 以后直接在这里写AI代码
    // ======================================
    void CallAIAfterExperimentComplete()
    {
        experimentFinished = true;
        tipText.text = "🎉 实验全部完成！正在调用AI分析...";
        Debug.Log("实验已结束，准备调用AI"); // 这里用到它了
    }
}