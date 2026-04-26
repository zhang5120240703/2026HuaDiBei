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

    [Header("波形控制")]
    [Tooltip("波形动画速度：越小越慢")]
    public float waveSpeed = 0.0001f;  // 大幅减慢速度

    [Header("滑动变阻器")]
    public Slider resistorSlider;    // 变阻器滑块
    public float rValue = 0.5f;       // 电阻值
    public float maxVoltage = 10f;    // 最大电压
    public float maxCurrent = 5f;     // 最大电流

    [Header("实时电学数据")]
    public float curVoltage;         // 当前电压
    public float curCurrent;         // 当前电流

    [Header("实验状态")]
    public bool circuitOK = false;    // 电路是否搭建完成
    private bool powerOn = false;     // 开关是否打开

    [Header("灯光特效")]
    public Light magneticLight;       // 磁场灯

    [Header("按钮")]
    public Button circuitSwitch;      // 电源开关
    public Button resetBtn;           // 重置按钮

    [Header("UI文本显示")]
    public TextMeshProUGUI tipText;       // 提示文字
    public TextMeshProUGUI voltageText;   // 电压显示
    public TextMeshProUGUI currentText;   // 电流显示
    public TextMeshProUGUI paramText;     // 参数显示

    [Header("公式验证模块")]
    public TMP_InputField formulaInput;   // 公式输入框
    public Button submitFormulaBtn;       // 提交按钮
    public TextMeshProUGUI formulaResultText; // 结果显示
    private readonly string rightFormula = "f=1/(2π√LC)"; // 正确公式

   
    void Start()
    {
        runTime = 0;
        CalcOmega(); // 计算振荡频率

        // 初始状态：关灯、禁用滑块
        if (magneticLight != null) magneticLight.intensity = 0;
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

        UpdateUI_Data(); // 更新UI数据
    }

    void Update()
    {
        // 只有电路完成+开关打开，才运行波形和特效
        if (circuitOK && powerOn)
        {
            runTime += Time.deltaTime;
            UpdateEffect();
        }
    }

    //=====================================
    // 绘制波形（屏幕上方 + 缩小 + 减速 + 标注）
    //=====================================
    void OnGUI()
    {
        // 未开启则不绘制
        if (!circuitOK || !powerOn) return;

        // 波形位置：屏幕上方空白区
        float centerX = Screen.width / 2;
        float centerY = Screen.height * 0.22f;

        // 波形宽度
        float width = Screen.width * 0.7f;

        // 波形幅度
        float height = 22 * rValue;

        //====================
        // 绘制电压波形（蓝色）
        //====================
        GUI.color = Color.blue;
        GUI.Label(new Rect(centerX - width / 2 - 70, centerY + height + 5, 100, 25), "电压波形");
        for (int i = 0; i < width; i += 3)
        {
            float t = (float)i / width;
            float y = Mathf.Cos(t * Mathf.PI * 4 + runTime * omega * waveSpeed) * height;
            DrawLine(
                new Vector2(centerX - width / 2 + i, centerY + y),
                new Vector2(centerX - width / 2 + i + 3, centerY + Mathf.Cos((t + 0.01f) * Mathf.PI * 4 + runTime * omega * waveSpeed) * height),
                2);
        }

        //====================
        // 绘制电流波形（绿色）
        //====================
        GUI.color = Color.green;
        GUI.Label(new Rect(centerX - width / 2 - 70, centerY - height - 20, 100, 25), "电流波形");
        for (int i = 0; i < width; i += 3)
        {
            float t = (float)i / width;
            float y = Mathf.Sin(t * Mathf.PI * 4 + runTime * omega * waveSpeed) * height;
            DrawLine(
                new Vector2(centerX - width / 2 + i, centerY - y),
                new Vector2(centerX - width / 2 + i + 3, centerY - Mathf.Sin((t + 0.01f) * Mathf.PI * 4 + runTime * omega * waveSpeed) * height),
                2);
        }
    }

    //=====================================
    // 画线工具函数
    //=====================================
    void DrawLine(Vector2 p1, Vector2 p2, float width)
    {
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        float length = Vector2.Distance(p1, p2);
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y, length, width), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, p1);
    }

    //=====================================
    // 计算LC振荡频率
    //=====================================
    void CalcOmega()
    {
        float L = inductance * 0.001f;
        float C = capacitance * 0.000001f;
        omega = 1f / Mathf.Sqrt(L * C);
    }

    //=====================================
    // 外部调用：电路搭建完成
    //=====================================
    public void OnCircuitComplete()
    {
        circuitOK = true;
        if (resistorSlider != null) resistorSlider.interactable = true;
        if (tipText != null) tipText.text = "电路已连接，请打开开关";
    }

    //=====================================
    // 开关点击事件
    //=====================================
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
            tipText.text = "实验运行中";
        else
        {
            tipText.text = "实验已暂停";
            CloseEffect();
        }
    }

    //=====================================
    // 变阻器数值变化
    //=====================================
    void ResistorChange(float val)
    {
        if (!circuitOK) return;
        rValue = val;
        UpdateUI_Data();
    }

    //=====================================
    // 更新电压、电流、参数显示
    //=====================================
    void UpdateUI_Data()
    {
        curVoltage = maxVoltage * rValue;
        curCurrent = maxCurrent * rValue;

        if (voltageText != null) voltageText.text = $"电压：{curVoltage:F2} V";
        if (currentText != null) currentText.text = $"电流：{curCurrent:F2} A";
        if (paramText != null) paramText.text = $"L：{inductance} mH\nC：{capacitance} μF";
    }

    //=====================================
    // 更新灯光亮度
    //=====================================
    void UpdateEffect()
    {
        if (magneticLight != null)
            magneticLight.intensity = curCurrent * 1.2f;
    }

    //=====================================
    // 关闭灯光
    //=====================================
    void CloseEffect()
    {
        if (magneticLight != null) magneticLight.intensity = 0;
    }

    //=====================================
    // 重置实验全部状态
    //=====================================
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
        tipText.text = "请重新连接电路";
    }

    //=====================================
    // 公式验证逻辑
    //=====================================
    void CheckFormula()
    {
        if (formulaInput == null || formulaResultText == null) return;

        string input = formulaInput.text.Trim().Replace(" ", "");
        string right = rightFormula.Replace(" ", "");

        if (input == right)
        {
            formulaResultText.text = "✅ 回答正确！";
            formulaResultText.color = Color.green;
        }
        else
        {
            formulaResultText.text = "❌ 错误！正确公式：f=1/(2π√LC)";
            formulaResultText.color = Color.red;
        }
    }
}