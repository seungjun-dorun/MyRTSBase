using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class FormationUtility
{
    /// <summary>
    /// �׷� ��ǥ ������ ���� ����Ʈ�� ������� �� ������ ���� ��ǥ ������ ���. (���� �׸����)
    /// </summary>
    /// <param name="groupTargetCoord">�׷��� �ֿ� ��ǥ ��ǥ (ť�� ��ǥ��).</param>
    /// <param name="units">��� ���ֵ��� ����Ʈ�Դϴ�.</param>
    /// <param name="hexGridSystem">��� �׸��� �ý��� �����Դϴ�.</param>
    /// <param name="formationType">���ϴ� �����̼� Ÿ�� (��: "Spiral", "Line", "Wedge").</param>
    /// <param name="spacing">���� �� ���� (Ÿ�� ����, ��: 1�̸� �ٷ� �� Ÿ��, 2�� �� ĭ �ǳ� �� Ÿ��).</param>
    /// <returns>�� ������ ���� ��ǥ ��ǥ ����Ʈ�Դϴ�.</returns>
    public static List<Vector3Int> CalculateIndividualTargetPositions(
        Vector3Int groupTargetCoord,
        List<SimulatedObject> units,
        HexGridSystem hexGridSystem,
        string formationType = "Spiral", // �⺻ �����̼� Ÿ��
        int spacing = 1) // ���� �׸��忡���� ���� 1 (����) �Ǵ� 2 (��ĭ ���) ������ ���
    {
        List<Vector3Int> positions = new List<Vector3Int>();
        if (units == null || units.Count == 0 || hexGridSystem == null)
        {
            return positions;
        }

        // ��ǥ ������ ��ȿ�ϰ� ���� �� �ִ��� ���� Ȯ��
        if (!hexGridSystem.IsValidHex(groupTargetCoord) || !hexGridSystem.GetTileAt(groupTargetCoord).isWalkable)
        {
            // ��ȿ���� ���� ��ǥ ���� ó�� (��: ���� ����� ��ȿ�� �������� �����ϰų�, �� ����Ʈ ��ȯ)
            Debug.LogWarning($"FormationUtility: Group target {groupTargetCoord} is invalid or unwalkable.");
            // �ӽ÷� ��� ���ֿ��� ������ (��ȿ���� ����) ��ǥ�� �ְų�, �� ����Ʈ ��ȯ
            for (int i = 0; i < units.Count; i++) positions.Add(groupTargetCoord);
            return positions;
        }

        if (units.Count == 1)
        {
            positions.Add(groupTargetCoord);
            return positions;
        }

        switch (formationType.ToLower())
        {
            case "line":
                // positions = CalculateLineFormation(groupTargetCoord, units.Count, hexGridSystem, spacing, units[0].ForwardDirection); // ����: ù ������ ���� ����
                break;
            case "wedge":
                // positions = CalculateWedgeFormation(groupTargetCoord, units.Count, hexGridSystem, spacing, units[0].ForwardDirection);
                break;
            case "spiral": // �⺻�� �Ǵ� ����� ����
            default:
                positions = CalculateSpiralFormation(groupTargetCoord, units.Count, hexGridSystem, spacing);
                break;
        }

        // ���� �����̼� ��� �� �Ҵ�� ��ġ�� ���� ������ ���ٸ� (��: ���� ����)
        // ���� ���ֵ��� groupTargetCoord �Ǵ� ���� ������ ��ġ �ֺ��� ��ġ
        if (positions.Count < units.Count)
        {
            Debug.LogWarning($"FormationUtility: Not enough valid positions found for all units in {formationType} formation. Remaining units will use group target or last valid position.");
            int currentPosCount = positions.Count;
            for (int i = currentPosCount; i < units.Count; i++)
            {
                positions.Add(positions.Count > 0 ? positions.Last() : groupTargetCoord); // ������ ��ȿ ��ġ �Ǵ� �׷� Ÿ��
            }
        }

        return positions.Take(units.Count).ToList(); // ��Ȯ�� ���� ����ŭ�� ��ȯ
    }

    /// <summary>
    /// ��ǥ ���� �ֺ����� ���������� ���������� �����̼� ��ġ�� ���.
    /// ���� �׸��忡�� �ֺ� Ÿ���� ã�� ���� �Ϲ����� ��� �� �ϳ���.
    /// </summary>
    private static List<Vector3Int> CalculateSpiralFormation(
        Vector3Int centerCoord,
        int numUnits,
        HexGridSystem hexGridSystem,
        int spacing) // spacing�� Ÿ�� ��(ring) �������� �ؼ��� �� ����
    {
        List<Vector3Int> formationPositions = new List<Vector3Int>();
        HashSet<Vector3Int> occupiedByFormation = new HashSet<Vector3Int>();

        // 1. �߾� ���� (����) ��ġ �Ҵ� (�߾��� ���� �� �ִٸ�)
        if (hexGridSystem.IsValidHex(centerCoord) && hexGridSystem.GetTileAt(centerCoord).isWalkable)
        {
            formationPositions.Add(centerCoord);
            occupiedByFormation.Add(centerCoord);
            if (formationPositions.Count >= numUnits) return formationPositions;
        }
        else
        {
            // �߾��� ���� �� ���ٸ�, ���⼭ ó���� �ߴ��ϰų� ��ü ��ġ�� ã�ƾ� ��.
            // ���⼭�� ������ �� ����Ʈ�� ��ȯ�ϰų�, ����� ��ȿ�� Ÿ���� ã�� ������ �� ����.
            // ������ �׳� �����Ͽ� �ֺ����� ã���� ��.
        }


        // 2. �ֺ� ��(ring)���� Ȯ���ϸ� ��ġ �Ҵ�
        // spacing�� '�� ��° ������ ����� ���ΰ�' �Ǵ� '�� ������ ����' ������ �ؼ� ����.
        // ���⼭�� ���� ���� ������ ä�������ٰ� ���� (spacing = 1 �� �⺻)
        // ���� ���ְ� '������ ����'�� spacing���� �η��� GetTilesInRingAtDistance ���� �Լ��� �ʿ�.
        int currentRingRadius = 1; // ù ��° ������ ����
        int safetyBreak = numUnits * 5 + 10; // ����� ū ������ ���� ���� ����
        int iterations = 0;

        while (formationPositions.Count < numUnits && iterations < safetyBreak)
        {
            iterations++;
            // ���� �������� ���� �ִ� Ÿ�ϵ��� ������
            List<Vector3Int> ringTiles = GetHexesInRing(centerCoord, currentRingRadius, hexGridSystem);

            if (ringTiles.Count == 0 && currentRingRadius > numUnits) // �� �̻� ã�� ���� ���ٰ� �Ǵ�
            {
                Debug.LogWarning("SpiralFormation: No more tiles found in outer rings.");
                break;
            }

            // �� Ÿ�ϵ��� (����������) Ư�� ������ �����Ͽ� �ϰ��� �Ҵ� ����
            // ��: Ư�� ���⿡�� �����Ͽ� �ð�/�ݽð� ��������
            // ringTiles.Sort((a,b) => CompareHexPositionsClockwise(centerCoord, a, b)); // ���� �Լ� �ʿ�

            foreach (Vector3Int tileCoord in ringTiles)
            {
                if (hexGridSystem.IsValidHex(tileCoord) &&
                    hexGridSystem.GetTileAt(tileCoord).isWalkable &&
                    !occupiedByFormation.Contains(tileCoord))
                {
                    formationPositions.Add(tileCoord);
                    occupiedByFormation.Add(tileCoord);
                    if (formationPositions.Count >= numUnits) break;
                }
            }
            currentRingRadius++; // ���� ������
        }
        return formationPositions;
    }

    /// <summary>
    /// (���� �Լ�) Ư�� �߽����κ��� �־��� ������(�� �ε���)�� ���� Ÿ�ϵ��� ��ȯ����
    /// HexGridSystem�� �� ����� �ִٸ� �װ��� ����ϴ� ���� ������
    /// �̴� ť�� ��ǥ�踦 �������� �� ���� �׸��� ���� ã�� �Ϲ����� �������
    /// </summary>
    /// <param name="center">�߽� ť�� ��ǥ</param>
    /// <param name="radius">������ (�� �ε���, 0�� �߽�, 1�� ù ��° ��)</param>
    /// <param name="grid">HexGridSystem ���� (��ȿ�� �˻��)</param>
    /// <returns>�ش� ���� ���ϴ� ť�� ��ǥ ����Ʈ</returns>
    public static List<Vector3Int> GetHexesInRing(Vector3Int center, int radius, HexGridSystem grid)
    {
        List<Vector3Int> results = new List<Vector3Int>();
        if (radius == 0)
        {
            if (grid != null && grid.IsValidHex(center) && grid.IsTileWalkable(center))
            {
                results.Add(center);
            }
            return results;
        }
        if (radius < 0 || grid == null) // grid null üũ �߰�
        {
            Debug.LogError("GetHexesInRing: Radius is negative or HexGridSystem is null.");
            return results;
        }
    
        Vector3Int[] directions = HexGridSystem.GetCubeDirections();
        if (directions == null || directions.Length < 6)
        {
            Debug.LogError("GetHexesInRing: HexGridSystem.GetCubeDirections() returned null or insufficient directions!");
            return results;
        }
    
        // ���� ������ ���: �߽ɿ��� Ư�� ����(��: directions[4])���� radius��ŭ �̵�
        // �� ������ ����� Red Blob Games�� cube_add(center, cube_scale(cube_direction(X), radius))�� �ش�
        Vector3Int currentHex = center + directions[4] * radius; // ���� �̸��� currentHex�� ��Ȯ��
    
        for (int sideIndex = 0; sideIndex < 6; sideIndex++) // 6���� ����(��)�� ��ȸ
        {
            for (int stepInSide = 0; stepInSide < radius; stepInSide++) // �� ���� ���� radius ��ŭ �̵�
            {
                // ���� Ÿ���� ��ȿ�ϰ� ���� �� ������ ����� �߰�
                // �ߺ� �߰��� �����ϱ� ���� HashSet�� ����ϰų�, results.Contains üũ
                if (grid.IsValidHex(currentHex) && grid.IsTileWalkable(currentHex))
                {
                    if (!results.Contains(currentHex)) // Contains�� List���� ������ ���� ����. HashSet ����.
                    {
                        results.Add(currentHex);
                    }
                }
    
                // ���� ��(sideIndex)�� �������� ���� �� Ÿ�Ϸ� �̵�
                // (����: currentHex ��ü�� ������Ʈ�ؾ� ���� �ݺ����� �ùٸ� ��ġ���� ����)
                if (grid.TryGetNeighbor(currentHex, sideIndex, out HexTile neighborTile))
                {
                    currentHex = neighborTile.cubeCoords;
                }
                else
                {
                    // �� �������� �� �̻� ������ �� ���� (�� ��� ��)
                    // �̴� ���� �� ��迡 ���������� �ǹ�.
                    // �ش� 'side'�� ������ 'step'�� �ǳʶٰ� ���� 'side'�� �����ؾ� ��.
                    // Debug.LogWarning($"GetHexesInRing: Cannot find neighbor for {currentHex} in direction {sideIndex}. Ring might be incomplete.");
                    break; // ���� for (stepInSide) ���� �ߴ�, ���� sideIndex�� �Ѿ
                }
            }
        }
        return results;
        // HashSet<Vector3Int> resultSet = new HashSet<Vector3Int>(); // �ߺ� ���� ����� ���ϸ� HashSet ���
        // ... ���� ������ resultSet.Add(currentHex); ...
        // return new List<Vector3Int>(resultSet);
    }

    // ���� �ٸ� �����̼� �Լ� �߰� ����

    // --- ��Ÿ �����̼� Ÿ�� �Լ� (����) ---

    // public static List<Vector3Int> CalculateLineFormation(
    //     Vector3Int startCoord, int numUnits, HexGridSystem hexGridSystem, int spacing, Vector3Int lineDirection)
    // {
    //     List<Vector3Int> positions = new List<Vector3Int>();
    //     Vector3Int currentPos = startCoord;
    //     // lineDirection�� ���� �׸����� 6���� �� �ϳ����� ��
    //     // ��: HexGridSystem.GetDirectionVector("NorthEast")
    //
    //     for (int i = 0; i < numUnits; i++)
    //     {
    //         Vector3Int placementPos = currentPos;
    //         for(int s=0; s < i * spacing; s++) // spacing ��ŭ �̵�
    //         {
    //            placementPos += lineDirection; // �����δ� �� �������� �̵��ϴ� ���� �ʿ�
    //         }
    //
    //         if (hexGridSystem.IsValidHex(placementPos) && hexGridSystem.GetTileAt(placementPos).isWalkable)
    //         {
    //             positions.Add(placementPos);
    //         }
    //         else
    //         {
    //             // ��ȿ���� �ʰų� ���� �� ���� ��� ó�� (��: ���� ��ġ ��� �Ǵ� �ߴ�)
    //             if (positions.Count > 0) positions.Add(positions.Last()); // �ӽ�
    //             else positions.Add(startCoord); // �ӽ�
    //         }
    //     }
    //     return positions;
    // }
}
