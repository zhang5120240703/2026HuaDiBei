using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SimpleExperimentUI : MonoBehaviour
{
    [Header("=== 常驻底栏 ===")]
    public Button btnPause;
    public Button btnResume;
    public Button btnReset;
    public TextMeshProUGUI txtStatus;

    [Header("=== 步骤面板 (Step1~5) ===")]
    public GameObject[] stepPanels;

    [Header("=== Step2 输入框 ===")]
    public TMP_InputField inputVelocity;
    public TMP_InputField inputAngle;
    public TMP_InputField inputDirX;
    public TMP_InputField inputDirZ;
    public TMP_InputField inputStartY;
    public TMP_InputField inputTimeStep;
    public TMP_InputField inputTotalTime;
    public TextMeshProUGUI txtError;
    public Button btnConfirmStep2;

    [Header("=== 其他步骤按钮 ===")]
    public Button btnConfirmStep1;
    public Button btnConfirmStep4;
    public Button btnFinishSteps;

    [Header("=== 数据显示 ===")]
    public TextMeshProUGUI txtSimResult;
    public TextMeshProUGUI txtXDist;
    public TextMeshProUGUI txtYDist;
    public TextMeshProUGUI txtTotalDist;
    public TextMeshProUGUI txtPointCount;

    private ProjectileExperimentController _ctrl;

    void Start()
    {
        _ctrl = FindObjectOfType<ProjectileExperimentController>();
        if (_ctrl == null)
        {
            Debug.LogError("未找到 ProjectileExperimentController！");
            return;
        }

        BindButtons();
        BindControllerEvents();
        InitUI();
    }

    void BindButtons()
    {
        // 常驻按钮
        btnPause.onClick.AddListener(() => _ctrl.RequestPause());
        btnResume.onClick.AddListener(() => _ctrl.RequestResume());
        btnReset.onClick.AddListener(() => _ctrl.RequestReset());

        // 步骤按钮
        btnConfirmStep1.onClick.AddListener(() => _ctrl.ConfirmPrepare());
        btnConfirmStep2.onClick.AddListener(() => { PushParams(); _ctrl.ConfirmParam(); });
        btnConfirmStep4.onClick.AddListener(() => _ctrl.ConfirmObserved());
        btnFinishSteps.onClick.AddListener(() => _ctrl.ConfirmFinish());
    }

    void BindControllerEvents()
    {
        _ctrl.OnStepEntered += OnStepChanged;
        _ctrl.OnParamLoaded += OnParamLoaded;
        _ctrl.OnParamError += ShowError;
        _ctrl.OnSimulationReady += OnSimulationReady;
        _ctrl.OnObserveData += OnObserveData;
        _ctrl.OnPaused += OnPaused;
        _ctrl.OnResumed += OnResumed;
        _ctrl.OnReset += OnReset;
    }

    void InitUI()
    {
        // 显示Step1面板
        ShowStepPanel(0);

        // 设置默认参数显示
        inputVelocity.text = "10";
        inputAngle.text = "45";
        inputDirX.text = "0";
        inputDirZ.text = "1";
        inputStartY.text = "1";
        inputTimeStep.text = "0.02";
        inputTotalTime.text = "5";

        // 按钮初始状态
        btnPause.interactable = true;
        btnResume.interactable = false;   // 初始未暂停，恢复按钮不可用
        txtStatus.text = "⚪ 空闲";

        HideError();
    }

    void PushParams()
    {
        float v = float.Parse(inputVelocity.text);
        float angle = float.Parse(inputAngle.text);
        float dirX = float.Parse(inputDirX.text);
        float dirZ = float.Parse(inputDirZ.text);
        float startY = float.Parse(inputStartY.text);
        float dt = float.Parse(inputTimeStep.text);
        float totalT = float.Parse(inputTotalTime.text);

        _ctrl.SetParam(
            velocity: v,
            angle: angle,
            direction: new Vector3(dirX, 0, dirZ),
            startPosition: new Vector3(0, startY, 0),
            timeStep: dt,
            totalTime: totalT);
    }

    void OnStepChanged(ExperimentStep step)
    {
        ShowStepPanel((int)step);
    }

    void ShowStepPanel(int index)
    {
        for (int i = 0; i < stepPanels.Length; i++)
        {
            if (stepPanels[i] != null)
                stepPanels[i].SetActive(i == index);
        }
    }

    void OnParamLoaded(float v, float angle, Vector3 dir, Vector3 pos, float dt, float T)
    {
        inputVelocity.text = v.ToString();
        inputAngle.text = angle.ToString();
        inputDirX.text = dir.x.ToString();
        inputDirZ.text = dir.z.ToString();
        inputStartY.text = pos.y.ToString();
        inputTimeStep.text = dt.ToString();
        inputTotalTime.text = T.ToString();
    }

    void OnSimulationReady(LaunchParamSnapshot snap, List<Vector3> points)
    {
        txtSimResult.text = $"✅ 仿真完成\n" +
                           $"轨迹点数：{points.Count}\n" +
                           $"初速度：{snap.InitialVelocity} m/s\n" +
                           $"仰角：{snap.LaunchAngle}°\n" +
                           $"起点高度：{snap.StartPosition.y} m";
    }

    void OnObserveData(float xD, float yD, float total, int count)
    {
        txtXDist.text = $"X位移：{xD:F2} m";
        txtYDist.text = $"Y位移：{yD:F2} m";
        txtTotalDist.text = $"总路程：{total:F2} m";
        txtPointCount.text = $"轨迹点数：{count}";
    }

    void OnPaused()
    {
        btnPause.interactable = false;   // 暂停后禁用暂停按钮
        btnResume.interactable = true;   // 启用恢复按钮
        txtStatus.text = "⏸ 已暂停";
    }

    void OnResumed()
    {
        btnPause.interactable = true;    // 启用暂停按钮
        btnResume.interactable = false;  // 禁用恢复按钮
        txtStatus.text = "▶ 运行中";
    }

    void OnReset()
    {
        btnPause.interactable = true;
        btnResume.interactable = false;
        txtStatus.text = "⚪ 空闲";
        HideError();
    }

    void ShowError(string msg)
    {
        txtError.text = msg;
        txtError.gameObject.SetActive(true);
        Invoke(nameof(HideError), 3f);
    }

    void HideError()
    {
        txtError.gameObject.SetActive(false);
    }
}