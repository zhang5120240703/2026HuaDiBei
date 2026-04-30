using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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
    private bool _isDestroyed = false;

    void Start()
    {
        Debug.Log($"[SimpleExperimentUI] Start 执行, gameObject={gameObject.name}, enabled={enabled}");
        _ctrl = FindObjectOfType<ProjectileExperimentController>();
        if (_ctrl == null)
        {
            Debug.LogError("未找到 ProjectileExperimentController！");
            return;
        }
        ExperimentStateManager.Instance?.ResetExperiment();

        BindButtons();
        BindControllerEvents();
        InitUI();

        // 强制显示 Step1 并激活按钮
        ShowStepPanel(0);
        btnPause.interactable = true;
        btnResume.interactable = true;
        btnReset.interactable = true;
        btnConfirmStep1.interactable = true;
        Debug.Log($"[SimpleExperimentUI] btnConfirmStep1.interactable={btnConfirmStep1.interactable}");
    }

    IEnumerator DelayedReset()
    {
        yield return null;
        yield return null;
        yield return null;
        yield return null;
        _ctrl?.RequestReset();
    }

    void OnDestroy()
    {
        _isDestroyed = true;
        CancelInvoke();

        if (_ctrl != null)
        {
            _ctrl.OnStepEntered -= OnStepChanged;
            _ctrl.OnParamLoaded -= OnParamLoaded;
            _ctrl.OnParamError -= ShowError;
            _ctrl.OnSimulationReady -= OnSimulationReady;
            _ctrl.OnObserveData -= OnObserveData;
            _ctrl.OnPaused -= OnPaused;
            _ctrl.OnResumed -= OnResumed;
            _ctrl.OnReset -= OnReset;
            _ctrl.OnFlowError -= OnFlowError;
        }
    }

    void BindButtons()
    {
        Debug.Log($"[SimpleExperimentUI] BindButtons: btnPause={btnPause != null}, btnConfirmStep1={btnConfirmStep1 != null}");
     
        btnPause.onClick.AddListener(() => _ctrl?.RequestPause());
        btnResume.onClick.AddListener(() => _ctrl?.RequestResume());
        btnReset.onClick.AddListener(() => _ctrl?.RequestReset());

        btnConfirmStep1.onClick.AddListener(() =>
        {
            _ctrl?.RequestReset();
            // 等一帧让重置完成，再确认准备
            StartCoroutine(DelayedConfirmPrepare());
        });
        IEnumerator DelayedConfirmPrepare()
        {
            yield return null;
            _ctrl?.ConfirmPrepare();
        }

        btnConfirmStep2.onClick.AddListener(() =>
        {
            PushParams();
            _ctrl?.ConfirmParam();
        });

        btnConfirmStep4.onClick.AddListener(() => _ctrl?.ConfirmObserved());
        btnFinishSteps.onClick.AddListener(() => _ctrl?.ConfirmFinish());
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
        _ctrl.OnFlowError += OnFlowError;
    }

    void InitUI()
    {
        ShowStepPanel(0);

        if (inputVelocity != null) inputVelocity.text = "10";
        if (inputAngle != null) inputAngle.text = "45";
        if (inputDirX != null) inputDirX.text = "0";
        if (inputDirZ != null) inputDirZ.text = "1";
        if (inputStartY != null) inputStartY.text = "1";
        if (inputTimeStep != null) inputTimeStep.text = "0.02";
        if (inputTotalTime != null) inputTotalTime.text = "5";

        if (btnPause != null) btnPause.interactable = true;
        if (btnResume != null) btnResume.interactable = false;
        if (txtStatus != null) txtStatus.text = "⚪ 空闲";

        HideError();
    }

    void PushParams()
    {
        if (_ctrl == null) return;

        float v = ParseFloat(inputVelocity?.text, 10f);
        float angle = ParseFloat(inputAngle?.text, 45f);
        float dirX = ParseFloat(inputDirX?.text, 0f);
        float dirZ = ParseFloat(inputDirZ?.text, 1f);
        float startY = ParseFloat(inputStartY?.text, 1f);
        float dt = ParseFloat(inputTimeStep?.text, 0.02f);
        float totalT = ParseFloat(inputTotalTime?.text, 5f);

        _ctrl.SetParam(
            velocity: v,
            angle: angle,
            direction: new Vector3(dirX, 0, dirZ),
            startPosition: new Vector3(0, startY, 0),
            timeStep: dt,
            totalTime: totalT);
    }

    float ParseFloat(string text, float fallback)
    {
        if (string.IsNullOrEmpty(text)) return fallback;
        return float.TryParse(text, out float val) ? val : fallback;
    }

    void OnStepChanged(ExperimentStep step)
    {
        if (_isDestroyed) return;
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
        if (_isDestroyed) return;
        if (inputVelocity != null) inputVelocity.text = v.ToString();
        if (inputAngle != null) inputAngle.text = angle.ToString();
        if (inputDirX != null) inputDirX.text = dir.x.ToString();
        if (inputDirZ != null) inputDirZ.text = dir.z.ToString();
        if (inputStartY != null) inputStartY.text = pos.y.ToString();
        if (inputTimeStep != null) inputTimeStep.text = dt.ToString();
        if (inputTotalTime != null) inputTotalTime.text = T.ToString();
    }

    void OnSimulationReady(LaunchParamSnapshot snap, List<Vector3> points)
    {
        if (_isDestroyed || txtSimResult == null) return;

        txtSimResult.text = $"仿真完成\n" +
                           $"轨迹点数：{points.Count}\n" +
                           $"速度：{snap.InitialVelocity} m/s\n" +
                           $"仰角：{snap.LaunchAngle}°\n" +
                           $"起点高度：{snap.StartPosition.y} m";

        // 追加到本次流程的临时存储
        SessionDataStore.Add(
            SimulationDataBuffer.HorizontalDistance,
            SimulationDataBuffer.YDistance,
            SimulationDataBuffer.TotalDistance,
            SimulationDataBuffer.TrajectoryPointCount,
            snap.InitialVelocity,
            snap.LaunchAngle
        );
    }

    void OnObserveData(float xD, float yD, float total, int count)
    {
        if (_isDestroyed) return;
        if (txtXDist != null) txtXDist.text = $"{xD:F2} m";
        if (txtYDist != null) txtYDist.text = $"{yD:F2} m";
        if (txtTotalDist != null) txtTotalDist.text = $"{total:F2} m";
        if (txtPointCount != null) txtPointCount.text = $"{count}";
    }

    void OnPaused()
    {
        if (_isDestroyed) return;
        if (btnPause != null) btnPause.interactable = false;
        if (btnResume != null) btnResume.interactable = true;
        if (txtStatus != null) txtStatus.text = "已暂停";
    }

    void OnResumed()
    {
        if (_isDestroyed) return;
        if (btnPause != null) btnPause.interactable = true;
        if (btnResume != null) btnResume.interactable = false;
        if (txtStatus != null) txtStatus.text = "运行中";
    }

    void OnReset()
    {
        if (_isDestroyed) return;
        if (btnPause != null) btnPause.interactable = true;
        if (btnResume != null) btnResume.interactable = false;
        if (txtStatus != null) txtStatus.text = "空闲";
        HideError();
    }

    void ShowError(string msg)
    {
        if (_isDestroyed || txtError == null) return;
        txtError.text = msg;
        txtError.gameObject.SetActive(true);
        Invoke(nameof(HideError), 3f);
    }

    void HideError()
    {
        if (_isDestroyed || txtError == null) return;
        txtError.gameObject.SetActive(false);
    }

    void OnFlowError(string msg)
    {
        Debug.LogError($"[SimpleExperimentUI] 流程错误: {msg}");

        if (msg.Contains("不在准备阶段") || msg.Contains("不在参数设置阶段") ||
            msg.Contains("不在观察阶段") || msg.Contains("不在结束阶段"))
        {
            Debug.Log("[SimpleExperimentUI] 检测到步骤不匹配，自动重置流程...");
            _ctrl?.RequestReset();
        }
    }
}