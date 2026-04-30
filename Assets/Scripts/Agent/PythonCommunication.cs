using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PythonCommunication : MonoBehaviour
{
    private const string DefaultRequestUrl = "http://127.0.0.1:5000/unity";

    [Header("Python 服务地址")]
    [SerializeField] private string requestUrl = DefaultRequestUrl;

    // 外部脚本订阅：收到 AI 响应后自行处理
    public event Action<AgentResponse> OnResponseReceived;
    // 外部脚本订阅：请求发送失败时处理错误
    public event Action<string> OnRequestFailed;

    // 防止同一时间重复发送多个请求
    private bool _isRequestInFlight;

    // 发送普通消息给 AI，例如实验场景中的实时问答
    public void SendChatMessage(string message, string experimentName = "", string extraContext = "")
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning("发送给 AI 的消息为空。", this);
            OnRequestFailed?.Invoke("发送给 AI 的消息为空。");
            return;
        }

        AgentRequest requestBody = new AgentRequest
        {
            requestType = AgentRequestType.Message,
            experimentName = experimentName ?? string.Empty,
            content = message,
            summaryContext = string.Empty,
            extraContext = extraContext ?? string.Empty,
            metadataJson = string.Empty,
        };

        SendRequest(requestBody);
    }

    // 请求 AI 生成实验总结
    public void RequestExperimentSummary(
        string experimentName,
        string summaryContext,
        string extraContext = "",
        string metadataJson = "")
    {
        if (string.IsNullOrWhiteSpace(summaryContext))
        {
            Debug.LogWarning("实验总结内容为空。", this);
            OnRequestFailed?.Invoke("实验总结内容为空。");
            return;
        }

        AgentRequest requestBody = new AgentRequest
        {
            requestType = AgentRequestType.Summary,
            experimentName = experimentName ?? string.Empty,
            content = string.Empty,
            summaryContext = summaryContext,
            extraContext = extraContext ?? string.Empty,
            metadataJson = metadataJson ?? string.Empty,
        };

        SendRequest(requestBody);
    }

    // 通用发送入口：以后新增 guidance、qa、report 等类型时，直接从外部组装 AgentRequest 即可
    public void SendRequest(AgentRequest requestBody)
    {
        if (requestBody == null)
        {
            Debug.LogWarning("请求体为空。", this);
            OnRequestFailed?.Invoke("请求体为空。");
            return;
        }

        if (_isRequestInFlight)
        {
            Debug.LogWarning("已有请求正在进行中", this);
            OnRequestFailed?.Invoke("已有请求正在进行中");
            return;
        }

        NormalizeRequest(requestBody);

        string validationError = ValidateRequest(requestBody);
        if (!string.IsNullOrEmpty(validationError))
        {
            Debug.LogWarning(validationError, this);
            OnRequestFailed?.Invoke(validationError);
            return;
        }

        StartCoroutine(PostRequest(requestBody));
    }

    // 统一整理请求字段，避免外部脚本传入 null
    private void NormalizeRequest(AgentRequest requestBody)
    {
        requestBody.requestType = (requestBody.requestType ?? string.Empty).Trim().ToLowerInvariant();
        requestBody.experimentName = requestBody.experimentName ?? string.Empty;
        requestBody.content = requestBody.content ?? string.Empty;
        requestBody.summaryContext = requestBody.summaryContext ?? string.Empty;
        requestBody.extraContext = requestBody.extraContext ?? string.Empty;
        requestBody.metadataJson = requestBody.metadataJson ?? string.Empty;
    }

    // 当前只启用 message 和 summary 两类请求，其余类型先预留
    private string ValidateRequest(AgentRequest requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody.requestType))
        {
            return "请求类型为空。";
        }

        if (requestBody.requestType == AgentRequestType.Message &&
            string.IsNullOrWhiteSpace(requestBody.content))
        {
            return "普通消息内容为空。";
        }

        if (requestBody.requestType == AgentRequestType.Summary &&
            string.IsNullOrWhiteSpace(requestBody.summaryContext))
        {
            return "实验总结内容为空。";
        }

        return null;
    }

    // 完整通信流程：序列化请求、发送到 Python、解析响应并抛出事件
    private IEnumerator PostRequest(AgentRequest requestBody)
    {
        _isRequestInFlight = true;

        try
        {
            string json = JsonUtility.ToJson(requestBody);
            byte[] postData = Encoding.UTF8.GetBytes(json);

            Debug.Log("发送信息：" + json, this);

            using (UnityWebRequest request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(postData);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    AgentResponse response = JsonUtility.FromJson<AgentResponse>(responseText);

                    if (response == null)
                    {
                        Debug.LogWarning("Python 返回了一个空 JSON 响应。", this);
                        OnRequestFailed?.Invoke("Python 返回了一个空 JSON 响应。");
                    }
                    else
                    {
                        NormalizeResponse(response);
                        Debug.Log("Python 回复：" + response.reply, this);
                        OnResponseReceived?.Invoke(response);
                    }
                }
                else
                {
                    Debug.LogError("Python 连接错误：" + request.error, this);
                    OnRequestFailed?.Invoke(request.error);
                }
            }
        }
        finally
        {
            _isRequestInFlight = false;
        }
    }

    // 统一整理响应字段，给外部脚本一个稳定的数据结构
    private void NormalizeResponse(AgentResponse response)
    {
        response.responseType = response.responseType ?? string.Empty;
        response.reply = response.reply ?? string.Empty;
        response.suggestion = response.suggestion ?? string.Empty;
        response.extraDataJson = response.extraDataJson ?? string.Empty;
    }
}

// 请求类型常量：
// 当前已启用 message、summary；
// guidance、qa、report 等类型先预留，后面加功能时直接复用这套协议。
public static class AgentRequestType
{
    public const string Message = "message";
    public const string Summary = "summary";

    // 预留扩展类型
    public const string Guidance = "guidance";
    public const string Qa = "qa";
    public const string Report = "report";
}

// 响应类型常量：
// 当前已启用 message、summary、error；
// 以后如果加 guidance、qa，也建议沿用同样命名。
public static class AgentResponseType
{
    public const string Message = "message";
    public const string Summary = "summary";
    public const string Error = "error";

    // 预留扩展类型
    public const string Guidance = "guidance";
    public const string Qa = "qa";
    public const string Report = "report";
}

[Serializable]
public class AgentRequest
{
    // 请求类型：当前使用 message 或 summary
    public string requestType;
    // 实验名称，例如“单摆实验”
    public string experimentName;
    // 普通消息内容，例如学生提问
    public string content;
    // 实验总结所需的上下文文本
    public string summaryContext;
    // 预留扩展字段：补充说明、阶段说明、问题背景等
    public string extraContext;
    // 预留扩展字段：结构化 JSON 文本，后面可放实验数据、学生记录等
    public string metadataJson;
}

[Serializable]
public class AgentResponse
{
    // 响应类型：message、summary、error，后面可扩展 guidance、qa
    public string responseType;
    // AI 返回的主文本内容
    public string reply;
    // 预留扩展字段：建议语句、下一步提示等
    public string suggestion;
    // 预留扩展字段：结构化 JSON 文本，后面可放评分、标签、摘要等
    public string extraDataJson;
}
