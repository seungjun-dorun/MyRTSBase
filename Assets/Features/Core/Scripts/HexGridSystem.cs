using System.Collections.Generic;
using UnityEngine;

// ���� Ÿ�� �����͸� ������ ����ü �Ǵ� Ŭ����
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
    // -> �Ʒ� GetCubeDirections()�� ���� �����ϵ��� ����

    // ���� �׸����� 6���� �⺻ ���� ���� (ť�� ��ǥ�� ����)
    // �� ������ Red Blob Games���� ���� ����ϴ� ���� �� �ϳ��̸�,
    // GetHexesInRing �� ��Ÿ ���� ��� ������ �ϰ����� �����ϴ� ���� �߿��մϴ�.
    // (q, r, s) = (x, y, z) in Red Blob Games' cube coordinate context
    // ����: ������-��, ������, ������-�Ʒ�, ����-�Ʒ�, ����, ����-�� (�ð���� �Ǵ� �ݽð����)
    // ���� ���� (Red Blob Games���� ���� ���̴�, �ణ �ٸ� �� ����, ������Ʈ�� �°� ����)
    private static readonly Vector3Int[] _cubeDirectionsInternal = new Vector3Int[] {
        new Vector3Int(+1,  0, -1), // ��: East-NorthEast (Pointy Top ����, q ����, s ����)
        new Vector3Int(+1, -1,  0), // ��: East-SouthEast (q ����, r ����)
        new Vector3Int( 0, -1, +1), // ��: South (r ����, s ����)
        new Vector3Int(-1,  0, +1), // ��: West-SouthWest (q ����, s ����)
        new Vector3Int(-1, +1,  0), // ��: West-NorthWest (q ����, r ����)
        new Vector3Int( 0, +1, -1)  // ��: North (r ����, s ����)
    };

    /// <summary>
    /// ���� �׸����� ǥ�� ť�� ��ǥ ���� ���� �迭�� ��ȯ�մϴ�.
    /// �� �迭�� �ܺ� (��: FormationUtility)���� ������ �� �ֽ��ϴ�.
    /// </summary>
    public static Vector3Int[] GetCubeDirections()
    {
        return _cubeDirectionsInternal;
    }

    /// <summary>
    /// ���� �׸��忡 ������ Ÿ���� �� ������ ��ȯ�մϴ�.
    /// </summary>
    public int GetTileDataCount()
    {
        if (_tileData == null) return 0;
        return _tileData.Count;
    }

    /// <summary>
    /// �׸��忡 �ִ� ��� Ÿ�� �����͸� �÷��� ���·� ��ȯ�մϴ�.
    /// </summary>
    public ICollection<HexTile> GetAllTiles() 
    {
        if (_tileData == null) return new List<HexTile>();
        return _tileData.Values;
    }

    /// <summary>
    /// Ư�� ť�� ��ǥ�� Ÿ���� �̵� �������� Ȯ���ϴ� public �Լ��Դϴ�.
    /// </summary>
    public bool IsTileWalkable(Vector3Int cubeCoords) // �ٸ� ������ �ʿ��� �� �־� �߰� ����
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

    // GenerateLogicalGrid, Coordinate Conversions, ��κ��� Grid Operations �Լ���
    // �������ֽ� ����� ���� �����ϰ� �����˴ϴ�. (UnityEngine.Vector3 ��� ����)
    // Vector3�� ����� �� UnityEngine.Vector3�� ������ֽ� �κ��� �״�� �ΰų�,
    // ���� ��ܿ� using UnityEngine; �� �����Ƿ� �׳� Vector3�� �ᵵ �����մϴ�.
    // ���⼭�� �������� ���� �׳� Vector3�� ǥ���ϰڽ��ϴ�.

    void GenerateLogicalGrid()
    {
        _tileData.Clear();
        // �������ֽ� ���簢�� ������ ��� �� ���� ���� ���
        for (int r_offset_row = 0; r_offset_row < mapHeight; r_offset_row++)
        {
            for (int q_offset_col = 0; q_offset_col < mapWidth; q_offset_col++)
            {
                Vector3Int cubeCoords = OffsetToCube(new Vector2Int(q_offset_col, r_offset_row));
                Vector3 worldPos = CubeToWorld(cubeCoords);
                // isWalkable �� MovementCost�� �� �����ͳ� ������ ���� ��Ģ�� ���� ����
                HexTile tile = new HexTile(cubeCoords, worldPos, true, 1);
                _tileData[cubeCoords] = tile;
            }
        }
        Debug.Log($"Generated {_tileData.Count} hex tiles.");
    }

    #region Coordinate Conversions (�������ֽ� ����� �����ϰ� ����)
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

    #region Grid Operations (GetNeighbor �߰�)
    public HexTile GetTileAt(Vector3Int cubeCoords)
    {
        _tileData.TryGetValue(cubeCoords, out HexTile tile);
        return tile; // ã�� ���ϸ� �⺻�� HexTile (isWalkable=false ���� �� ����)
    }

    public bool TryGetTileAt(Vector3Int cubeCoords, out HexTile tile)
    {
        return _tileData.TryGetValue(cubeCoords, out tile);
    }

    public List<HexTile> GetNeighbors(Vector3Int cubeCoords)
    {
        List<HexTile> neighbors = new List<HexTile>();
        Vector3Int[] directions = GetCubeDirections(); // ���� �޼��� ���
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
    /// �־��� ť�� ��ǥ���� Ư�� �������� �� ĭ �̵��� �̿� ť�� ��ǥ�� �ش��ϴ� Ÿ���� ��ȯ�մϴ�.
    /// �ش� ��ġ�� Ÿ���� ���ų� ��ȿ���� ������ �⺻�� HexTile�� ��ȯ�� �� �����Ƿ�,
    /// ��ȯ�� Ÿ���� ��ȿ���� Ȯ���ؾ� �մϴ�. (�Ǵ� TryGetNeighbor)
    /// </summary>
    public HexTile GetNeighborTile(Vector3Int cubeCoords, int directionIndex)
    {
        Vector3Int[] directions = GetCubeDirections();
        if (directionIndex < 0 || directionIndex >= directions.Length)
        {
            Debug.LogError($"Invalid direction index for GetNeighborTile: {directionIndex}. Must be 0-5.");
            return default(HexTile); // �Ǵ� ���� �߻�
        }
        Vector3Int neighborCoords = cubeCoords + directions[directionIndex];
        TryGetTileAt(neighborCoords, out HexTile neighborTile); // ����� �ֵ� ���� neighborTile ��ȯ
        return neighborTile;
    }

    /// <summary>
    /// Ư�� ť�� ��ǥ���� �־��� ����(0-5)�� �̿� Ÿ�� ������ ���������� �õ��մϴ�.
    /// </summary>
    public bool TryGetNeighbor(Vector3Int cubeCoords, int direction, out HexTile neighborTile)
    {
        neighborTile = default(HexTile);
        if (direction < 0 || direction >= CUBE_DIRECTIONS.Length)
        {
            Debug.LogError($"TryGetNeighbor: ��ȿ���� ���� ���� ���Դϴ� - {direction}");
            return false;
        }   

        Vector3Int neighborCubeCoords = cubeCoords + CUBE_DIRECTIONS[direction];
        return TryGetTileAt(neighborCubeCoords, out neighborTile);
    }

    /// <summary>
    /// �־��� ť�� ��ǥ���� Ư�� �������� �� ĭ �̵��� �̿� ť�� ��ǥ�� ��ȯ�մϴ�.
    /// (FormationUtility���� �ʿ�� �ߴ� �Լ�)
    /// </summary>
    public Vector3Int GetNeighborCubeCoord(Vector3Int cubeCoords, int directionIndex)
    {
        Vector3Int[] directions = GetCubeDirections();
        if (directionIndex < 0 || directionIndex >= directions.Length)
        {
            Debug.LogError($"Invalid direction index for GetNeighborCubeCoord: {directionIndex}. Must be 0-5.");
            return cubeCoords; // ���� �� ���� ��ġ ��ȯ (�Ǵ� ����)
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

    // OnDrawGizmos, DrawHexGizmo, HexCornerOffset �Լ��� �������ֽ� ����� �����ϰ� ����
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



