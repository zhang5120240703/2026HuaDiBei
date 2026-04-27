using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本次实验流程的临时数据存储器
/// 生命周期：从进入实验场景到返回 MainMenu
/// 退出场景或下次点"开始"时清空
/// </summary>
public static class SessionDataStore
{
    private static List<SessionRecord> records = new List<SessionRecord>();

    public static List<SessionRecord> Records => records;
    public static int Count => records.Count;
    public static bool HasData => records.Count > 0;

    /// <summary>追加一次实验数据</summary>
    public static void Add(float xDist, float yDist, float totalDist, int pointCount, float velocity, float angle)
    {
        records.Add(new SessionRecord
        {
            xDistance = xDist,
            yDistance = yDist,
            totalDistance = totalDist,
            pointCount = pointCount,
            velocity = velocity,
            angle = angle
        });
        Debug.Log($"[SessionDataStore] 追加数据，当前总数={records.Count}");
    }

    /// <summary>清空所有数据</summary>
    public static void Clear()
    {
        records.Clear();
        Debug.Log("[SessionDataStore] 已清空");
    }

    public struct SessionRecord
    {
        public float xDistance;
        public float yDistance;
        public float totalDistance;
        public int pointCount;
        public float velocity;
        public float angle;
    }
}