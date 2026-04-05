using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 核心物理模拟类（纯静态工具）
/// 
/// 模块定位：被动数据提供者，仅负责平抛/斜抛运动的三维轨迹点序列计算。
/// 类内部保留一个可被修改的静态字段 GroundY（默认 0），用于临时调整地面高度。
/// 不监听任何事件，不操作任何 GameObject 或 UI。
/// 
/// 功能说明：
///   支持任意水平方向（XZ 平面）的平抛/斜抛运动，
///   输出按时间顺序排列的世界空间三维轨迹点列表。
/// 
/// 落地规则：
///   物体落到全局坐标 y <= GroundY 时视为触地并停止（地面高度可通过 SetGroundHeight 临时修改）。
/// 
/// 数据流：
///   外部调用 SimulateProjectileMotion(...)
///   → 本函数计算，返回 List<Vector3>
///   → 外部将结果写入 SimulationDataBuffer.UpdateTrajectoryData(...) 或自行消费
/// </summary>
public static class PhysicsSimulationCore
{
    // 重力加速度（m/s²），在公式中作为正值使用，方向为 -Y
    private const float Gravity = 9.8f;
    // 全局地面高度（默认 y=0，可通过 SetGroundHeight 调整）
    private static float GroundY = 0f;
    // 轨迹点最大数量上限（防止参数异常或浮点精度问题导致的无限/爆炸循环）
    public const int MaxTrajectoryPoints = 10000;

    /// <summary>
    /// 计算平抛 / 斜抛运动的三维轨迹点序列（核心函数）。
    /// 
    /// 物理公式：
    ///   水平单位向量由 launchDirection 的 XZ 分量归一化得到
    ///   angleRad = launchAngle * Deg2Rad
    ///   vHorizontal = initialVelocity * cos(angleRad)
    ///   vVertical   = initialVelocity * sin(angleRad)
    ///   vH_vec      = horizontalDir * vHorizontal
    ///
    ///   pos(t) = startPosition
    ///            + vH_vec * t
    ///            + Vector3.up * (vVertical * t - 0.5 * g * t^2)
    ///
    /// 终止条件：当轨迹与 y = GroundY 相交时作为落地点（函数会计算精确落地时刻并加入最终点）
    /// </summary>
    public static List<Vector3> SimulateProjectileMotion(
        float initialVelocity,
        float launchAngle,
        Vector3 launchDirection,
        Vector3 startPosition,
        float timeStep,
        float totalTime)
    {
        var trajectoryPoints = new List<Vector3>();

        // 参数合法性校验
        if (initialVelocity <= 0f || timeStep <= 0f || totalTime <= 0f)
        {
            Debug.LogWarning("[PhysicsSimulationCore] 参数非法：initialVelocity、timeStep、totalTime 必须均大于 0。");
            return trajectoryPoints;
        }

        if (startPosition.y <= GroundY)
        {
            Debug.LogWarning($"[PhysicsSimulationCore] 起点高度低于或等于地面（GroundY={GroundY}），无法进行抛体运动。请确保 startPosition.y > GroundY。");
            return trajectoryPoints;
        }

        // 水平方向归一化（忽略 Y 分量，确保在 XZ 平面内）
        Vector3 horizontalDir = new Vector3(launchDirection.x, 0f, launchDirection.z);
        if (horizontalDir.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("[PhysicsSimulationCore] launchDirection 水平分量接近零向量，已自动回退为 Vector3.forward。");
            horizontalDir = Vector3.forward;
        }
        horizontalDir.Normalize();

        // 物理量初始化
        float angleRad = launchAngle * Mathf.Deg2Rad;
        float vHorizontal = initialVelocity * Mathf.Cos(angleRad); // 水平速度标量
        float vVertical = initialVelocity * Mathf.Sin(angleRad);   // 垂直速度标量
        Vector3 vHVec = horizontalDir * vHorizontal;               // 水平速度向量

        int pointCount = 0;
        float startY = startPosition.y;

        // 逐时间步长计算轨迹点
        for (float t = 0f; t <= totalTime + 1e-6f && pointCount < MaxTrajectoryPoints; t += timeStep)
        {
            // 水平位移（匀速直线运动）
            Vector3 horizontalDisp = vHVec * t;

            // 垂直位移（匀加速：向下为负）
            float verticalDisp = vVertical * t - 0.5f * Gravity * t * t;

            Vector3 point = startPosition + horizontalDisp + Vector3.up * verticalDisp;

            // 若已低于地面，计算准确的落地时刻并加入最终落地点
            if (point.y <= GroundY)
            {
                // 解析求解：startY + vVertical * t_hit - 0.5 * g * t_hit^2 = GroundY
                // => -0.5*g*t^2 + v*t + (startY - GroundY) = 0
                // 求正根 t_hit = (v + sqrt(v^2 + 2*g*(startY - GroundY))) / g
                float d = startY - GroundY;
                float disc = vVertical * vVertical + 2f * Gravity * d;

                if (disc >= 0f && Gravity > 0f)
                {
                    float sqrtDisc = Mathf.Sqrt(disc);
                    float tHit = (vVertical + sqrtDisc) / Gravity;

                    // 若数值误差导致 tHit 超过当前 t（极少发生），将其夹在上一个步长范围内
                    float tPrev = Mathf.Max(0f, t - timeStep);
                    if (tHit < tPrev || tHit > t)
                    {
                        // 尝试另一根（数值稳定性保障）
                        float tAlt = (vVertical - sqrtDisc) / Gravity;
                        if (tAlt >= tPrev && tAlt <= t) tHit = tAlt;
                        else tHit = Mathf.Clamp(tHit, tPrev, t);
                    }

                    Vector3 horizontalDispHit = vHVec * tHit;
                    float verticalDispHit = vVertical * tHit - 0.5f * Gravity * tHit * tHit;
                    Vector3 hitPoint = startPosition + horizontalDispHit + Vector3.up * verticalDispHit;
                    // 强制将 y 设置为 GroundY 以消除浮点误差
                    hitPoint.y = GroundY;

                    trajectoryPoints.Add(hitPoint);
                    pointCount++;
                }
                else
                {
                    // 退化情况（理论上不应发生），退回到上一帧并线性插值 y 到 GroundY
                    float tPrev = Mathf.Max(0f, t - timeStep);
                    Vector3 prevHorizontal = vHVec * tPrev;
                    float prevVertical = vVertical * tPrev - 0.5f * Gravity * tPrev * tPrev;
                    Vector3 prevPoint = startPosition + prevHorizontal + Vector3.up * prevVertical;

                    // 线性插值，找到 alpha 使得 y = GroundY
                    float alpha = 0f;
                    float dy = (point.y - prevPoint.y);
                    if (Mathf.Abs(dy) > 1e-6f) alpha = (GroundY - prevPoint.y) / dy;
                    alpha = Mathf.Clamp01(alpha);
                    Vector3 hitPoint = Vector3.Lerp(prevPoint, point, alpha);
                    hitPoint.y = GroundY;
                    trajectoryPoints.Add(hitPoint);
                    pointCount++;
                }

                break;
            }

            trajectoryPoints.Add(point);
            pointCount++;
        }

        if (pointCount >= MaxTrajectoryPoints)
        {
            Debug.LogWarning($"[PhysicsSimulationCore] 轨迹点已达上限 {MaxTrajectoryPoints}，已强制截断。请检查 timeStep/totalTime 参数是否合理。");
        }

        Debug.Log($"[PhysicsSimulationCore] 三维轨迹计算完成。初速度={initialVelocity}m/s  仰角={launchAngle}°  方向={horizontalDir}  起点={startPosition}  地面高度={GroundY}  步长={timeStep}s  最大时长={totalTime}s  → 共 {trajectoryPoints.Count} 个轨迹点。");

        return trajectoryPoints;
    }

    // 常用调用示例（保持不变）
    public static List<Vector3> ClassicProjectile(float initialVelocity, float startY = 1f)
    {
        Vector3 startPos = new Vector3(0f, startY, 0f);
        return SimulateProjectileMotion(initialVelocity, 0f, Vector3.forward, startPos, 0.02f, 5f);
    }

    public static List<Vector3> OptimalAngleProjectile(float initialVelocity, float startY = 1f)
    {
        Vector3 startPos = new Vector3(0f, startY, 0f);
        return SimulateProjectileMotion(initialVelocity, 45f, Vector3.forward, startPos, 0.02f, 5f);
    }

    public static List<Vector3> CustomProjectile(
        float initialVelocity,
        float launchAngle,
        Vector3 launchDirection,
        Vector3 startPosition)
    {
        return SimulateProjectileMotion(initialVelocity, launchAngle, launchDirection, startPosition, 0.02f, 5f);
    }

    /// <summary>
    /// 修改全局地面高度（临时调整）
    /// </summary>
    /// <param name="newGroundY">新的地面 y 坐标</param>
    public static void SetGroundHeight(float newGroundY)
    {
        GroundY = newGroundY;
        Debug.Log($"[PhysicsSimulationCore] 全局地面高度已临时调整为 y = {newGroundY}");
    }
}