using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    // 顺序拖入：UI0、UI1、UI2、UI4、UI5
    public GameObject[] uiPages;

    private int currentPage = 0;
    private string selectedScene; // 记录你选的实验场景

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        HideAll();
        ShowPage(0);
    }

    void HideAll()
    {
        foreach (var ui in uiPages) ui.SetActive(false);
    }

    public void ShowPage(int index)
    {
        HideAll();
        currentPage = index;
        uiPages[index].SetActive(true);
    }

    // UI1 选择实验后调用：记录场景名，并跳到预览 UI2
    public void SelectExperiment(string sceneName)
    {
        selectedScene = sceneName;
        ShowPage(1); // 跳到 UI2（预览）
    }

    // UI2 点开始实验：跳去对应场景
    public void StartExperiment()
    {
        StartCoroutine(LoadToScene());
        //SceneManager.LoadScene(selectedScene);
    }

    // 从实验场景回来 → 显示总结 UI4
    public void ShowSummary()
    {
        ShowPage(2); // UI4
    }

    // 下一页
    public void Next()
    {
        if (currentPage == 3) // UI5 结束
        {
            ShowPage(0); // 回到开始
        }
        else
        {
            ShowPage(currentPage + 1);
        }
    }

    // 返回
    public void Back()
    {
        ShowPage(currentPage - 1);
    }

    public IEnumerator LoadToScene()
    {
        yield return SceneManager.LoadSceneAsync(selectedScene);
    }

}
