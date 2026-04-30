using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PendulumAI : MonoBehaviour
{
    [SerializeField] private PythonCommunication PC;
    [SerializeField] private PendulumExperimentAIController PEAI;
    [SerializeField] private TMP_InputField studentContent;
    [SerializeField] protected TMP_Text AIText;

    private ExperimentPackage data;

    private void Start()
    {
        //PC = GameObject.Find("AIManager").GetComponent<PythonCommunication>();
    }

    public void OnEnable()
    {
        PC.OnResponseReceived += OnReciveAIResponse;
    }

    public void OnDisable()
    {
        PC.OnResponseReceived -= OnReciveAIResponse;
    }

    public void GetData()
    {
        data = PEAI.GetAllData();
    }

    public void BuildRequest()
    {
        studentContent.text = "请输入内容...";
        if (data == null)
        {
            GetData();
        }

        string dataText = BuildDataText();

        PC.SendChatMessage(studentContent.text,"单摆运动的研究","你是一个中学物理实验的虚拟AI教学助手，现在学生需要你帮忙回答一下他在content字段中提出的问题，请用温和的有耐心的语气来解答，只输出纯文本，禁用markdown模式，目前学生的实验数据为："+dataText);
    }

    private string BuildDataText()
    {
        if (data == null)
        {
            return "暂无实验数据";
        }

        return
            "基础摆长(m): " + data.pendulumLength + "\n" +
            "理论重力加速度(m/s2): " + data.gravity + "\n" +
            "当前摆长(m): " + data.currentLength + "\n" +
            "当前摆角(deg): " + data.currentAngle + "\n" +
            "完成周期数: " + data.totalCycles + "\n" +
            "平均周期(s): " + data.averageCycle + "\n" +
            "各次周期记录(s): " + FormatFloatArray(data.allCycles) + "\n" +
            "各组g值(m/s2): " + FormatFloatArray(data.allGValues) + "\n" +
            "最终平均g值(m/s2): " + data.finalAverageG + "\n" +
            "数据是否有效: " + data.allDataValid + "\n" +
            "用户输入g值(m/s2): " + data.userInputG + "\n" +
            "当前动能(J): " + data.kinetic + "\n" +
            "当前势能(J): " + data.potential + "\n" +
            "当前总机械能(J): " + data.totalEnergy;
    }

    public void OnReciveAIResponse(AgentResponse response)
    {
        AIText.text = "";
        StartCoroutine(TextAnimation(response));
    }

    private string FormatFloatArray(float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "无";
        }

        return string.Join(", ", values);
    }

    IEnumerator TextAnimation(AgentResponse response)
    {
        for (int i = 0; i < response.reply.Length; i++)
        {
            AIText.text += response.reply[i];
            yield return null;
        }
    }
}
