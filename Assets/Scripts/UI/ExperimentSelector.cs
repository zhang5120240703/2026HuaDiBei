using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperimentSelector : MonoBehaviour
{
    [Header("实验数据配置")]
    public ExperimentData[] experimentDatas;

    [Header("UI引用（原有逻辑，完全保留）")]
    public Image[] thumbnailButtons;  // 左侧缩略图按钮
    public TMP_Text nameText;         // 实验名称文本
    public Image bigPreviewImage;      // 右侧大图预览

    [Header("面板切换")]
    public GameObject UI1_ListPanel;   // 实验列表面板
    public GameObject UI2_PreviewPanel;// 预览面板

    private ExperimentData currentSelectedData;

    void Start()
    {
        // 初始化缩略图按钮
        for (int i = 0; i < thumbnailButtons.Length; i++)
        {
            int index = i;
            Button btn = thumbnailButtons[i].GetComponent<Button>();
            btn.onClick.AddListener(() => SelectExperiment(index));
        }

        // 默认选中第一个实验
        if (experimentDatas.Length > 0)
        {
            SelectExperiment(0);
        }
    }

    // 【原有逻辑】点缩略图：更新右侧大图和名字，不跳转
    public void SelectExperiment(int index)
    {
        if (index < 0 || index >= experimentDatas.Length) return;

        // 记录当前选中的实验数据
        currentSelectedData = experimentDatas[index];

        // 更新右侧UI：实验名 + 大图
        nameText.text = currentSelectedData.experimentName;
        bigPreviewImage.sprite = currentSelectedData.previewSprite;
    }

    // 【核心逻辑】预览按钮点击事件：点击才跳UI2并传数据
    public void OnPreviewButtonClick()
    {
        if (currentSelectedData == null)
        {
            Debug.LogWarning("没有选中任何实验！");
            return;
        }

        // 给UI2面板的脚本传数据
        UI2_ExperimentPreview previewUI = UI2_PreviewPanel.GetComponent<UI2_ExperimentPreview>();
        previewUI.SetExperimentData(currentSelectedData);

        // 切换面板
        UI1_ListPanel.SetActive(false);
        UI2_PreviewPanel.SetActive(true);
    }
}
