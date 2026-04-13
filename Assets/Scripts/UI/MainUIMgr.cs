using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    public GameObject[] uiPages; // 顺序：UI0、UI1、UI2、UI4、UI5

    private int currentIndex = 0;

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
        currentIndex = index;
        uiPages[currentIndex].SetActive(true);
    }

    public void Next()
    {
        if (currentIndex == 2)
        {
            // 协程切换到UI3场景
            StartCoroutine(LoadSceneUI3());
        }
        else if (currentIndex == 4)
        {
            // 最后一页回到第一页
            ShowPage(0);
        }
        else
        {
            ShowPage(currentIndex + 1);
        }
    }

    public void Back()
    {
        ShowPage(currentIndex - 1);
    }

    // 协程加载UI3场景
    IEnumerator LoadSceneUI3()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync("SceneUI3");
        yield return op;
    }

    // 从SceneUI3返回后，主场景会调用这个
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene")
        {
            ShowPage(3); // 直接显示UI4
        }
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
}
