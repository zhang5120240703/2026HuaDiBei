using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class PendulumDataRecorder : MonoBehaviour
{
    [Header("=== 表格UI绑定 ===")]
    // 第一组
    public TMP_InputField input_L_1;
    public TMP_InputField input_T_1;
    public TMP_Text text_G_1;

    // 第二组
    public TMP_InputField input_L_2;
    public TMP_InputField input_T_2;
    public TMP_Text text_G_2;

    // 第三组
    public TMP_InputField input_L_3;
    public TMP_InputField input_T_3;
    public TMP_Text text_G_3;

    [Header("=== 结果显示 ===")]
    public TMP_Text text_AverageG;
    public Button btn_Calculate;
    public Button btn_Save;
    public Button btn_Reset;

   public struct ExperimentData
    {
        public float length;
        public float period;
        public float gValue;
        public bool isValid;
    }

    private ExperimentData[] experimentDatas = new ExperimentData[3];
    private const float PI = Mathf.PI;

    #region ===================== 【AI 实验数据接口】 =====================
    /// <summary>
    /// 获取所有3组实验原始数据
    /// 每组包含：摆长、周期、g、是否有效
    /// </summary>
    public ExperimentData[] GetAllExperimentData() => experimentDatas;

    /// <summary>
    /// 获取所有组计算出的重力加速度
    /// 单位：m/s²
    /// </summary>
    public float[] GetAllGValues()
    {
        var list = new System.Collections.Generic.List<float>();
        foreach (var item in experimentDatas) list.Add(item.gValue);
        return list.ToArray();
    }

    /// <summary>
    /// 获取最终平均重力加速度
    /// 单位：m/s²
    /// </summary>
    public float GetFinalAverageG()
    {
        float total = 0;
        int count = 0;
        foreach (var d in experimentDatas)
        {
            if (d.isValid) { total += d.gValue; count++; }
        }
        return count > 0 ? total / count : 0;
    }

    /// <summary>
    /// 判断所有实验数据是否完整有效
    /// AI用于判断实验是否完成
    /// </summary>
    public bool IsAllExperimentsValid()
    {
        foreach (var d in experimentDatas)
            if (!d.isValid) return false;
        return true;
    }
    #endregion
    void Start()
    {
        ResetAllData();

        if (btn_Calculate != null) btn_Calculate.onClick.AddListener(CalculateAllData);
        if (btn_Save != null) btn_Save.onClick.AddListener(SaveDataToFile);
        if (btn_Reset != null) btn_Reset.onClick.AddListener(ResetAllData);
    }

    public void CalculateAllData()
    {
        CalculateSingleGroup(0, input_L_1, input_T_1, text_G_1);
        CalculateSingleGroup(1, input_L_2, input_T_2, text_G_2);
        CalculateSingleGroup(2, input_L_3, input_T_3, text_G_3);
        CalculateAverageG();
    }

    /// <summary>
    /// 核心修复：区分「空输入（没填）」和「无效输入（填错）」
    /// 没填的组保持「——」，只有填错才显示「输入无效」
    /// </summary>
    private void CalculateSingleGroup(int index, TMP_InputField inputL, TMP_InputField inputT, TMP_Text textG)
    {
        if (inputL == null || inputT == null || textG == null)
        {
            Debug.LogError($"第{index + 1}组UI组件未绑定");
            return;
        }

        // 1. 先判断是否为空输入（用户没填）
        bool isLEmpty = string.IsNullOrWhiteSpace(inputL.text);
        bool isTEmpty = string.IsNullOrWhiteSpace(inputT.text);

        // 如果任意一个输入为空，说明用户没填，不修改显示，保持原有「——」
        if (isLEmpty || isTEmpty)
        {
            experimentDatas[index] = new ExperimentData { isValid = false };
            return; // 关键：不修改textG，保留初始状态
        }

        // 2. 输入不为空，再验证是否为有效数字
        bool isLValid = float.TryParse(inputL.text, out float length) && length > 0;
        bool isTValid = float.TryParse(inputT.text, out float period) && period > 0;

        // 3. 输入不为空但无效，才显示「输入无效」
        if (!isLValid || !isTValid)
        {
            textG.text = "输入无效";
            experimentDatas[index] = new ExperimentData { isValid = false };
            return;
        }

        // 4. 输入有效，正常计算重力加速度
        float g = (4 * PI * PI * length) / (period * period);

        experimentDatas[index] = new ExperimentData
        {
            length = length,
            period = period,
            gValue = g,
            isValid = true
        };

        textG.text = $"{g:F2} m/s²";
    }

    private void CalculateAverageG()
    {
        float totalG = 0;
        int validCount = 0;

        foreach (var data in experimentDatas)
        {
            if (data.isValid)
            {
                totalG += data.gValue;
                validCount++;
            }
        }

        if (text_AverageG != null)
        {
            if (validCount == 0)
            {
                text_AverageG.text = "平均值：——";
            }
            else
            {
                float average = totalG / validCount;
                text_AverageG.text = $"平均值：{average:F2} m/s²";
            }
        }
    }

    public void ResetAllData()
    {
        // 清空输入框
        ClearInputField(input_L_1);
        ClearInputField(input_T_1);
        ClearInputField(input_L_2);
        ClearInputField(input_T_2);
        ClearInputField(input_L_3);
        ClearInputField(input_T_3);

        // 重置结果为「——」，初始状态
        SetText(text_G_1, "——");
        SetText(text_G_2, "——");
        SetText(text_G_3, "——");
        SetText(text_AverageG, "平均值：——");

        // 重置数据数组
        for (int i = 0; i < experimentDatas.Length; i++)
        {
            experimentDatas[i] = new ExperimentData { isValid = false };
        }
    }

    public void SaveDataToFile()
    {
        string dataContent = "单摆实验数据记录\n";
        dataContent += "====================\n";
        dataContent += $"记录时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
        dataContent += "组别\t摆长(m)\t周期(s)\t重力加速度(m/s²)\n";

        float totalG = 0;
        int validCount = 0;
        for (int i = 0; i < experimentDatas.Length; i++)
        {
            var data = experimentDatas[i];
            if (data.isValid)
            {
                dataContent += $"{i + 1}\t{data.length:F2}\t{data.period:F2}\t{data.gValue:F2}\n";
                totalG += data.gValue;
                validCount++;
            }
            else
            {
                // 空输入/未填写的组，在文件中标记为「未填写」，而非「无效」
                dataContent += $"{i + 1}\t未填写\t未填写\t未填写\n";
            }
        }

        if (validCount > 0)
        {
            float average = totalG / validCount;
            dataContent += $"\n平均重力加速度：{average:F2} m/s²";
        }
        else
        {
            dataContent += "\n平均重力加速度：未填写";
        }

        string savePath = Path.Combine(Application.persistentDataPath, "PendulumExperiment_" + System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
        File.WriteAllText(savePath, dataContent, System.Text.Encoding.UTF8);
        Debug.Log($"数据已保存到：{savePath}");
    }

    #region 工具方法
    private void ClearInputField(TMP_InputField input)
    {
        if (input != null) input.text = "";
    }

    private void SetText(TMP_Text text, string content)
    {
        if (text != null) text.text = content;
    }
    #endregion

    [ContextMenu("填充测试数据")]
    private void FillTestData()
    {
        input_L_1.text = "2.33";
        input_T_1.text = "2.44";
        input_L_2.text = "1.5";
        input_T_2.text = "2.45";
        input_L_3.text = "1.0";
        input_T_3.text = "2.00";
        CalculateAllData();
    }
}