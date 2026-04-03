using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.Networking;

public class PythonCommunication : MonoBehaviour
{
    public bool isValid;
    public State gameState = State.MainMenu;
    private void Start()
    {
        SendMessageToPython("连接Python中");
    }

    public void SendMessageToPython(string message)
    {
        StartCoroutine(PostRequest(message));
    }


    private void HandleAgentAction(AgentResponse res)
    {
        switch (res.action)
        {
            case "确认参数":
                Debug.Log("执行动作：确认参数");
                break;
            case "跳转到下一步":
                Debug.Log("执行动作：跳转到下一步");
                break;
            case "选择实验":
                Debug.Log("执行动作：选择实验");
                break;
            case "实验一开始":
                Debug.Log("执行动作：开始实验一");
                break;
            case "实验二开始":
                Debug.Log("执行动作：开始实验二");
                break;
            case "总结阶段":
                Debug.Log("执行动作：开始总结");
                break;
            default:
                Debug.Log("未知动作：" + res.action);
                break;
        }
    }

    IEnumerator PostRequest(string msg)
    {
        AgentRequest req = new AgentRequest
        {
            currentStep = "开始运行程序",
            runState = gameState.ToString(),
            isParamValid = isValid,
            availableActions = new string[] {"确认参数","跳转到下一步","选择实验","实验一开始","实验二开始","总结阶段"}
        };
        string json = JsonUtility.ToJson(req);
        byte[] postData = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest("http://127.0.0.1:5000/unity", "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-type", "application/json");

        yield return request.SendWebRequest();

        if(request.result == UnityWebRequest.Result.Success)
        {
            byte[] bytes = request.downloadHandler.data;
            string response = Encoding.UTF8.GetString(bytes);
            AgentResponse res = JsonUtility.FromJson<AgentResponse>(response);
            HandleAgentAction(res);
            Debug.Log("Python返回:"+ res.reply);
        }
        else
        {
            Debug.Log("Python连接失败"+request.error);
        }

    }
}

[System.Serializable]
/*public class PythonResponse
{
    public int code;
    public string msg;
}*/

public class AgentRequest
{
    public string currentStep;
    public string runState;
    public bool isParamValid;
    public string[] availableActions;
}

public class AgentResponse
{
    public string action;
    public string reply;
}
