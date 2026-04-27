using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    public GameObject[] uiPages;

    private int currentPage = 0;
    private string selectedScene;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        HideAll();

        var dataMgr = ExperimentDataManager.Instance;
        var lastRecord = dataMgr != null ? dataMgr.GetLastRecord() : null;
        bool hasCompleteResult = lastRecord != null;

        if (hasCompleteResult)
        {
            ShowPage(3);
        }
        else
        {
            ShowPage(0);
        }
    }

    void HideAll()
    {
        foreach (var ui in uiPages) ui.SetActive(false);
    }

    public void ShowPage(int index)
    {
        HideAll();
        currentPage = index;
        if (index >= 0 && index < uiPages.Length && uiPages[index] != null)
            uiPages[index].SetActive(true);
    }

    public void SelectExperiment(string sceneName)
    {
        selectedScene = sceneName;
        ShowPage(1);
    }

    public void StartExperiment()
    {
        if (ExperimentResultBridge.Instance != null)
        {
            ExperimentResultBridge.Instance.Clear();
            ExperimentResultBridge.Instance.experimentName = selectedScene;
            ExperimentResultBridge.Instance.startTime = Time.time;
        }

        StartCoroutine(LoadToScene());
    }

    public void ShowSummary()
    {
        ShowPage(3);
    }

    public void Next()
    {
        if (currentPage == 3)
        {
            ShowPage(0);
        }
        else
        {
            ShowPage(currentPage + 1);
        }
    }

    public void Back()
    {
        ShowPage(currentPage - 1);
    }

    public IEnumerator LoadToScene()
    {
        yield return SceneManager.LoadSceneAsync(selectedScene);
    }
}