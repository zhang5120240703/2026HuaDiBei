using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LCOscillation : MonoBehaviour
{
    [Header("物理参数")]
    public float inductance = 50f;   // 电感
    public float capacitance = 100f; // 电容
    private float omega;             // 振荡角频率
    private float runTime;           // 运行计时

    [Header("【波形清晰度&速度控制】")]
    [Tooltip("波形移动速度：数值越小越慢，推荐0.005-0.02")]
    public float waveSpeed = 0.0001f;
    [Tooltip("屏幕内显示的波形周期数：1=1个完整波，最清晰")]
    public int waveCycleCount = 1;
    [Tooltip("波形线条粗细")]
    public float lineWidth = 4f;
    [Tooltip("波形固定最大高度")]
    public float waveMaxHeight = 80f;

    [Header("滑动变阻器")]
    public Slider resistorSlider;
    public float rValue = 0.5f;
    public float maxVoltage = 10f;
    public float maxCurrent = 5f;

    [Header("实时电学数据")]
    public float curVoltage;
    public float curCurrent;

    [Header("实验状态")]
    public bool circuitOK = false;
    private bool powerOn = false;

    [Header("灯光特效")]
    public Light magneticLight;

    [Header("按钮组件")]
    public Button circuitSwitch;
    public Button resetBtn;

    [Header("UI文本显示")]
    public TextMeshProUGUI tipText;
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI currentText;
    public TextMeshProUGUI paramText;

    [Header("公式验证模块")]
    public TMP_InputField formulaInput;
    public Button submitFormulaBtn;
    public TextMeshProUGUI formulaResultText;
    private readonly string rightFormula = "f=1/(2π√LC)";

    // 波形背景纹理
    private Texture2D bgTexture;


    void Start()
    {
        runTime = 0;
        // 修复错误：调用CalcOmega方法
        CalcOmega();

        // 初始化波形背景
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        bgTexture.Apply();

        // 初始化灯光
        if (magneticLight != null) magneticLight.intensity = 0;

        // 初始化滑动变阻器
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
        // 初始中文提示
        if (tipText != null) tipText.text = "请先完成电路连接";
    }

    // 修复错误：Update方法完整闭合，大括号匹配
    void Update()
    {
        if (circuitOK && powerOn)
        {
            // 超慢速度控制，直接在这里生效
            runTime += Time.deltaTime * waveSpeed;
            UpdateEffect();
        }
    }

    // 修复错误：OnGUI放在类里，不是Update内部，Unity能正常调用
    void OnGUI()
    {
        if (!circuitOK || !powerOn) return;

        // 波形区域固定参数
        float screenW = Screen.width;
        float waveAreaW = screenW * 0.8f;
        float waveAreaH = waveMaxHeight * 3f;
        float waveAreaX = (screenW - waveAreaW) / 2;
        float waveAreaY = Screen.height * 0.15f;

        // 1. 画半透明背景，让波形和场景分开，一眼看清
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(waveAreaX, waveAreaY, waveAreaW, waveAreaH), bgTexture);

        // 2. 电压和电流波形上下分开，不重叠
        float voltageCenterY = waveAreaY + waveAreaH * 0.25f;
        float currentCenterY = waveAreaY + waveAreaH * 0.75f;
        float waveHalfHeight = waveMaxHeight * 0.4f;

        // -------------------------- 电压波形（亮青色，上半区）--------------------------
        GUI.color = Color.cyan;
        GUI.Label(new Rect(waveAreaX - 60, voltageCenterY - 12, 60, 25), "电压");
        // 画中间基准线
        DrawLine(new Vector2(waveAreaX, voltageCenterY), new Vector2(waveAreaX + waveAreaW, voltageCenterY), 1f);

        // 绘制平滑波形
        for (int i = 0; i < waveAreaW; i += 2)
        {
            float t = (float)i / waveAreaW;
            float wavePhase = t * Mathf.PI * 2 * waveCycleCount + runTime * omega;
            float y = Mathf.Cos(wavePhase) * waveHalfHeight;

            DrawLine(
                new Vector2(waveAreaX + i, voltageCenterY + y),
                new Vector2(waveAreaX + i + 2, voltageCenterY + Mathf.Cos(wavePhase + Mathf.PI * 2 * waveCycleCount * 0.0025f) * waveHalfHeight),
                lineWidth);
        }

        // -------------------------- 电流波形（绿色，下半区）--------------------------
        GUI.color = Color.green;
        GUI.Label(new Rect(waveAreaX - 60, currentCenterY - 12, 60, 25), "电流");
        // 画中间基准线
        DrawLine(new Vector2(waveAreaX, currentCenterY), new Vector2(waveAreaX + waveAreaW, currentCenterY), 1f);

        // 绘制平滑波形
        for (int i = 0; i < waveAreaW; i += 2)
        {
            float t = (float)i / waveAreaW;
            float wavePhase = t * Mathf.PI * 2 * waveCycleCount + runTime * omega;
            float y = Mathf.Sin(wavePhase) * waveHalfHeight;

            DrawLine(
                new Vector2(waveAreaX + i, currentCenterY + y),
                new Vector2(waveAreaX + i + 2, currentCenterY + Mathf.Sin(wavePhase + Mathf.PI * 2 * waveCycleCount * 0.0025f) * waveHalfHeight),
                lineWidth);
        }
    }

    // 画线工具
    void DrawLine(Vector2 p1, Vector2 p2, float width)
    {
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(p1, p2);
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width / 2, length, width), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, p1);
    }

    // 修复错误：补充CalcOmega方法，解决找不到名称的报错
    void CalcOmega()
    {
        float L = inductance * 0.001f;
        float C = capacitance * 0.000001f;
        omega = 1f / Mathf.Sqrt(L * C);
    }

    // 电路连接完成回调
    public void OnCircuitComplete()
    {
        circuitOK = true;
        if (resistorSlider != null) resistorSlider.interactable = true;
        if (tipText != null) tipText.text = "电路已连接，请打开电源开关";
    }

    // 电源开关点击
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

    // 滑动电阻调节
    void ResistorChange(float val)
    {
        if (!circuitOK) return;
        rValue = val;
        UpdateUI_Data();
    }

    // 更新UI数据
    void UpdateUI_Data()
    {
        curVoltage = maxVoltage * rValue;
        curCurrent = maxCurrent * rValue;

        if (voltageText != null) voltageText.text = $"电压：{curVoltage:F2} V";
        if (currentText != null) currentText.text = $"电流：{curCurrent:F2} A";
        if (paramText != null) paramText.text = $"电感L：{inductance} mH\n电容C：{capacitance} μF";
    }

    // 更新灯光效果
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
        runTime = 0;
        rValue = 0.5f;

        if (resistorSlider != null)
        {
            resistorSlider.value = 0.5f;
            resistorSlider.interactable = false;
        }

        CloseEffect();
        UpdateUI_Data();
        if (tipText != null) tipText.text = "实验已重置，请重新连接电路";
    }

    // 公式验证
    void CheckFormula()
    {
        if (formulaInput == null || formulaResultText == null) return;

        string input = formulaInput.text.Trim().Replace(" ", "");
        string right = rightFormula.Replace(" ", "");

        if (input == right)
        {
            formulaResultText.text = "✅ 答案正确";
            formulaResultText.color = Color.green;
        }
        else
        {
            formulaResultText.text = "❌ 答案错误，正确公式：f=1/(2π√LC)";
            formulaResultText.color = Color.red;
        }
    }
}