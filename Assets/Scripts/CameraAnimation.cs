using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraAnimation : MonoBehaviour
{
    public Transform TargetTransform;
    public Camera MainCamera;
    public Transform StartTransform;

    private void Start()
    {
        StartCoroutine(CameraMove(2f));
    }


    private IEnumerator CameraMove(float doration)
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
