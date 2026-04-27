using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneCallback : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            StartCoroutine(TryShowSummary());
        }
    }

    IEnumerator TryShowSummary()
    {
        float timeout = 1f;
        while (UIManager.instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // ★ 改用 ExperimentDataManager 的最后一条记录来判断
        var dataMgr = ExperimentDataManager.Instance;
        var lastRecord = dataMgr != null ? dataMgr.GetLastRecord() : null;
        bool hasCompleteResult = lastRecord != null;

        Debug.Log($"[SceneCallback] UIManager={UIManager.instance != null}, dataMgr={dataMgr != null}, lastRecord={lastRecord != null}, hasCompleteResult={hasCompleteResult}");

        if (hasCompleteResult && UIManager.instance != null)
        {
            Debug.Log("[SceneCallback] 调用 ShowSummary()");
            UIManager.instance.ShowSummary();
        }
        else
        {
            Debug.Log("[SceneCallback] 不满足条件，不跳转总结");
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}