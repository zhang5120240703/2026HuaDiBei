using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.Networking;

public class PythonCommunication : MonoBehaviour
{
    private void Start()
    {
        SendMessageToPython("连接Python中");
    }

    public void SendMessageToPython(string message)
    {
        StartCoroutine(PostRequest(message));
    }

    IEnumerator PostRequest(string msg)
    {
        string json = "{\"msg\":\"" + msg + "\"}";
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
            PythonResponse res = JsonUtility.FromJson<PythonResponse>(response);
            Debug.Log("Python返回:"+ res.msg);
        }
        else
        {
            Debug.Log("Python连接失败"+request.error);
        }

    }
}

[System.Serializable]
public class PythonResponse
{
    public int code;
    public string msg;
}