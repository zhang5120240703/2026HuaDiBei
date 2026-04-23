using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LCOscillation : MonoBehaviour
{
    [Header("物理参数")]
    public float inductance = 50f;
    public float capacitance = 100f;
    private float omega;
    private float q0;
    private float time;

    [Header("实验状态")]
    public bool circuitBuilt = false;
    private bool switchOn = false;

    [Header("波形")]
    public LineRenderer voltageCurve;
    public LineRenderer currentCurve;
    public int curvePointCount = 200;

    [Header("特效")]
    public Light magneticLight;
    public ParticleSystem magneticParticle;
    private ParticleSystem.EmissionModule particleEmission;

    [Header("开关按钮")]
    public Button circuitSwitch;
    public Button resetBtn;

    [Header("滑块")]
    public Slider LSlider;
    public Slider CSlider;

    [Header("UI文本")]
    public TextMeshProUGUI tipText;
    public TextMeshProUGUI paramText;
    public TextMeshProUGUI energyText;

    [Header("公式输入UI")]
    public TMP_InputField formulaInput;
    public Button submitFormulaBtn;
    public TextMeshProUGUI formulaResultText;
    public string correctFormula = "f=1/(2π√LC)";

    void Start()
    {
        time = 0;
        UpdatePhysicsParams();

        // 一开始隐藏公式UI
        if (formulaInput != null)
            formulaInput.gameObject.SetActive(false);
        if (submitFormulaBtn != null)
            submitFormulaBtn.gameObject.SetActive(false);
        if (formulaResultText != null)
            formulaResultText.gameObject.SetActive(false);

        // 绑定事件
        if (circuitSwitch != null)
            circuitSwitch.onClick.AddListener(ToggleSwitch);
        if (resetBtn != null)
            resetBtn.onClick.AddListener(ResetExperiment);
        if (submitFormulaBtn != null)
            submitFormulaBtn.onClick.AddListener(CheckFormula);

        if (LSlider != null)
            LSlider.onValueChanged.AddListener(OnLChanged);
        if (CSlider != null)
            CSlider.onValueChanged.AddListener(OnCChanged);

        if (magneticParticle != null)
        {
            particleEmission = magneticParticle.emission;
            particleEmission.rateOverTime = 0;
        }
    }

    void Update()
    {
        if (switchOn)
        {
            time += Time.deltaTime;
            UpdateOscillation();
            UpdateWaveforms();
            UpdateEffects();
        }
    }

    void UpdatePhysicsParams()
    {
        float L = inductance * 0.001f;
        float C = capacitance * 0.000001f;
        omega = 1f / Mathf.Sqrt(L * C);
        q0 = 1f;

        if (paramText != null)
            paramText.text = "L：" + inductance + "mH\nC：" + capacitance + "μF";
    }

    void UpdateOscillation()
    {
        float current = -q0 * omega * Mathf.Sin(omega * time);
        float energy = 0.5f * inductance * 0.001f * current * current;
        if (energyText != null)
            energyText.text = "能量：" + energy.ToString("F4") + " J";
    }

    void UpdateWaveforms()
    {
        float period = 2 * Mathf.PI / omega;
        for (int i = 0; i < curvePointCount; i++)
        {
            float t = (float)i / curvePointCount * period;
            float xPos = -3f + t * 2f;

            float v = Mathf.Cos(omega * t * 0.8f);
            float c = Mathf.Sin(omega * t * 0.8f);

            if (voltageCurve != null)
                voltageCurve.SetPosition(i, new Vector3(xPos, v, 0));
            if (currentCurve != null)
                currentCurve.SetPosition(i, new Vector3(xPos, c - 1.5f, 0));

        }
    }

    void UpdateEffects()
    {
        float current = -q0 * omega * Mathf.Sin(omega * time);
        float absCur = Mathf.Abs(current);

        if (magneticLight != null)
            magneticLight.intensity = absCur * 2;

        if (magneticParticle != null)
            particleEmission.rateOverTime = absCur * 50;
    }

    // 电路连接完成后调用
    public void OnCircuitComplete()
    {
        circuitBuilt = true;
        if (tipText != null)
            tipText.text = "✅ 电路连接完成！请输入公式";

        // 强制激活输入框
        if (formulaInput != null)
        {
            formulaInput.gameObject.SetActive(true);
            formulaInput.interactable = true;
            formulaInput.ActivateInputField(); // 自动聚焦
        }
        if (submitFormulaBtn != null)
            submitFormulaBtn.gameObject.SetActive(true);
        if (formulaResultText != null)
            formulaResultText.gameObject.SetActive(true);
    }

    void ToggleSwitch()
    {
        if (!circuitBuilt) return;
        switchOn = !switchOn;
        time = 0;
    }

    void OnLChanged(float v)
    {
        inductance = v;
        UpdatePhysicsParams();
    }

    void OnCChanged(float v)
    {
        capacitance = v;
        UpdatePhysicsParams();
    }

    void ResetExperiment()
    {
        switchOn = false;
        circuitBuilt = false;
        time = 0;

        if (formulaInput != null)
        {
            formulaInput.gameObject.SetActive(false);
            formulaInput.text = "";
        }
        if (submitFormulaBtn != null)
            submitFormulaBtn.gameObject.SetActive(false);
        if (formulaResultText != null)
        {
            formulaResultText.gameObject.SetActive(false);
            formulaResultText.text = "";
        }

        if (tipText != null)
            tipText.text = "请重新连接电路";
    }

    // 验证公式
    void CheckFormula()
    {
        if (formulaInput == null || formulaResultText == null) return;

        string input = formulaInput.text.Trim().Replace(" ", "");
        string correct = correctFormula.Replace(" ", "");

        if (input == correct)
        {
            formulaResultText.text = "✅ 公式正确！";
            formulaResultText.color = Color.green;
        }
        else
        {
            formulaResultText.text = "❌ 错误，正确：f=1/(2π√LC)";
            formulaResultText.color = Color.red;
        }
    }
}