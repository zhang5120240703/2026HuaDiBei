using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PythonCommunication : MonoBehaviour
{
    private const string DefaultRequestUrl = "http://127.0.0.1:5000/unity";
    private const string ActionConfirmPara = "ConfirmPara";
    //private const string ActionJumpToNextState = "JumpToNextState";
    private const string ActionChooseExp = "ChooseExp";
    private const string ActionStartFirstExp = "StartFirstExp";
    private const string ActionStartSecondExp = "StartSecondExp";
    private const string ActionEnterSummary = "EnterSummary";
    private const string ActionNoAction = "NoAction";
    private const string StepStartProgram = "StartProgram";
    private const string StepRunFirstExp = "RunFirstExp";
    private const string StepRunSecondExp = "RunSecondExp";
    private const string StepFinishSummary = "FinishSummary";
    private const string StepCompleted = "Completed";

    [Header("Request")]
    [SerializeField] private string requestUrl = DefaultRequestUrl;
    [SerializeField] private string currentStepMsg = "StartProgram";
    [SerializeField] private bool isValid;
    [SerializeField] private State gameState = State.MainMenu;

    [Header("Debug")]
    [SerializeField] private bool sendOnStart = true;

    private bool _isRequestInFlight;

    // 组件启动时执行；如果开启了自动发送，就在开始阶段向 Python 发起一次请求。
    private void Start()
    {
        if (string.IsNullOrWhiteSpace(currentStepMsg))
        {
            currentStepMsg = GetDefaultStepForState(gameState);
        }

        if (sendOnStart)
        {
            SendMessageToPython();
        }
    }

    // 使用当前 Inspector 面板中的配置直接发送一次测试请求。
    [ContextMenu("Send Test Request")]
    public void SendMessageToPython()
    {
        TrySendRequest();
    }

    // 外部可传入新的步骤描述；如果 message 非空，会先更新 currentStepMsg 再发送请求。
    public void SendMessageToPython(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            currentStepMsg = message;
        }

        TrySendRequest();
    }

    [ContextMenu("Reset Step For Current State")]
    public void ResetStepForCurrentState()
    {
        currentStepMsg = GetDefaultStepForState(gameState);
    }

    // 请求发送前的保护入口；如果已有请求在进行中，则拒绝重复发送。
    private void TrySendRequest()
    {
        if (_isRequestInFlight)
        {
            Debug.LogWarning("已有请求在进行中，已拒绝新的请求", this);
            return;
        }

        StartCoroutine(PostRequest());
    }

    //构建发送的json消息
    private AgentRequest BuildRequest()
    {
        string currentStep = GetCurrentStep();

        return new AgentRequest
        {
            currentStep = currentStep,
            runState = gameState.ToString(),
            isParamValid = isValid,
            availableActions = GetAvailableActions(currentStep)
        };
    }

    private string GetCurrentStep()
    {
        if (string.IsNullOrWhiteSpace(currentStepMsg))
        {
            currentStepMsg = GetDefaultStepForState(gameState);
        }

        return currentStepMsg;
    }

    private string GetDefaultStepForState(State state)
    {
        //根据 state 的不同，返回不同的值
        return state switch
        {
            State.MainMenu => StepStartProgram,
            State.FirstExp => StepRunFirstExp,
            State.SecondExp => StepRunSecondExp,
            State.Summary => StepFinishSummary,
            _ => StepStartProgram
        };
    }

    // 根据当前状态和参数是否合法，动态生成本次请求允许执行的动作白名单。
    private string[] GetAvailableActions(string currentStep)
    {
        if (!isValid)
        {
            return Array.Empty<string>();
        }

        return (gameState, currentStep) switch
        {
            (State.MainMenu, StepStartProgram) => new[] { ActionChooseExp },
            (State.FirstExp, StepRunFirstExp) => new[] { ActionStartFirstExp },
            (State.SecondExp, StepRunSecondExp) => new[] { ActionStartSecondExp },
            (State.Summary, StepFinishSummary) => new[] { ActionEnterSummary },
            (State.Summary, StepCompleted) => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
    }

    //根据返回的action执行对应的功能
    private void HandleAgentAction(AgentResponse response)
    {
        switch (response.action)
        {
            case ActionConfirmPara:
                Debug.Log("执行操作：确认参数", this);
                break;
            /*case ActionJumpToNextState:
                Debug.Log("Execute action: JumpToNextState", this);
                break;*/
            case ActionChooseExp:
                Debug.Log("执行操作：选择实验", this);
                break;
            case ActionStartFirstExp:
                Debug.Log("执行操作：开始第一个实验", this);
                break;
            case ActionStartSecondExp:
                Debug.Log("E执行操作：开始第二个实验", this);
                break;
            case ActionEnterSummary:
                Debug.Log("执行操作：进入总结环节", this);
                break;
            case ActionNoAction:
                Debug.Log("请求失败，无动作", this);
                return;
            default:
                Debug.LogWarning("Unknown action: " + response.action, this);
                return;
        }

        ApplyNextState(response.nextState);
        ApplyNextStep(response.nextStep);
    }

    // 将 Python 返回的 nextState 字符串解析为本地枚举，并在合法时更新当前状态。
    private void ApplyNextState(string nextState)
    {
        if (string.IsNullOrWhiteSpace(nextState))
        {
            return;
        }

        if (!Enum.TryParse(nextState, out State parsedState))
        {
            Debug.LogWarning("Unknown nextState: " + nextState, this);
            return;
        }

        if (gameState != parsedState)
        {
            Debug.Log($"State change: {gameState} -> {parsedState}", this);
            gameState = parsedState;
        }
    }

    private void ApplyNextStep(string nextStep)
    {
        if (string.IsNullOrWhiteSpace(nextStep))
        {
            return;
        }

        if (currentStepMsg != nextStep)
        {
            Debug.Log($"Step change: {currentStepMsg} -> {nextStep}", this);
            currentStepMsg = nextStep;
        }
    }

    // 检查返回的 action 是否属于本次请求允许的动作，避免执行越界操作。
    private bool IsActionAllowed(string action, string[] allowedActions)
    {
        if(action == ActionNoAction)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(action) || allowedActions == null)
        {
            return false;
        }


        if (allowedActions.Contains(action))
        {
            return true;
        }

        return false;
    }

    // 负责完整的通信流程：构造请求、发送给 Python、解析响应、校验动作并执行。
    private IEnumerator PostRequest()
    {
        _isRequestInFlight = true;
        try
        {
            AgentRequest requestBody = BuildRequest();
            string json = JsonUtility.ToJson(requestBody);
            byte[] postData = Encoding.UTF8.GetBytes(json);

            Debug.Log("发送信息：" + json, this);

            using (UnityWebRequest request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(postData);
                request.downloadHandler = new DownloadHandlerBuffer();
                //通知python按照JSON解析
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    AgentResponse response = JsonUtility.FromJson<AgentResponse>(responseText);

                    if (response == null)
                    {
                        Debug.LogWarning("Python返回了一个空JSON信息", this);
                    }
                    else if (!IsActionAllowed(response.action, requestBody.availableActions))
                    {
                        Debug.LogWarning("拒绝执行操作： " + response.action, this);
                    }
                    else
                    {
                        Debug.Log("Python回复：" + response.reply, this);
                        HandleAgentAction(response);
                    }
                }
                else
                {
                    Debug.LogError("Python request failed: " + request.error, this);
                }
            }
        }
        finally
        {
            _isRequestInFlight = false;
        }
    }
}

[Serializable]
public class AgentRequest
{
    public string currentStep;
    public string runState;
    public bool isParamValid;
    public string[] availableActions;
}

[Serializable]
public class AgentResponse
{
    public string action;
    public string nextState;
    public string nextStep;
    public string reply;
}
