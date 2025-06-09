using System.Collections.Generic;
using UnityEngine;

// 육각 타일 데이터를 저장할 구조체 또는 클래스
public struct HexTile
{
    public Vector3Int cubeCoords;
    public Vector3 worldPosition;
    public bool isWalkable;
    public GameObject tilePrefabInstance;
    public int MovementCost { get; set; }

    public HexTile(Vector3Int cubeCoords, Vector3 worldPosition, bool isWalkable = true, int movementCost = 1)
    {
        this.cubeCoords = cubeCoords;
        this.worldPosition = worldPosition;
        this.isWalkable = isWalkable;
        this.MovementCost = movementCost;
        this.tilePrefabInstance = null;
    }
}

public class HexGridSystem : MonoBehaviour
{
    public int mapWidth;
    public int mapHeight;
    public float hexSize = 1f;
    public enum HexOrientation { PointyTop, FlatTop }
    public HexOrientation orientation = HexOrientation.PointyTop;
    public float defaultYPosition = 0f;

    private Dictionary<Vector3Int, HexTile> _tileData = new Dictionary<Vector3Int, HexTile>();

    private static readonly Vector3Int[] CUBE_DIRECTIONS = new Vector3Int[] {
    new Vector3Int(+1,  0, -1), new Vector3Int(+1, -1,  0), new Vector3Int( 0, -1, +1),
    new Vector3Int(-1,  0, +1), new Vector3Int(-1, +1,  0), new Vector3Int( 0, +1, -1)
    };
    // -> 아래 GetCubeDirections()를 통해 접근하도록 변경

    // 육각 그리드의 6가지 기본 방향 벡터 (큐브 좌표계 기준)
    // 이 순서는 Red Blob Games에서 흔히 사용하는 순서 중 하나이며,
    // GetHexesInRing 및 기타 방향 기반 로직과 일관성을 유지하는 것이 중요합니다.
    // (q, r, s) = (x, y, z) in Red Blob Games' cube coordinate context
    // 순서: 오른쪽-위, 오른쪽, 오른쪽-아래, 왼쪽-아래, 왼쪽, 왼쪽-위 (시계방향 또는 반시계방향)
    // 예시 순서 (Red Blob Games에서 자주 보이는, 약간 다를 수 있음, 프로젝트에 맞게 통일)
    private static readonly Vector3Int[] _cubeDirectionsInternal = new Vector3Int[] {
        new Vector3Int(+1,  0, -1), // 예: East-NorthEast (Pointy Top 기준, q 증가, s 감소)
        new Vector3Int(+1, -1,  0), // 예: East-SouthEast (q 증가, r 감소)
        new Vector3Int( 0, -1, +1), // 예: South (r 감소, s 증가)
        new Vector3Int(-1,  0, +1), // 예: West-SouthWest (q 감소, s 증가)
        new Vector3Int(-1, +1,  0), // 예: West-NorthWest (q 감소, r 증가)
        new Vector3Int( 0, +1, -1)  // 예: North (r 증가, s 감소)
    };

    /// <summary>
    /// 육각 그리드의 표준 큐브 좌표 방향 벡터 배열을 반환합니다.
    /// 이 배열은 외부 (예: FormationUtility)에서 참조할 수 있습니다.
    /// </summary>
    public static Vector3Int[] GetCubeDirections()
    {
        return _cubeDirectionsInternal;
    }

    /// <summary>
    /// 현재 그리드에 생성된 타일의 총 개수를 반환합니다.
    /// </summary>
    public int GetTileDataCount()
    {
        if (_tileData == null) return 0;
        return _tileData.Count;
    }

    /// <summary>
    /// 그리드에 있는 모든 타일 데이터를 컬렉션 형태로 반환합니다.
    /// </summary>
    public ICollection<HexTile> GetAllTiles() 
    {
        if (_tileData == null) return new List<HexTile>();
        return _tileData.Values;
    }

    /// <summary>
    /// 특정 큐브 좌표의 타일이 이동 가능한지 확인하는 public 함수입니다.
    /// </summary>
    public bool IsTileWalkable(Vector3Int cubeCoords) // 다른 곳에서 필요할 수 있어 추가 권장
    {
        if (TryGetTileAt(cubeCoords, out HexTile tile))
        {
            return tile.isWalkable;
        }
        return false;
    }

    void Awake()
    {
        GenerateLogicalGrid();
    }

    // GenerateLogicalGrid, Coordinate Conversions, 대부분의 Grid Operations 함수는
    // 제공해주신 내용과 거의 동일하게 유지됩니다. (UnityEngine.Vector3 명시 제외)
    // Vector3를 사용할 때 UnityEngine.Vector3로 명시해주신 부분은 그대로 두거나,
    // 파일 상단에 using UnityEngine; 가 있으므로 그냥 Vector3로 써도 무방합니다.
    // 여기서는 가독성을 위해 그냥 Vector3로 표기하겠습니다.

    void GenerateLogicalGrid()
    {
        _tileData.Clear();
        // 제공해주신 직사각형 오프셋 기반 맵 생성 로직 사용
        for (int r_offset_row = 0; r_offset_row < mapHeight; r_offset_row++)
        {
            for (int q_offset_col = 0; q_offset_col < mapWidth; q_offset_col++)
            {
                Vector3Int cubeCoords = OffsetToCube(new Vector2Int(q_offset_col, r_offset_row));
                Vector3 worldPos = CubeToWorld(cubeCoords);
                // isWalkable 및 MovementCost는 맵 데이터나 절차적 생성 규칙에 따라 설정
                HexTile tile = new HexTile(cubeCoords, worldPos, true, 1);
                _tileData[cubeCoords] = tile;
            }
        }
        Debug.Log($"Generated {_tileData.Count} hex tiles.");
    }

    #region Coordinate Conversions (제공해주신 내용과 동일하게 유지)
    public Vector3Int OffsetToCube(Vector2Int offsetCoords)
    {
        int q, r, s;
        if (orientation == HexOrientation.PointyTop)
        {
            q = offsetCoords.x - (offsetCoords.y - (offsetCoords.y & 1)) / 2;
            r = offsetCoords.y;
        }
        else
        {
            q = offsetCoords.x;
            r = offsetCoords.y - (offsetCoords.x - (offsetCoords.x & 1)) / 2;
        }
        s = -q - r;
        return new Vector3Int(q, r, s);
    }

    public Vector2Int CubeToOffset(Vector3Int cubeCoords)
    {
        int col, row;
        if (orientation == HexOrientation.PointyTop)
        {
            col = cubeCoords.x + (cubeCoords.y - (cubeCoords.y & 1)) / 2;
            row = cubeCoords.y;
        }
        else
        {
            col = cubeCoords.x;
            row = cubeCoords.y + (cubeCoords.x - (cubeCoords.x & 1)) / 2;
        }
        return new Vector2Int(col, row);
    }

    public Vector3 CubeToWorld(Vector3Int cubeCoords)
    {
        float x, z;
        if (orientation == HexOrientation.PointyTop)
        {
            x = hexSize * (Mathf.Sqrt(3.0f) * cubeCoords.x + Mathf.Sqrt(3.0f) / 2.0f * cubeCoords.y);
            z = hexSize * (3.0f / 2.0f * cubeCoords.y);
        }
        else
        {
            x = hexSize * (3.0f / 2.0f * cubeCoords.x);
            z = hexSize * (Mathf.Sqrt(3.0f) / 2.0f * cubeCoords.x + Mathf.Sqrt(3.0f) * cubeCoords.y);
        }
        return new Vector3(x, defaultYPosition, z);
    }

    public Vector3Int WorldToCube(Vector3 worldPos)
    {
        float q_float, r_float;
        float worldX = worldPos.x;
        float worldZ = worldPos.z;

        if (orientation == HexOrientation.PointyTop)
        {
            q_float = (Mathf.Sqrt(3.0f) / 3.0f * worldX - 1.0f / 3.0f * worldZ) / hexSize;
            r_float = (2.0f / 3.0f * worldZ) / hexSize;
        }
        else
        {
            q_float = (2.0f / 3.0f * worldX) / hexSize;
            r_float = (-1.0f / 3.0f * worldX + Mathf.Sqrt(3.0f) / 3.0f * worldZ) / hexSize;
        }
        float s_float = -q_float - r_float;
        return CubeRound(q_float, r_float, s_float);
    }

    private Vector3Int CubeRound(float fq, float fr, float fs)
    {
        int q = Mathf.RoundToInt(fq);
        int r = Mathf.RoundToInt(fr);
        int s = Mathf.RoundToInt(fs);
        float q_diff = Mathf.Abs(q - fq);
        float r_diff = Mathf.Abs(r - fr);
        float s_diff = Mathf.Abs(s - fs);
        if (q_diff > r_diff && q_diff > s_diff) q = -r - s;
        else if (r_diff > s_diff) r = -q - s;
        else s = -q - r;
        return new Vector3Int(q, r, s);
    }
    #endregion

    #region Grid Operations (GetNeighbor 추가)
    public HexTile GetTileAt(Vector3Int cubeCoords)
    {
        _tileData.TryGetValue(cubeCoords, out HexTile tile);
        return tile; // 찾지 못하면 기본값 HexTile (isWalkable=false 등일 수 있음)
    }

    public bool TryGetTileAt(Vector3Int cubeCoords, out HexTile tile)
    {
        return _tileData.TryGetValue(cubeCoords, out tile);
    }

    public List<HexTile> GetNeighbors(Vector3Int cubeCoords)
    {
        List<HexTile> neighbors = new List<HexTile>();
        Vector3Int[] directions = GetCubeDirections(); // 정적 메서드 사용
        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighborCubeCoords = cubeCoords + directions[i];
            if (TryGetTileAt(neighborCubeCoords, out HexTile neighborTile))
            {
                neighbors.Add(neighborTile);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// 주어진 큐브 좌표에서 특정 방향으로 한 칸 이동한 이웃 큐브 좌표에 해당하는 타일을 반환합니다.
    /// 해당 위치에 타일이 없거나 유효하지 않으면 기본값 HexTile을 반환할 수 있으므로,
    /// 반환된 타일의 유효성을 확인해야 합니다. (또는 TryGetNeighbor)
    /// </summary>
    public HexTile GetNeighborTile(Vector3Int cubeCoords, int directionIndex)
    {
        Vector3Int[] directions = GetCubeDirections();
        if (directionIndex < 0 || directionIndex >= directions.Length)
        {
            Debug.LogError($"Invalid direction index for GetNeighborTile: {directionIndex}. Must be 0-5.");
            return default(HexTile); // 또는 예외 발생
        }
        Vector3Int neighborCoords = cubeCoords + directions[directionIndex];
        TryGetTileAt(neighborCoords, out HexTile neighborTile); // 결과가 있든 없든 neighborTile 반환
        return neighborTile;
    }

    /// <summary>
    /// 특정 큐브 좌표에서 주어진 방향(0-5)의 이웃 타일 정보를 가져오려고 시도합니다.
    /// </summary>
    public bool TryGetNeighbor(Vector3Int cubeCoords, int direction, out HexTile neighborTile)
    {
        neighborTile = default(HexTile);
        if (direction < 0 || direction >= CUBE_DIRECTIONS.Length)
        {
            Debug.LogError($"TryGetNeighbor: 유효하지 않은 방향 값입니다 - {direction}");
            return false;
        }   

        Vector3Int neighborCubeCoords = cubeCoords + CUBE_DIRECTIONS[direction];
        return TryGetTileAt(neighborCubeCoords, out neighborTile);
    }

    /// <summary>
    /// 주어진 큐브 좌표에서 특정 방향으로 한 칸 이동한 이웃 큐브 좌표를 반환합니다.
    /// (FormationUtility에서 필요로 했던 함수)
    /// </summary>
    public Vector3Int GetNeighborCubeCoord(Vector3Int cubeCoords, int directionIndex)
    {
        Vector3Int[] directions = GetCubeDirections();
        if (directionIndex < 0 || directionIndex >= directions.Length)
        {
            Debug.LogError($"Invalid direction index for GetNeighborCubeCoord: {directionIndex}. Must be 0-5.");
            return cubeCoords; // 오류 시 현재 위치 반환 (또는 예외)
        }
        return cubeCoords + directions[directionIndex];
    }


    public int GetDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
    }

    public bool IsValidHex(Vector3Int cubeCoords)
    {
        return _tileData.ContainsKey(cubeCoords);
    }

    // OnDrawGizmos, DrawHexGizmo, HexCornerOffset 함수는 제공해주신 내용과 동일하게 유지
    void OnDrawGizmos()
    {
        if (_tileData == null || _tileData.Count == 0) return;
        foreach (var tileEntry in _tileData)
        {
            HexTile tile = tileEntry.Value;
            Gizmos.color = tile.isWalkable ? Color.white : Color.red;
            DrawHexGizmo(tile.worldPosition, hexSize);
        }
    }
    void DrawHexGizmo(Vector3 center, float size)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 p1_offset = HexCornerOffset(size, i);
            Vector3 p2_offset = HexCornerOffset(size, (i + 1) % 6);
            Gizmos.DrawLine(center + p1_offset, center + p2_offset);
        }
    }
    Vector3 HexCornerOffset(float size, int i)
    {
        float angle_deg = 60 * i;
        if (orientation == HexOrientation.PointyTop) angle_deg -= 30;
        float angle_rad = Mathf.Deg2Rad * angle_deg;
        float cornerX = size * Mathf.Cos(angle_rad);
        float cornerZ = size * Mathf.Sin(angle_rad);
        return new Vector3(cornerX, 0, cornerZ);
    }

    #endregion
}



