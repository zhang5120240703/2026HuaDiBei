using UnityEngine;

public class CircuitBuild : MonoBehaviour
{
    [Header("连线预览")]
    public LineRenderer currentDrawLine;

    [Header("四个电路节点")]
    public CircuitNode node_PowerPos;
    public CircuitNode node_Switch;
    public CircuitNode node_Inductor;
    public CircuitNode node_Capacitor;

    private CircuitNode startNode;

    void Update()
    {
        // 鼠标按下 → 选节点
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                CircuitNode n = hit.collider.GetComponent<CircuitNode>();
                if (n != null)
                {
                    startNode = n;
                    Debug.Log("✅ 选中：" + n.name);
                }
            }
        }

        // 拖动 → 显示线
        if (Input.GetMouseButton(0) && startNode != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 2f));

            currentDrawLine.SetPosition(0, startNode.transform.position);
            currentDrawLine.SetPosition(1, mouseWorld);
        }

        // 鼠标抬起 → 强制连接！
        if (Input.GetMouseButtonUp(0))
        {
            if (startNode == null)
            {
                currentDrawLine.SetPosition(0, Vector3.zero);
                currentDrawLine.SetPosition(1, Vector3.zero);
                return;
            }

            // 最关键：强制检测终点
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                CircuitNode endNode = hit.collider.GetComponent<CircuitNode>();

                if (endNode != null)
                {
                    Debug.Log("🎯 找到终点：" + endNode.name);
                }

                // 只要碰到节点，就一定能连！
                if (endNode != null && endNode != startNode)
                {
                    ConnectTwoNodes(startNode, endNode);
                }
            }

            // 重置
            currentDrawLine.SetPosition(0, Vector3.zero);
            currentDrawLine.SetPosition(1, Vector3.zero);
            startNode = null;
        }
    }

    // 连接两个节点（最简化，绝对不报错）
    void ConnectTwoNodes(CircuitNode a, CircuitNode b)
    {
        if (a == null || b == null) return;

        a.isConnected = true;
        b.isConnected = true;

        Debug.Log("🔗 连接成功：" + a.name + " → " + b.name);

        // 检查是否全部连完
        bool allComplete =
            node_PowerPos != null && node_PowerPos.isConnected &&
            node_Switch != null && node_Switch.isConnected &&
            node_Inductor != null && node_Inductor.isConnected &&
            node_Capacitor != null && node_Capacitor.isConnected;

        if (allComplete)
        {
            Debug.Log("🎉 电路搭建完成！");
            LCOscillation lc = GetComponent<LCOscillation>();
            if (lc != null) lc.OnCircuitComplete();
        }
    }
}