using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 挂载在 MainMenu 的 UI4 总结面板 GameObject 上。
/// 职责：显示实验名称、本次用时、历史数据列表。
/// </summary>
public class UI4_SummaryPanel : MonoBehaviour
{
    [Header("三个 TMP_Text（在 Inspector 拖入）")]
    public TMP_Text txtExperimentName;   // Text 1: 实验名称
    public TMP_Text txtDuration;         // Text 2: 本次用时
    public TMP_Text txtDataList;         // Text 3: 数据罗列

    void OnEnable()
    {
        RefreshSummary();
    }

    /// <summary>
    /// 打开面板时自动刷新，也可由 UIManager 主动调用
    /// </summary>
    public void RefreshSummary()
    {
        var dataMgr = ExperimentDataManager.Instance;
        var lastRecord = dataMgr?.GetLastRecord();

        // Text 1: 实验名称
        if (txtExperimentName != null)
        {
            string expName = lastRecord != null ? lastRecord.experimentName : "未知实验";
            txtExperimentName.text = $"实验：{expName}";
        }

        // Text 2: 用时
        if (txtDuration != null)
        {
            string duration = lastRecord != null ? lastRecord.duration : "—";
            txtDuration.text = $"用时：{duration}";
        }

        // Text 3: 历史数据列表
        if (txtDataList != null && dataMgr != null)
        {
            string expName = lastRecord != null ? lastRecord.experimentName : "";
            List<ExperimentRecord> filtered = !string.IsNullOrEmpty(expName)
                ? dataMgr.GetRecordsByName(expName)
                : dataMgr.Records;

            if (filtered.Count == 0)
            {
                txtDataList.text = "暂无实验记录";
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < filtered.Count; i++)
                {
                    sb.AppendLine($"第{i + 1}次: {filtered[i].ToDisplayString()}");
                }
                txtDataList.text = sb.ToString();
            }
        }
    }

    /// <summary>
    /// 关闭总结面板时的清理（由关闭按钮调用）
    /// </summary>
    public void OnCloseSummary()
    {
        // 桥接器数据已存入 DataManager，可以安全清空
        ExperimentResultBridge.Instance?.Clear();
    }
}