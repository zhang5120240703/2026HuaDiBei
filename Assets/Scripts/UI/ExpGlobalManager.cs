using UnityEngine;

public class ExpGlobalManager : MonoBehaviour
{
    public static ExpGlobalManager Instance;

    // 当前选中实验下标
    public int selectIndex;
    // 当前选中实验数据
    public ExperimentData curSelectData;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
