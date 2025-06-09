using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class FormationUtility
{
    /// <summary>
    /// 그룹 목표 지점과 유닛 리스트를 기반으로 각 유닛의 개별 목표 지점을 계산. (육각 그리드용)
    /// </summary>
    /// <param name="groupTargetCoord">그룹의 주요 목표 좌표 (큐브 좌표계).</param>
    /// <param name="units">대상 유닛들의 리스트입니다.</param>
    /// <param name="hexGridSystem">헥사 그리드 시스템 참조입니다.</param>
    /// <param name="formationType">원하는 포메이션 타입 (예: "Spiral", "Line", "Wedge").</param>
    /// <param name="spacing">유닛 간 간격 (타일 단위, 예: 1이면 바로 옆 타일, 2면 한 칸 건너 뛴 타일).</param>
    /// <returns>각 유닛의 개별 목표 좌표 리스트입니다.</returns>
    public static List<Vector3Int> CalculateIndividualTargetPositions(
        Vector3Int groupTargetCoord,
        List<SimulatedObject> units,
        HexGridSystem hexGridSystem,
        string formationType = "Spiral", // 기본 포메이션 타입
        int spacing = 1) // 육각 그리드에서는 보통 1 (인접) 또는 2 (한칸 띄고) 정도를 사용
    {
        List<Vector3Int> positions = new List<Vector3Int>();
        if (units == null || units.Count == 0 || hexGridSystem == null)
        {
            return positions;
        }

        // 목표 지점이 유효하고 걸을 수 있는지 먼저 확인
        if (!hexGridSystem.IsValidHex(groupTargetCoord) || !hexGridSystem.GetTileAt(groupTargetCoord).isWalkable)
        {
            // 유효하지 않은 목표 지점 처리 (예: 가장 가까운 유효한 지점으로 변경하거나, 빈 리스트 반환)
            Debug.LogWarning($"FormationUtility: Group target {groupTargetCoord} is invalid or unwalkable.");
            // 임시로 모든 유닛에게 동일한 (유효하지 않은) 목표를 주거나, 빈 리스트 반환
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
                // positions = CalculateLineFormation(groupTargetCoord, units.Count, hexGridSystem, spacing, units[0].ForwardDirection); // 예시: 첫 유닛의 방향 기준
                break;
            case "wedge":
                // positions = CalculateWedgeFormation(groupTargetCoord, units.Count, hexGridSystem, spacing, units[0].ForwardDirection);
                break;
            case "spiral": // 기본값 또는 명시적 선택
            default:
                positions = CalculateSpiralFormation(groupTargetCoord, units.Count, hexGridSystem, spacing);
                break;
        }

        // 만약 포메이션 계산 후 할당된 위치가 유닛 수보다 적다면 (예: 공간 부족)
        // 남은 유닛들은 groupTargetCoord 또는 리더 유닛의 위치 주변에 배치
        if (positions.Count < units.Count)
        {
            Debug.LogWarning($"FormationUtility: Not enough valid positions found for all units in {formationType} formation. Remaining units will use group target or last valid position.");
            int currentPosCount = positions.Count;
            for (int i = currentPosCount; i < units.Count; i++)
            {
                positions.Add(positions.Count > 0 ? positions.Last() : groupTargetCoord); // 마지막 유효 위치 또는 그룹 타겟
            }
        }

        return positions.Take(units.Count).ToList(); // 정확히 유닛 수만큼만 반환
    }

    /// <summary>
    /// 목표 지점 주변으로 나선형으로 퍼져나가는 포메이션 위치를 계산.
    /// 육각 그리드에서 주변 타일을 찾는 가장 일반적인 방법 중 하나임.
    /// </summary>
    private static List<Vector3Int> CalculateSpiralFormation(
        Vector3Int centerCoord,
        int numUnits,
        HexGridSystem hexGridSystem,
        int spacing) // spacing은 타일 링(ring) 간격으로 해석될 수 있음
    {
        List<Vector3Int> formationPositions = new List<Vector3Int>();
        HashSet<Vector3Int> occupiedByFormation = new HashSet<Vector3Int>();

        // 1. 중앙 유닛 (리더) 위치 할당 (중앙이 걸을 수 있다면)
        if (hexGridSystem.IsValidHex(centerCoord) && hexGridSystem.GetTileAt(centerCoord).isWalkable)
        {
            formationPositions.Add(centerCoord);
            occupiedByFormation.Add(centerCoord);
            if (formationPositions.Count >= numUnits) return formationPositions;
        }
        else
        {
            // 중앙이 걸을 수 없다면, 여기서 처리를 중단하거나 대체 위치를 찾아야 함.
            // 여기서는 간단히 빈 리스트를 반환하거나, 가까운 유효한 타일을 찾아 시작할 수 있음.
            // 지금은 그냥 진행하여 주변에서 찾도록 함.
        }


        // 2. 주변 링(ring)으로 확장하며 위치 할당
        // spacing은 '몇 번째 링부터 사용할 것인가' 또는 '링 사이의 간격' 등으로 해석 가능.
        // 여기서는 가장 안쪽 링부터 채워나간다고 가정 (spacing = 1 이 기본)
        // 실제 유닛간 '물리적 간격'을 spacing으로 두려면 GetTilesInRingAtDistance 같은 함수가 필요.
        int currentRingRadius = 1; // 첫 번째 링부터 시작
        int safetyBreak = numUnits * 5 + 10; // 충분히 큰 값으로 무한 루프 방지
        int iterations = 0;

        while (formationPositions.Count < numUnits && iterations < safetyBreak)
        {
            iterations++;
            // 현재 반지름의 링에 있는 타일들을 가져옴
            List<Vector3Int> ringTiles = GetHexesInRing(centerCoord, currentRingRadius, hexGridSystem);

            if (ringTiles.Count == 0 && currentRingRadius > numUnits) // 더 이상 찾을 링이 없다고 판단
            {
                Debug.LogWarning("SpiralFormation: No more tiles found in outer rings.");
                break;
            }

            // 링 타일들을 (선택적으로) 특정 순서로 정렬하여 일관된 할당 보장
            // 예: 특정 방향에서 시작하여 시계/반시계 방향으로
            // ringTiles.Sort((a,b) => CompareHexPositionsClockwise(centerCoord, a, b)); // 정렬 함수 필요

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
            currentRingRadius++; // 다음 링으로
        }
        return formationPositions;
    }

    /// <summary>
    /// (헬퍼 함수) 특정 중심으로부터 주어진 반지름(링 인덱스)의 육각 타일들을 반환함함
    /// HexGridSystem에 이 기능이 있다면 그것을 사용하는 것이 좋으며
    /// 이는 큐브 좌표계를 기준으로 한 육각 그리드 링을 찾는 일반적인 방법임임
    /// </summary>
    /// <param name="center">중심 큐브 좌표</param>
    /// <param name="radius">반지름 (링 인덱스, 0은 중심, 1은 첫 번째 링)</param>
    /// <param name="grid">HexGridSystem 참조 (유효성 검사용)</param>
    /// <returns>해당 링에 속하는 큐브 좌표 리스트</returns>
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
        if (radius < 0 || grid == null) // grid null 체크 추가
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
    
        // 링의 시작점 계산: 중심에서 특정 방향(예: directions[4])으로 radius만큼 이동
        // 이 시작점 계산은 Red Blob Games의 cube_add(center, cube_scale(cube_direction(X), radius))에 해당
        Vector3Int currentHex = center + directions[4] * radius; // 변수 이름을 currentHex로 명확히
    
        for (int sideIndex = 0; sideIndex < 6; sideIndex++) // 6개의 방향(변)을 순회
        {
            for (int stepInSide = 0; stepInSide < radius; stepInSide++) // 각 변을 따라 radius 만큼 이동
            {
                // 현재 타일이 유효하고 걸을 수 있으면 결과에 추가
                // 중복 추가를 방지하기 위해 HashSet을 사용하거나, results.Contains 체크
                if (grid.IsValidHex(currentHex) && grid.IsTileWalkable(currentHex))
                {
                    if (!results.Contains(currentHex)) // Contains는 List에서 성능이 좋지 않음. HashSet 권장.
                    {
                        results.Add(currentHex);
                    }
                }
    
                // 현재 변(sideIndex)의 방향으로 다음 링 타일로 이동
                // (주의: currentHex 자체를 업데이트해야 다음 반복에서 올바른 위치에서 시작)
                if (grid.TryGetNeighbor(currentHex, sideIndex, out HexTile neighborTile))
                {
                    currentHex = neighborTile.cubeCoords;
                }
                else
                {
                    // 이 방향으로 더 이상 진행할 수 없음 (맵 경계 등)
                    // 이는 링이 맵 경계에 걸쳐있음을 의미.
                    // 해당 'side'의 나머지 'step'은 건너뛰고 다음 'side'로 진행해야 함.
                    // Debug.LogWarning($"GetHexesInRing: Cannot find neighbor for {currentHex} in direction {sideIndex}. Ring might be incomplete.");
                    break; // 안쪽 for (stepInSide) 루프 중단, 다음 sideIndex로 넘어감
                }
            }
        }
        return results;
        // HashSet<Vector3Int> resultSet = new HashSet<Vector3Int>(); // 중복 없는 결과를 원하면 HashSet 사용
        // ... 루프 내에서 resultSet.Add(currentHex); ...
        // return new List<Vector3Int>(resultSet);
    }

    // 추후 다른 포메이션 함수 추가 가능

    // --- 기타 포메이션 타입 함수 (예시) ---

    // public static List<Vector3Int> CalculateLineFormation(
    //     Vector3Int startCoord, int numUnits, HexGridSystem hexGridSystem, int spacing, Vector3Int lineDirection)
    // {
    //     List<Vector3Int> positions = new List<Vector3Int>();
    //     Vector3Int currentPos = startCoord;
    //     // lineDirection은 육각 그리드의 6방향 중 하나여야 함
    //     // 예: HexGridSystem.GetDirectionVector("NorthEast")
    //
    //     for (int i = 0; i < numUnits; i++)
    //     {
    //         Vector3Int placementPos = currentPos;
    //         for(int s=0; s < i * spacing; s++) // spacing 만큼 이동
    //         {
    //            placementPos += lineDirection; // 실제로는 이 방향으로 이동하는 로직 필요
    //         }
    //
    //         if (hexGridSystem.IsValidHex(placementPos) && hexGridSystem.GetTileAt(placementPos).isWalkable)
    //         {
    //             positions.Add(placementPos);
    //         }
    //         else
    //         {
    //             // 유효하지 않거나 걸을 수 없는 경우 처리 (예: 이전 위치 사용 또는 중단)
    //             if (positions.Count > 0) positions.Add(positions.Last()); // 임시
    //             else positions.Add(startCoord); // 임시
    //         }
    //     }
    //     return positions;
    // }
}
