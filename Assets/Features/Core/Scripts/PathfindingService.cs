using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System; // Action 사용을 위해
using System.Linq;

public class PathfindingService : MonoBehaviour
{
    #region Singleton
    private static PathfindingService _instance;
    public static PathfindingService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PathfindingService>();
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(nameof(PathfindingService));
                    _instance = singletonObject.AddComponent<PathfindingService>();
                    DontDestroyOnLoad(singletonObject); // 씬 전환 시에도 유지 (선택적)
                    Debug.Log("PathfindingService 인스턴스가 생성되었습니다.");
                }
            }
            return _instance;
        }
    }
    #endregion

    [Tooltip("경로 탐색에 사용할 HexGridSystem 참조입니다.")]
    public HexGridSystem hexGridSystem; // Inspector에서 할당 또는 Awake에서 찾기

    private Queue<PathRequest> _pathRequestQueue = new Queue<PathRequest>();
    private PathRequest _currentPathRequest;
    private bool _isProcessingPath = false;

    [Tooltip("한 프레임에 최대 몇 개의 경로 탐색 요청을 시작할지 결정합니다. (0 이하면 제한 없음, 코루틴 분산 시에는 1이 적절)")]
    public int maxRequestsPerFrame = 1; // 성능에 따라 조절
    private int _requestsProcessedThisFrame = 0;


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("PathfindingService의 다른 인스턴스가 이미 존재합니다. 이 인스턴스를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        // DontDestroyOnLoad(gameObject); // 필요에 따라 주석 해제

        if (hexGridSystem == null)
        {
            hexGridSystem = FindFirstObjectByType<HexGridSystem>(); // 자동으로 찾아보기
            if (hexGridSystem == null)
            {
                Debug.LogError("PathfindingService: HexGridSystem이 할당되지 않았고, 씬에서 찾을 수도 없습니다!");
            }
        }
    }

    void Update()
    {
        _requestsProcessedThisFrame = 0; // 매 프레임 처리량 초기화
        TryProcessNext(); // 큐에 대기 중인 요청 처리 시도
    }

    /// <summary>
    /// 경로 탐색을 요청합니다. 결과는 콜백을 통해 비동기적으로 반환됩니다.
    /// </summary>
    /// <param name="startCoord">시작 큐브 좌표</param>
    /// <param name="targetCoord">목표 큐브 좌표</param>
    /// <param name="callback">경로 탐색 완료 시 호출될 콜백 함수 (경로 리스트, 성공 여부)</param>
    public void RequestPath(Vector3Int startCoord, Vector3Int targetCoord, Action<List<Vector3Int>, bool> callback)
    {
        if (hexGridSystem == null)
        {
            Debug.LogError("PathfindingService: HexGridSystem이 설정되지 않아 경로 탐색을 수행할 수 없습니다.");
            callback?.Invoke(new List<Vector3Int>(), false); // 즉시 실패 콜백
            return;
        }

        PathRequest newRequest = new PathRequest(startCoord, targetCoord, callback);
        _pathRequestQueue.Enqueue(newRequest);
        // Debug.Log($"경로 요청 추가: {startCoord} -> {targetCoord}. 대기열: {_pathRequestQueue.Count}");
        TryProcessNext();
    }

    private void TryProcessNext()
    {
        // 현재 처리 중인 경로가 없고, 큐에 요청이 있으며, 프레임당 처리 한도를 넘지 않았을 때
        if (!_isProcessingPath && _pathRequestQueue.Count > 0 && (maxRequestsPerFrame <=0 || _requestsProcessedThisFrame < maxRequestsPerFrame) )
        {
            _currentPathRequest = _pathRequestQueue.Dequeue();
            _isProcessingPath = true;
            _requestsProcessedThisFrame++;
            // Debug.Log($"경로 처리 시작: {_currentPathRequest.PathStart} -> {_currentPathRequest.PathEnd}");
            StartCoroutine(FindPathCoroutine(_currentPathRequest.PathStart, _currentPathRequest.PathEnd));
        }
    }

    /// <summary>
    /// A* 알고리즘을 사용하여 경로를 찾는 코루틴입니다.
    /// </summary>
    private IEnumerator FindPathCoroutine(Vector3Int startCoord, Vector3Int targetCoord)
    {
        List<Vector3Int> waypoints = new List<Vector3Int>();
        bool pathSuccess = false;

        // A* 알고리즘 노드 클래스 (PathNode)
        PathNode startNode = new PathNode(startCoord, 0, CalculateHeuristic(startCoord, targetCoord), null);
        PathNode targetNode = new PathNode(targetCoord, 0, 0, null); // 목표 노드는 gCost, hCost가 크게 중요하지 않음

        // 열린 목록(Open List)과 닫힌 목록(Closed List)
        // 열린 목록은 우선순위 큐로 구현하면 더 효율적이지만, 여기서는 간단히 List와 Sort 사용
        List<PathNode> openSet = new List<PathNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>(); // 좌표만 저장하여 중복 체크

        openSet.Add(startNode);

        // A* 알고리즘 루프 (안전장치로 반복 횟수 제한 가능)
        int iterations = 0;
        int maxIterations = 1000; // 맵 크기나 복잡도에 따라 조절 (무한 루프 방지)

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // F 코스트가 가장 낮은 노드 선택 (우선순위 큐가 아니므로 정렬 후 첫 번째 요소 선택)
            PathNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || (openSet[i].FCost == currentNode.FCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.Position);

            // 목표 지점 도달 시
            if (currentNode.Position == targetNode.Position)
            {
                // Debug.Log($"경로 찾음! {startCoord} -> {targetCoord} (반복: {iterations})");
                pathSuccess = true;
                waypoints = RetracePath(startNode.Position, currentNode); // 시작점은 retrace에서 제외될 수 있으므로 명시적 전달
                break;
            }

            // 이웃 노드 탐색 (HexGridSystem 사용)
            // hexGridSystem.GetNeighbors가 List<HexTile>을 반환하므로, 받는 변수 타입을 HexTile로 변경
            foreach (HexTile neighborTileObject in hexGridSystem.GetNeighbors(currentNode.Position))
            {
                // HexTile 객체에서 실제 좌표를 가져옵니다.
                // HexTile 클래스에 Position 또는 Coordinates 같은 Vector3Int 속성이 있어야 합니다.
                // 예시: public Vector3Int CubeCoords { get; private set; }
                Vector3Int neighborCoord = neighborTileObject.cubeCoords; // 또는 neighborTileObject.Position 등 실제 속성명 사용

                // closedSet은 좌표 기반으로 유지되므로 neighborCoord 사용
                if (closedSet.Contains(neighborCoord))
                {
                    continue;
                }

                // IsValidHex는 좌표로 확인하는 것이 일반적이나, GetNeighbors에서 이미 유효한 타일만 반환하므로
                // 여기서는 neighborTileObject.isWalkable만 확인해도 될 수 있습니다.
                // 다만, GetNeighbors가 맵 경계를 넘어선 타일 객체를 생성하지 않는다는 보장이 있어야 합니다.
                // 안전하게는 IsValidHex(neighborCoord)도 호출하는 것이 좋습니다.
                if (!hexGridSystem.IsValidHex(neighborCoord) || // 만약을 위해 유효성 검사 (GetNeighbors에서 이미 처리되었을 수 있음)
                    !neighborTileObject.isWalkable) // HexTile 객체에서 직접 walkable 정보 사용
                {
                    continue;
                }

                // 이동 비용 계산 (여기서는 모든 타일 비용 1로 가정, 지형 따라 가중치 부여 가능)
                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode.Position, neighborCoord); // GetDistance는 좌표 기반
                PathNode neighborNodeInOpenSet = openSet.Find(node => node.Position == neighborCoord);

                if (neighborNodeInOpenSet == null || newMovementCostToNeighbor < neighborNodeInOpenSet.gCost)
                {
                    int hCost = CalculateHeuristic(neighborCoord, targetCoord);
                    PathNode neighborNode = new PathNode(neighborCoord, newMovementCostToNeighbor, hCost, currentNode);

                    if (neighborNodeInOpenSet == null)
                    {
                        openSet.Add(neighborNode);
                    }
                    else
                    {
                        neighborNodeInOpenSet.gCost = newMovementCostToNeighbor;
                        neighborNodeInOpenSet.Parent = currentNode;
                    }
                }
            }

            // 코루틴의 장점: 복잡한 계산 중간에 프레임 양보 가능 (필요하다면)
            // if (iterations % 50 == 0) yield return null; // 예: 50번 반복마다 한 프레임 쉬기 (성능에 따라 조절)
            // 다만, 이 방식은 결정론적 게임에서는 주의해야 함. 틱 기반으로 처리하려면 yield를 사용하지 않거나,
            // 틱 내에서 완료되도록 알고리즘을 최적화하거나, Job System을 사용해야 함.
            // 여기서는 단일 코루틴이 한 번에 경로를 다 찾고 결과를 반환한다고 가정.
        }
        if (iterations >= maxIterations) Debug.LogWarning($"경로 탐색 최대 반복 도달: {startCoord} -> {targetCoord}");
        if (!pathSuccess) Debug.LogWarning($"경로 찾기 실패: {startCoord} -> {targetCoord}");

        yield return null; // 최소 한 프레임 대기 (결과 처리를 다음 프레임으로 넘겨 부하 분산)

        // 결과 콜백 호출 및 다음 요청 처리 준비
        _currentPathRequest.Callback?.Invoke(waypoints, pathSuccess);
        _isProcessingPath = false;
        // TryProcessNext(); // Update에서 호출되므로 여기서 또 호출할 필요는 없음.
    }


    /// <summary>
    /// 최종 노드로부터 부모를 따라가며 경로를 역추적합니다.
    /// </summary>
    private List<Vector3Int> RetracePath(Vector3Int startCoord, PathNode endNode)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        PathNode currentNode = endNode;

        while (currentNode != null && currentNode.Position != startCoord) // 시작 노드는 경로에 포함하지 않거나, 포함할 수 있음
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }
        // 시작 지점을 경로의 첫 번째로 추가 (유닛의 현재 위치이므로 보통은 필요)
        // 경로 탐색 시 startNode를 명시적으로 포함하지 않았다면 여기서 추가
         if(path.Count == 0 && endNode.Position == startCoord) // 시작과 끝이 같은 경우
         {
             path.Add(startCoord);
         }
         else if (path.Count > 0 && path.Last() != startCoord) // 경로가 있는데 시작점이 포함 안된 경우
         {
             // 경로가 startCoord 에서부터 endNode 까지 왔으므로, 역추적하면 endNode 부터 startNode 직전까지.
             // 실제 유닛 이동 시에는 현재 위치(startCoord)는 이미 알고 있으므로, 다음 목적지부터 경로에 포함.
             // 또는 SetMoveCommand에서 경로의 첫 노드가 현재위치인지 확인하고 조정.
         }


        path.Reverse(); // 목표 지점 -> 시작 지점 순이므로 반전하여 시작 -> 목표 순으로 만듦
        return path;
    }

    /// <summary>
    /// 두 육각 타일 간의 휴리스틱 비용(추정 거리)을 계산합니다. (맨해튼 거리 또는 유클리드 거리 변형)
    /// 큐브 좌표계에서의 거리를 사용.
    /// </summary>
    private int CalculateHeuristic(Vector3Int a, Vector3Int b)
    {
        // 육각 그리드에서의 맨해튼 거리 (큐브 좌표계 기준)
        // ( |a.x - b.x| + |a.y - b.y| + |a.z - b.z| ) / 2
        // HexGridSystem에 GetDistance(a,b) 함수가 있다면 그것을 사용하는 것이 좋음.
        return hexGridSystem.GetDistance(a, b); // HexGridSystem에 구현된 거리 계산 함수 사용 가정
    }

    /// <summary>
    /// 두 인접 타일 간의 실제 이동 비용을 반환합니다.
    /// 기본값은 1이지만, 지형에 따라 비용이 달라질 수 있습니다 (예: 숲, 언덕).
    /// </summary>
    private int GetDistance(Vector3Int nodeA, Vector3Int nodeB)
    {
        // 여기서는 모든 인접 타일 이동 비용을 1로 가정
        // Tile tileB = hexGridSystem.GetTileAt(nodeB);
        // return tileB.movementCost; // 타일마다 이동 비용이 다를 경우
        return 1; // 대부분의 육각 그리드에서 인접 타일 간 거리는 1
    }

    /// <summary>
    /// 경로 탐색 요청 정보를 담는 구조체입니다.
    /// </summary>
    private struct PathRequest
    {
        public Vector3Int PathStart;
        public Vector3Int PathEnd;
        public Action<List<Vector3Int>, bool> Callback;

        public PathRequest(Vector3Int start, Vector3Int end, Action<List<Vector3Int>, bool> callback)
        {
            PathStart = start;
            PathEnd = end;
            Callback = callback;
        }
    }

    /// <summary>
    /// A* 알고리즘에 사용될 노드 클래스입니다.
    /// </summary>
    private class PathNode
    {
        public Vector3Int Position { get; }
        public int gCost; // 시작점으로부터의 실제 비용
        public int hCost; // 목표점까지의 추정 비용 (휴리스틱)
        public int FCost => gCost + hCost; // 총 예상 비용
        public PathNode Parent { get; set; }

        public PathNode(Vector3Int position, int gCost, int hCost, PathNode parent)
        {
            Position = position;
            this.gCost = gCost;
            this.hCost = hCost;
            Parent = parent;
        }
    }
}
