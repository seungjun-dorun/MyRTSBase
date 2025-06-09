using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System; // Action ����� ����
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
                    DontDestroyOnLoad(singletonObject); // �� ��ȯ �ÿ��� ���� (������)
                    Debug.Log("PathfindingService �ν��Ͻ��� �����Ǿ����ϴ�.");
                }
            }
            return _instance;
        }
    }
    #endregion

    [Tooltip("��� Ž���� ����� HexGridSystem �����Դϴ�.")]
    public HexGridSystem hexGridSystem; // Inspector���� �Ҵ� �Ǵ� Awake���� ã��

    private Queue<PathRequest> _pathRequestQueue = new Queue<PathRequest>();
    private PathRequest _currentPathRequest;
    private bool _isProcessingPath = false;

    [Tooltip("�� �����ӿ� �ִ� �� ���� ��� Ž�� ��û�� �������� �����մϴ�. (0 ���ϸ� ���� ����, �ڷ�ƾ �л� �ÿ��� 1�� ����)")]
    public int maxRequestsPerFrame = 1; // ���ɿ� ���� ����
    private int _requestsProcessedThisFrame = 0;


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("PathfindingService�� �ٸ� �ν��Ͻ��� �̹� �����մϴ�. �� �ν��Ͻ��� �ı��մϴ�.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        // DontDestroyOnLoad(gameObject); // �ʿ信 ���� �ּ� ����

        if (hexGridSystem == null)
        {
            hexGridSystem = FindFirstObjectByType<HexGridSystem>(); // �ڵ����� ã�ƺ���
            if (hexGridSystem == null)
            {
                Debug.LogError("PathfindingService: HexGridSystem�� �Ҵ���� �ʾҰ�, ������ ã�� ���� �����ϴ�!");
            }
        }
    }

    void Update()
    {
        _requestsProcessedThisFrame = 0; // �� ������ ó���� �ʱ�ȭ
        TryProcessNext(); // ť�� ��� ���� ��û ó�� �õ�
    }

    /// <summary>
    /// ��� Ž���� ��û�մϴ�. ����� �ݹ��� ���� �񵿱������� ��ȯ�˴ϴ�.
    /// </summary>
    /// <param name="startCoord">���� ť�� ��ǥ</param>
    /// <param name="targetCoord">��ǥ ť�� ��ǥ</param>
    /// <param name="callback">��� Ž�� �Ϸ� �� ȣ��� �ݹ� �Լ� (��� ����Ʈ, ���� ����)</param>
    public void RequestPath(Vector3Int startCoord, Vector3Int targetCoord, Action<List<Vector3Int>, bool> callback)
    {
        if (hexGridSystem == null)
        {
            Debug.LogError("PathfindingService: HexGridSystem�� �������� �ʾ� ��� Ž���� ������ �� �����ϴ�.");
            callback?.Invoke(new List<Vector3Int>(), false); // ��� ���� �ݹ�
            return;
        }

        PathRequest newRequest = new PathRequest(startCoord, targetCoord, callback);
        _pathRequestQueue.Enqueue(newRequest);
        // Debug.Log($"��� ��û �߰�: {startCoord} -> {targetCoord}. ��⿭: {_pathRequestQueue.Count}");
        TryProcessNext();
    }

    private void TryProcessNext()
    {
        // ���� ó�� ���� ��ΰ� ����, ť�� ��û�� ������, �����Ӵ� ó�� �ѵ��� ���� �ʾ��� ��
        if (!_isProcessingPath && _pathRequestQueue.Count > 0 && (maxRequestsPerFrame <=0 || _requestsProcessedThisFrame < maxRequestsPerFrame) )
        {
            _currentPathRequest = _pathRequestQueue.Dequeue();
            _isProcessingPath = true;
            _requestsProcessedThisFrame++;
            // Debug.Log($"��� ó�� ����: {_currentPathRequest.PathStart} -> {_currentPathRequest.PathEnd}");
            StartCoroutine(FindPathCoroutine(_currentPathRequest.PathStart, _currentPathRequest.PathEnd));
        }
    }

    /// <summary>
    /// A* �˰����� ����Ͽ� ��θ� ã�� �ڷ�ƾ�Դϴ�.
    /// </summary>
    private IEnumerator FindPathCoroutine(Vector3Int startCoord, Vector3Int targetCoord)
    {
        List<Vector3Int> waypoints = new List<Vector3Int>();
        bool pathSuccess = false;

        // A* �˰��� ��� Ŭ���� (PathNode)
        PathNode startNode = new PathNode(startCoord, 0, CalculateHeuristic(startCoord, targetCoord), null);
        PathNode targetNode = new PathNode(targetCoord, 0, 0, null); // ��ǥ ���� gCost, hCost�� ũ�� �߿����� ����

        // ���� ���(Open List)�� ���� ���(Closed List)
        // ���� ����� �켱���� ť�� �����ϸ� �� ȿ����������, ���⼭�� ������ List�� Sort ���
        List<PathNode> openSet = new List<PathNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>(); // ��ǥ�� �����Ͽ� �ߺ� üũ

        openSet.Add(startNode);

        // A* �˰��� ���� (������ġ�� �ݺ� Ƚ�� ���� ����)
        int iterations = 0;
        int maxIterations = 1000; // �� ũ�⳪ ���⵵�� ���� ���� (���� ���� ����)

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // F �ڽ�Ʈ�� ���� ���� ��� ���� (�켱���� ť�� �ƴϹǷ� ���� �� ù ��° ��� ����)
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

            // ��ǥ ���� ���� ��
            if (currentNode.Position == targetNode.Position)
            {
                // Debug.Log($"��� ã��! {startCoord} -> {targetCoord} (�ݺ�: {iterations})");
                pathSuccess = true;
                waypoints = RetracePath(startNode.Position, currentNode); // �������� retrace���� ���ܵ� �� �����Ƿ� ����� ����
                break;
            }

            // �̿� ��� Ž�� (HexGridSystem ���)
            // hexGridSystem.GetNeighbors�� List<HexTile>�� ��ȯ�ϹǷ�, �޴� ���� Ÿ���� HexTile�� ����
            foreach (HexTile neighborTileObject in hexGridSystem.GetNeighbors(currentNode.Position))
            {
                // HexTile ��ü���� ���� ��ǥ�� �����ɴϴ�.
                // HexTile Ŭ������ Position �Ǵ� Coordinates ���� Vector3Int �Ӽ��� �־�� �մϴ�.
                // ����: public Vector3Int CubeCoords { get; private set; }
                Vector3Int neighborCoord = neighborTileObject.cubeCoords; // �Ǵ� neighborTileObject.Position �� ���� �Ӽ��� ���

                // closedSet�� ��ǥ ������� �����ǹǷ� neighborCoord ���
                if (closedSet.Contains(neighborCoord))
                {
                    continue;
                }

                // IsValidHex�� ��ǥ�� Ȯ���ϴ� ���� �Ϲ����̳�, GetNeighbors���� �̹� ��ȿ�� Ÿ�ϸ� ��ȯ�ϹǷ�
                // ���⼭�� neighborTileObject.isWalkable�� Ȯ���ص� �� �� �ֽ��ϴ�.
                // �ٸ�, GetNeighbors�� �� ��踦 �Ѿ Ÿ�� ��ü�� �������� �ʴ´ٴ� ������ �־�� �մϴ�.
                // �����ϰԴ� IsValidHex(neighborCoord)�� ȣ���ϴ� ���� �����ϴ�.
                if (!hexGridSystem.IsValidHex(neighborCoord) || // ������ ���� ��ȿ�� �˻� (GetNeighbors���� �̹� ó���Ǿ��� �� ����)
                    !neighborTileObject.isWalkable) // HexTile ��ü���� ���� walkable ���� ���
                {
                    continue;
                }

                // �̵� ��� ��� (���⼭�� ��� Ÿ�� ��� 1�� ����, ���� ���� ����ġ �ο� ����)
                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode.Position, neighborCoord); // GetDistance�� ��ǥ ���
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

            // �ڷ�ƾ�� ����: ������ ��� �߰��� ������ �纸 ���� (�ʿ��ϴٸ�)
            // if (iterations % 50 == 0) yield return null; // ��: 50�� �ݺ����� �� ������ ���� (���ɿ� ���� ����)
            // �ٸ�, �� ����� �������� ���ӿ����� �����ؾ� ��. ƽ ������� ó���Ϸ��� yield�� ������� �ʰų�,
            // ƽ ������ �Ϸ�ǵ��� �˰����� ����ȭ�ϰų�, Job System�� ����ؾ� ��.
            // ���⼭�� ���� �ڷ�ƾ�� �� ���� ��θ� �� ã�� ����� ��ȯ�Ѵٰ� ����.
        }
        if (iterations >= maxIterations) Debug.LogWarning($"��� Ž�� �ִ� �ݺ� ����: {startCoord} -> {targetCoord}");
        if (!pathSuccess) Debug.LogWarning($"��� ã�� ����: {startCoord} -> {targetCoord}");

        yield return null; // �ּ� �� ������ ��� (��� ó���� ���� ���������� �Ѱ� ���� �л�)

        // ��� �ݹ� ȣ�� �� ���� ��û ó�� �غ�
        _currentPathRequest.Callback?.Invoke(waypoints, pathSuccess);
        _isProcessingPath = false;
        // TryProcessNext(); // Update���� ȣ��ǹǷ� ���⼭ �� ȣ���� �ʿ�� ����.
    }


    /// <summary>
    /// ���� ���κ��� �θ� ���󰡸� ��θ� �������մϴ�.
    /// </summary>
    private List<Vector3Int> RetracePath(Vector3Int startCoord, PathNode endNode)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        PathNode currentNode = endNode;

        while (currentNode != null && currentNode.Position != startCoord) // ���� ���� ��ο� �������� �ʰų�, ������ �� ����
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }
        // ���� ������ ����� ù ��°�� �߰� (������ ���� ��ġ�̹Ƿ� ������ �ʿ�)
        // ��� Ž�� �� startNode�� ��������� �������� �ʾҴٸ� ���⼭ �߰�
         if(path.Count == 0 && endNode.Position == startCoord) // ���۰� ���� ���� ���
         {
             path.Add(startCoord);
         }
         else if (path.Count > 0 && path.Last() != startCoord) // ��ΰ� �ִµ� �������� ���� �ȵ� ���
         {
             // ��ΰ� startCoord �������� endNode ���� �����Ƿ�, �������ϸ� endNode ���� startNode ��������.
             // ���� ���� �̵� �ÿ��� ���� ��ġ(startCoord)�� �̹� �˰� �����Ƿ�, ���� ���������� ��ο� ����.
             // �Ǵ� SetMoveCommand���� ����� ù ��尡 ������ġ���� Ȯ���ϰ� ����.
         }


        path.Reverse(); // ��ǥ ���� -> ���� ���� ���̹Ƿ� �����Ͽ� ���� -> ��ǥ ������ ����
        return path;
    }

    /// <summary>
    /// �� ���� Ÿ�� ���� �޸���ƽ ���(���� �Ÿ�)�� ����մϴ�. (����ư �Ÿ� �Ǵ� ��Ŭ���� �Ÿ� ����)
    /// ť�� ��ǥ�迡���� �Ÿ��� ���.
    /// </summary>
    private int CalculateHeuristic(Vector3Int a, Vector3Int b)
    {
        // ���� �׸��忡���� ����ư �Ÿ� (ť�� ��ǥ�� ����)
        // ( |a.x - b.x| + |a.y - b.y| + |a.z - b.z| ) / 2
        // HexGridSystem�� GetDistance(a,b) �Լ��� �ִٸ� �װ��� ����ϴ� ���� ����.
        return hexGridSystem.GetDistance(a, b); // HexGridSystem�� ������ �Ÿ� ��� �Լ� ��� ����
    }

    /// <summary>
    /// �� ���� Ÿ�� ���� ���� �̵� ����� ��ȯ�մϴ�.
    /// �⺻���� 1������, ������ ���� ����� �޶��� �� �ֽ��ϴ� (��: ��, ���).
    /// </summary>
    private int GetDistance(Vector3Int nodeA, Vector3Int nodeB)
    {
        // ���⼭�� ��� ���� Ÿ�� �̵� ����� 1�� ����
        // Tile tileB = hexGridSystem.GetTileAt(nodeB);
        // return tileB.movementCost; // Ÿ�ϸ��� �̵� ����� �ٸ� ���
        return 1; // ��κ��� ���� �׸��忡�� ���� Ÿ�� �� �Ÿ��� 1
    }

    /// <summary>
    /// ��� Ž�� ��û ������ ��� ����ü�Դϴ�.
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
    /// A* �˰��� ���� ��� Ŭ�����Դϴ�.
    /// </summary>
    private class PathNode
    {
        public Vector3Int Position { get; }
        public int gCost; // ���������κ����� ���� ���
        public int hCost; // ��ǥ�������� ���� ��� (�޸���ƽ)
        public int FCost => gCost + hCost; // �� ���� ���
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
