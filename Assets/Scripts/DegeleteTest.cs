using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void ShowUIDelegete();

public class DegeleteTest : MonoBehaviour
{
    public ShowUIDelegete showUI;
    private void Start()
    {
        showUI += ShowStartUI;
        showUI += StartUpdateEnemy;
        showUI += StartMovePlayer;
        showUI?.Invoke();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            showUI = ShowEndUI;
            showUI?.Invoke();
        }
    }

    public void ShowStartUI()
    {
        Debug.Log("展示开始的UI");
    }

    public void StartUpdateEnemy()
    {
        Debug.Log("开始生成敌人");
    }

    public void StartMovePlayer()
    {
        Debug.Log("开始玩家移动");
    }

    public void PlayerDeath()
    {
        Debug.Log("角色死亡");
    }

    public void ShowEndUI()
    {
        Debug.Log("展示结束的UI");
    }
}
