using UnityEngine;
using TMPro;

/// <summary>
/// 单摆实验结果验证脚本
/// 功能：用户输入最终重力加速度 → 点击提交按钮 → 判断实验是否成功
/// 误差范围：±0.2 m/s² 内判定为成功
/// </summary>
public class PendulumExperimentJudge : MonoBehaviour
{
    [Header("=== 依赖组件 ===")]
    public PendulumDataRecorder dataRecorder;

    [Header("=== 用户最终输入 ===")]
    public TMP_InputField input_UserFinalG;    // 用户输入最终G的框

    [Header("=== 实验配置 ===")]
    [SerializeField] private float theoreticalG = 9.8f;
    [SerializeField] private float errorTolerance = 0.2f;

    [Header("=== 结果显示UI ===")]
    public TMP_Text judgeResultText;    // 显示：成功/失败
    public TMP_Text errorDetailText;    // 显示详细误差

    [Header("=== 结果颜色 ===")]
    public Color successColor = Color.green;
    public Color failColor = Color.red;
    public Color tipColor = Color.gray;

    void Start()
    {
       
        HideResultTexts();
    }

    /// <summary>
   
    /// 用户点击提交 → 显示结果并判定
    /// </summary>
    public void OnSubmitFinalG()
    {
        // 先显示结果UI
        ShowResultTexts();

        // 没填输入框
        if (input_UserFinalG == null || string.IsNullOrWhiteSpace(input_UserFinalG.text))
        {
            ShowResult("提示", "请输入最终重力加速度", tipColor);
            return;
        }

        // 解析数字
        bool isValid = float.TryParse(input_UserFinalG.text, out float userG);
        if (!isValid || userG <= 0)
        {
            ShowResult("输入错误", "请输入有效的正数", tipColor);
            return;
        }

        // 计算误差
        float error = Mathf.Abs(userG - theoreticalG);
        bool success = error <= errorTolerance;

        // 显示结果
        string title = success ? "实验成功" : "实验失败";
        string detail = $"你的结果：{userG:F2}\n理论值：9.80\n误差：{error:F2}\n允许误差：±0.20";

        ShowResult(title, detail, success ? successColor : failColor);
    }

    /// <summary>
    /// 显示结果文本
    /// </summary>
    private void ShowResultTexts()
    {
        if (judgeResultText != null) judgeResultText.gameObject.SetActive(true);
        if (errorDetailText != null) errorDetailText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏结果文本（初始状态）
    /// </summary>
    private void HideResultTexts()
    {
        if (judgeResultText != null) judgeResultText.gameObject.SetActive(false);
        if (errorDetailText != null) errorDetailText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 统一显示结果
    /// </summary>
    private void ShowResult(string title, string detail, Color color)
    {
        if (judgeResultText != null)
        {
            judgeResultText.text = title;
            judgeResultText.color = color;
        }

        if (errorDetailText != null)
        {
            errorDetailText.text = detail;
            errorDetailText.color = color;
        }
    }

    /// <summary>
    /// 重置所有内容 → 再次隐藏结果
    /// </summary>
    public void ResetAll()
    {
        if (input_UserFinalG != null)
            input_UserFinalG.text = "";

        ShowResult("未判定", "请输入结果并提交", tipColor);

        // 重置后重新隐藏
        HideResultTexts();
    }
}