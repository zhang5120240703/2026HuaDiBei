using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CameraAnimation : MonoBehaviour
{
    public Transform TargetTransform;
    public Camera MainCamera;
    public Transform StartTransform;

    private void Start()
    {
        StartCoroutine(Test(2f));
    }


    private IEnumerator Test(float doration)
    {
        Vector3 target = TargetTransform.position;
        Vector3 start = StartTransform.position;
        float nowTime = 0f;


        while(nowTime<doration)
        {
            nowTime += Time.deltaTime;
            float t = nowTime / doration;
            MainCamera.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        MainCamera.transform.position = target;
    }
}
