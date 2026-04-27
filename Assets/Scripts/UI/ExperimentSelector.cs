using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperimentSelector : MonoBehaviour
{
    [Header("实验数据配置")]
    public ExperimentData[] experimentDatas;

    [Header("UI引用（原有逻辑，完全保留）")]
    public Image[] thumbnailButtons;
    public TMP_Text nameText;
    public Image bigPreviewImage;

    [Header("面板切换")]
    public GameObject UI1_ListPanel;
    public GameObject UI2_PreviewPanel;

    private ExperimentData currentSelectedData;

    void Start()
    {
        for (int i = 0; i < thumbnailButtons.Length; i++)
        {
            int index = i;
            Button btn = thumbnailButtons[i].GetComponent<Button>();
            btn.onClick.AddListener(() => SelectExperiment(index));
        }

        if (experimentDatas.Length > 0)
        {
            SelectExperiment(0);
        }
    }

    public void SelectExperiment(int index)
    {
        if (index < 0 || index >= experimentDatas.Length) return;

        currentSelectedData = experimentDatas[index];

        nameText.text = currentSelectedData.experimentName;
        bigPreviewImage.sprite = currentSelectedData.previewSprite;
    }

    public void OnPreviewButtonClick()
    {
        if (currentSelectedData == null)
        {
            Debug.LogWarning("没有选中任何实验！");
            return;
        }

        UI2_ExperimentPreview previewUI = UI2_PreviewPanel.GetComponent<UI2_ExperimentPreview>();
        previewUI.SetExperimentData(currentSelectedData);

        // 把 SO 中的场景名传给 UIManager
        UIManager.instance.SelectExperiment(currentSelectedData.sceneName, currentSelectedData.experimentName);

        // 把 SO 中的显示名称写入全局管理器和桥接器
        if (ExpGlobalManager.Instance != null)
        {
            ExpGlobalManager.Instance.curSelectData = currentSelectedData;
        }

        UI1_ListPanel.SetActive(false);
        UI2_PreviewPanel.SetActive(true);
    }
}