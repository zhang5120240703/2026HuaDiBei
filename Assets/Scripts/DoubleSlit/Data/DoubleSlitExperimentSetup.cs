using UnityEngine;

[AddComponentMenu("DoubleSlit/Double Slit Experiment Setup")]
public class DoubleSlitExperimentSetup : MonoBehaviour
{
    [Header("── 场景物体引用（自动配置光路Transform）──")]
    public GameObject lightSource;
    public GameObject singleSlit;
    public GameObject doubleSlit;

    void Start()
    {
        var lut = FindObjectOfType<DoubleSlitLUTGenerator>();
        if (lut != null)
        {
            if (lightSource != null) lut.lightSourceTf = lightSource.transform;
            if (singleSlit != null) lut.singleSlitTf = singleSlit.transform;
            if (doubleSlit != null) lut.doubleSlitTf = doubleSlit.transform;
        }

        var ctrl = FindObjectOfType<DoubleSlitSimpleController>();
        if (ctrl == null)
            Debug.LogError("[实验配置] 未找到 DoubleSlitSimpleController");
        else if (lut == null)
            Debug.LogError("[实验配置] 未找到 DoubleSlitLUTGenerator");
        else
            Debug.Log("[实验配置] 完成（光路 Transform 已自动配置）");
    }
}
