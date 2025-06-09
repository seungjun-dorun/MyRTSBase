using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq 사용 시 (우선순위 큐로 대체 권장)

public static class RequestPathForUnit // 클래스 이름 변경
{
    // AStarNode 내부 클래스 (Pathfinder.cs와 동일)
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

    // AStarNodeComparer 내부 클래스 (Pathfinder.cs와 동일)
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
    /// A* 알고리즘을 사용하여 시작점에서 목표점까지의 경로를 찾습니다.
    /// (함수 이름은 FindPath 또는 CalculatePath 등이 더 일반적일 수 있으나, 요청대로 유지)
    /// </summary>
    public static List<Vector3Int> FindPath(Vector3Int startCubeCoord, Vector3Int goalCubeCoord, HexGridSystem gridSystem)
    {
        // Pathfinder.cs의 FindPath 함수 내용 전체를 여기에 그대로 가져옵니다.
        // (이전 답변에서 제공된 A* 로직 전체)
        if (gridSystem == null)
        {
            Debug.LogError("RequestPathForUnit.FindPath: HexGridSystem 참조가 null입니다!");
            return new List<Vector3Int>();
        }

        HexTile startTile, goalTile;
        if (!gridSystem.TryGetTileAt(startCubeCoord, out startTile) || !startTile.isWalkable)
        {
            // Debug.LogWarning($"RequestPathForUnit.FindPath: 시작 지점 {startCubeCoord}가 유효하지 않거나 이동 불가능합니다.");
            return new List<Vector3Int>();
        }
        if (!gridSystem.TryGetTileAt(goalCubeCoord, out goalTile) || !goalTile.isWalkable)
        {
            // Debug.LogWarning($"RequestPathForUnit.FindPath: 목표 지점 {goalCubeCoord}가 유효하지 않거나 이동 불가능합니다.");
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
            AStarNode currentNode = openSet.OrderBy(node => node.FCost).ThenBy(node => node.hCost).FirstOrDefault(); // 우선순위 큐로 교체 권장

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
                        // 이미 OpenSet에 있다면 정보 업데이트 (더 나은 경로이므로)
                        existingNodeInOpenSet.gCost = movementCostToNeighbor;
                        existingNodeInOpenSet.parent = currentNode;
                        // FCost는 자동으로 업데이트됨
                    }
                    else
                    {
                        // OpenSet에 없으면 새로 추가
                        openSet.Add(neighborNode);
                    }
                }
            }
        }
        // Debug.LogWarning($"RequestPathForUnit.FindPath: {startCubeCoord}에서 {goalCubeCoord}까지의 경로를 찾지 못했습니다. (Iterations: {iterations})");
        return new List<Vector3Int>();
    }

    // CalculateHeuristic 함수 (Pathfinder.cs와 동일)
    private static int CalculateHeuristic(Vector3Int current, Vector3Int target, HexGridSystem gridSystem)
    {
        return gridSystem.GetDistance(current, target);
    }

    // RetracePath 함수 (Pathfinder.cs와 동일)
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
            Debug.LogError("RequestPathForUnit.RetracePath: 경로 역추적 중 오류 발생.");
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
