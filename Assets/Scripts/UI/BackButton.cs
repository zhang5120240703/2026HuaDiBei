using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    public void OnClickBack()
    {
        SceneManager.LoadScene("MainScene");
    }
}
