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

        var dataMgr = ExperimentDataManager.Instance;
        var lastRecord = dataMgr != null ? dataMgr.GetLastRecord() : null;
        bool hasCompleteResult = lastRecord != null;

        if (hasCompleteResult && UIManager.instance != null)
        {
            UIManager.instance.ShowSummary();
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}