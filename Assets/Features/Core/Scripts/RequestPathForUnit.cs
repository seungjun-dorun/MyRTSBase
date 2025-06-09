using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq ��� �� (�켱���� ť�� ��ü ����)

public static class RequestPathForUnit // Ŭ���� �̸� ����
{
    // AStarNode ���� Ŭ���� (Pathfinder.cs�� ����)
    private class AStarNode
    {
        public Vector3Int position;
        public AStarNode parent;
        public int gCost;
        public int hCost;
        public int FCost => gCost + hCost;

        public AStarNode(Vector3Int pos, AStarNode parentNode, int g, int h)
        {
            position = pos;
            parent = parentNode;
            gCost = g;
            hCost = h;
        }
    }

    // AStarNodeComparer ���� Ŭ���� (Pathfinder.cs�� ����)
    private class AStarNodeComparer : IComparer<AStarNode>
    {
        public int Compare(AStarNode x, AStarNode y)
        {
            int compare = x.FCost.CompareTo(y.FCost);
            if (compare == 0)
            {
                compare = x.hCost.CompareTo(y.hCost);
            }
            return compare;
        }
    }

    /// <summary>
    /// A* �˰����� ����Ͽ� ���������� ��ǥ�������� ��θ� ã���ϴ�.
    /// (�Լ� �̸��� FindPath �Ǵ� CalculatePath ���� �� �Ϲ����� �� ������, ��û��� ����)
    /// </summary>
    public static List<Vector3Int> FindPath(Vector3Int startCubeCoord, Vector3Int goalCubeCoord, HexGridSystem gridSystem)
    {
        // Pathfinder.cs�� FindPath �Լ� ���� ��ü�� ���⿡ �״�� �����ɴϴ�.
        // (���� �亯���� ������ A* ���� ��ü)
        if (gridSystem == null)
        {
            Debug.LogError("RequestPathForUnit.FindPath: HexGridSystem ������ null�Դϴ�!");
            return new List<Vector3Int>();
        }

        HexTile startTile, goalTile;
        if (!gridSystem.TryGetTileAt(startCubeCoord, out startTile) || !startTile.isWalkable)
        {
            // Debug.LogWarning($"RequestPathForUnit.FindPath: ���� ���� {startCubeCoord}�� ��ȿ���� �ʰų� �̵� �Ұ����մϴ�.");
            return new List<Vector3Int>();
        }
        if (!gridSystem.TryGetTileAt(goalCubeCoord, out goalTile) || !goalTile.isWalkable)
        {
            // Debug.LogWarning($"RequestPathForUnit.FindPath: ��ǥ ���� {goalCubeCoord}�� ��ȿ���� �ʰų� �̵� �Ұ����մϴ�.");
            return new List<Vector3Int>();
        }

        if (startCubeCoord == goalCubeCoord)
        {
            return new List<Vector3Int> { goalCubeCoord };
        }

        List<AStarNode> openSet = new List<AStarNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, int> gCosts = new Dictionary<Vector3Int, int>();

        AStarNode startNode = new AStarNode(startCubeCoord, null, 0, CalculateHeuristic(startCubeCoord, goalCubeCoord, gridSystem));
        openSet.Add(startNode);
        gCosts[startCubeCoord] = 0;

        int maxIterations = gridSystem.GetTileDataCount() * 2;
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            AStarNode currentNode = openSet.OrderBy(node => node.FCost).ThenBy(node => node.hCost).FirstOrDefault(); // �켱���� ť�� ��ü ����

            if (currentNode == null) break;

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.position);

            if (currentNode.position == goalCubeCoord)
            {
                return RetracePath(startNode, currentNode);
            }

            foreach (HexTile neighborTile in gridSystem.GetNeighbors(currentNode.position))
            {
                Vector3Int neighborPos = neighborTile.cubeCoords;
                if (!neighborTile.isWalkable || closedSet.Contains(neighborPos))
                {
                    continue;
                }

                int movementCostToNeighbor = currentNode.gCost + neighborTile.MovementCost;
                int knownGCost = gCosts.ContainsKey(neighborPos) ? gCosts[neighborPos] : int.MaxValue;

                if (movementCostToNeighbor < knownGCost)
                {
                    gCosts[neighborPos] = movementCostToNeighbor;
                    int hCost = CalculateHeuristic(neighborPos, goalCubeCoord, gridSystem);
                    AStarNode neighborNode = new AStarNode(neighborPos, currentNode, movementCostToNeighbor, hCost);

                    AStarNode existingNodeInOpenSet = openSet.FirstOrDefault(n => n.position == neighborPos);
                    if (existingNodeInOpenSet != null)
                    {
                        // �̹� OpenSet�� �ִٸ� ���� ������Ʈ (�� ���� ����̹Ƿ�)
                        existingNodeInOpenSet.gCost = movementCostToNeighbor;
                        existingNodeInOpenSet.parent = currentNode;
                        // FCost�� �ڵ����� ������Ʈ��
                    }
                    else
                    {
                        // OpenSet�� ������ ���� �߰�
                        openSet.Add(neighborNode);
                    }
                }
            }
        }
        // Debug.LogWarning($"RequestPathForUnit.FindPath: {startCubeCoord}���� {goalCubeCoord}������ ��θ� ã�� ���߽��ϴ�. (Iterations: {iterations})");
        return new List<Vector3Int>();
    }

    // CalculateHeuristic �Լ� (Pathfinder.cs�� ����)
    private static int CalculateHeuristic(Vector3Int current, Vector3Int target, HexGridSystem gridSystem)
    {
        return gridSystem.GetDistance(current, target);
    }

    // RetracePath �Լ� (Pathfinder.cs�� ����)
    private static List<Vector3Int> RetracePath(AStarNode startNode, AStarNode endNode)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        AStarNode currentNode = endNode;
        int pathSafetyBreak = 1000;
        int currentPathLength = 0;

        while (currentNode != null && currentPathLength < pathSafetyBreak)
        {
            path.Add(currentNode.position);
            if (currentNode.position == startNode.position) break;
            currentNode = currentNode.parent;
            currentPathLength++;
        }

        if (currentNode == null || currentNode.position != startNode.position)
        {
            Debug.LogError("RequestPathForUnit.RetracePath: ��� ������ �� ���� �߻�.");
            return new List<Vector3Int>();
        }
        path.Reverse();
        if (path.Count > 0 && path[0] == startNode.position)
        {
            path.RemoveAt(0);
        }
        return path;
    }
}
