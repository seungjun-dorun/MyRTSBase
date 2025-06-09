using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq 사용


public class BuildingManager : MonoBehaviour
{
public HexGridSystem hexGridSystemRef;
public ResourceManager resourceManagerRef;
public UnitManager unitManagerRef; // 유닛 생산 시 필요할 수 있음
public List<BuildingData> availableBuildingTypes; // Inspector에서 건설 가능한 건물 데이터 목록 할당

private Dictionary<int, Building> _allBuildings = new Dictionary<int, Building>();
private int _nextAvailableInstanceID = 10000; // 유닛 ID와 구분하기 위해 높은 수부터 시작 (임시)

void Start()
{
    if (hexGridSystemRef == null) hexGridSystemRef = FindObjectOfType<HexGridSystem>();
    // ... 다른 참조들도 확인 ...
}

// GameLogicManager 등에 의해 호출될 수 있음
public void UpdateBuildingsForTick(float tickInterval)
{
    // 활성화된 (예: 건설 중이거나, 유닛 생산 중이거나, 공격 중인) 건물들만 업데이트
    List<Building> activeBuildings = _allBuildings.Values.Where(b => b.IsAlive() && (!b.isConstructed || b.CanProduceUnits)).ToList();
    foreach (Building building in activeBuildings)
    {
        building.SimulateStep(tickInterval);
    }
}

/// <summary>
/// 건설 명령을 받아 새 건물을 "건설 예정지"로 등록하고, 일꾼에게 이동/건설 지시.
/// 실제 건설 시작 및 GameObject 생성은 이 함수 또는 GameLogicManager에서.
/// </summary>
public Building StartPlacingBuilding(string buildingDataID, Vector3Int targetCubeCoord, ulong playerId, List<WorkerUnit> workers)
{
    BuildingData dataToBuild = availableBuildingTypes.Find(bd => bd.unitName == buildingDataID); // 또는 ID로 찾기
    if (dataToBuild == null)
    {
        Debug.LogError($"BuildingData '{buildingDataID}'를 찾을 수 없습니다.");
        return null;
    }

    // 1. 건설 위치 유효성 검사 (평지인가? 다른 건물과 겹치지 않는가? 등)
    if (!CanPlaceBuildingAt(dataToBuild, targetCubeCoord, playerId))
    {
        Debug.LogWarning($"건물 {buildingDataID}를 {targetCubeCoord}에 건설할 수 없습니다.");
        return null;
    }

    // 2. 자원 소모 (ResourceManager 통해)
    if (resourceManagerRef != null)
    {
        if (!resourceManagerRef.TryConsumeResources(playerId, CreateCostDictionaryFromBuildingData(dataToBuild)))
        {
            Debug.LogWarning($"플레이어 {playerId}는 건물 {buildingDataID} 건설 비용이 부족합니다.");
            return null; // 자원 부족
        }
    }

    // 3. 건물 GameObject 생성 (초기에는 "건설 중" 모습)
    GameObject buildingGO = Instantiate(dataToBuild.unitPrefab, hexGridSystemRef.CubeToWorld(targetCubeCoord), Quaternion.identity);
    Building newBuilding = buildingGO.GetComponent<Building>(); // 또는 AddComponent
    if (newBuilding == null)
    {
        Debug.LogError("건물 프리팹에 Building 컴포넌트가 없습니다!");
        Destroy(buildingGO);
        return null;
    }

    int newId = _nextAvailableInstanceID++;
    newBuilding.Initialize(newId, playerId, dataToBuild, hexGridSystemRef, unitManagerRef, targetCubeCoord);
    _allBuildings.Add(newId, newBuilding);

    // 4. 해당 위치를 "건설 중"으로 표시 (HexGridSystem의 타일 데이터 업데이트)
    MarkTilesAsOccupied(targetCubeCoord, dataToBuild.sizeInTiles, newBuilding);


    // 5. 일꾼 유닛들에게 건설 명령 전달
    if (workers != null && workers.Count > 0)
    {
        foreach (WorkerUnit worker in workers)
        {
            // worker.SetBuildCommand(newBuilding, targetCubeCoord); // 일꾼에게 건설 대상 건물과 위치 전달
            // 실제로는 GameLogicManager가 BuildBuildingCommand를 처리하면서
            // 일꾼에게는 건설 위치로 이동하라는 MoveCommand를 먼저 내리고,
            // 도착 후 BuildingManager.AssignWorkerToConstruction(worker, newBuilding) 등을 호출할 수 있음.
        }
    }
    else
    {
        // 일꾼 없이 자동 건설되는 건물이라면 여기서 바로 건설 시작 로직 호출 가능
        // newBuilding.StartSelfConstruction();
    }

    Debug.Log($"플레이어 {playerId}가 {targetCubeCoord}에 {buildingDataID} 건설 시작 (ID: {newId}).");
    return newBuilding;
}

// 일꾼이 건설 현장에 도착했을 때 호출될 수 있는 함수
public void AssignWorkerToConstruction(WorkerUnit worker, Building targetBuilding)
{
    if (worker == null || targetBuilding == null || targetBuilding.isConstructed) return;
    // worker의 상태를 Building으로 변경하고, targetBuilding을 건설 대상으로 설정
    // worker.CurrentState = SimulatedObject.UnitState.Building;
    // worker.targetBuildingToConstruct = targetBuilding;
    Debug.Log($"일꾼 {worker.InstanceID}가 건물 {targetBuilding.InstanceID} 건설에 할당됨.");
}


public bool CanPlaceBuildingAt(BuildingData buildingData, Vector3Int centerCubeCoord, ulong playerId)
{
    // TODO: 건설 위치 유효성 검사 로직
    // 1. 맵 경계 내인가?
    // 2. 해당 타일들이 비어있고 이동 가능한(또는 건설 가능한) 지형인가? (HexGridSystem 정보 참조)
    // 3. 다른 건물이나 유닛과 겹치지 않는가?
    // 4. (필요시) 특정 건물 옆에만 지을 수 있는 등의 규칙
    // 육각 그리드에서 여러 타일을 차지하는 건물 배치 시, 해당 모든 타일 검사 필요
    List<Vector3Int> occupiedCoords = GetBuildingFootprint(centerCubeCoord, buildingData.sizeInTiles);
    foreach (var coord in occupiedCoords)
    {
        if (!hexGridSystemRef.IsValidHex(coord)) return false; // 맵 밖
        HexTile tile = hexGridSystemRef.GetTileAt(coord);
        if (!tile.isWalkable) return false; // 이동 불가 지역 (또는 isBuildable 플래그 확인)
        // if (IsTileOccupiedByAnotherBuilding(coord)) return false; // 다른 건물과 겹치는지 확인
    }
    return true;
}

private void MarkTilesAsOccupied(Vector3Int centerCubeCoord, Vector3Int size, Building occupyingBuilding)
{
    // TODO: 건물이 차지하는 모든 타일들을 "이동 불가" 또는 "점유됨" 상태로 변경
    // HexGridSystem의 HexTile 데이터에 isWalkable = false 또는 occupyingBuildingId = building.InstanceID 설정
    List<Vector3Int> occupiedCoords = GetBuildingFootprint(centerCubeCoord, size);
    foreach (var coord in occupiedCoords)
    {
        if (hexGridSystemRef.TryGetTileAt(coord, out HexTile tile))
        {
            // tile.isWalkable = false; // 이렇게 직접 바꾸려면 HexTile이 클래스여야 하거나,
            // _tileData를 업데이트하는 함수를 HexGridSystem에 만들어야 함.
            // hexGridSystemRef.SetTileOccupancy(coord, false, occupyingBuilding.InstanceID);
        }
    }
}
private void MarkTilesAsVacant(Vector3Int centerCubeCoord, Vector3Int size)
{
    // 건물이 파괴되었을 때 해당 타일들을 다시 "이동 가능"으로 변경
    // ... MarkTilesAsOccupied와 반대 로직 ...
}


public List<Vector3Int> GetBuildingFootprint(Vector3Int centerCubeCoord, Vector3Int size)
{
    // TODO: 건물의 중심 좌표와 크기를 기반으로 실제 차지하는 모든 타일의 큐브 좌표 리스트 반환
    // 육각 그리드에서 직사각형 건물을 배치하는 것은 까다로울 수 있음.
    // 건물의 형태를 육각형 기반(예: 중심 타일 + 주변 링)으로 하거나,
    // 또는 size.x 와 size.y를 육각 그리드 축에 맞춰 해석해야 함.
    // 지금은 간단히 중심 타일만 반환
    return new List<Vector3Int> { centerCubeCoord };
}


public Building GetBuildingById(int instanceId)
{
    _allBuildings.TryGetValue(instanceId, out Building building);
    return building;
}

public void NotifyBuildingDestroyed(Building destroyedBuilding)
{
    if (destroyedBuilding != null && _allBuildings.ContainsKey(destroyedBuilding.InstanceID))
    {
        MarkTilesAsVacant(destroyedBuilding.CurrentCubeCoords, destroyedBuilding.SizeInTiles);
        _allBuildings.Remove(destroyedBuilding.InstanceID);
        // GameObject 파괴는 Building.OnDeath에서 이미 처리되었거나, 여기서 처리
        // if (destroyedBuilding.gameObject != null) Destroy(destroyedBuilding.gameObject);
    }
}

// GameLogicManager에서 비용 정보를 쉽게 구성하기 위한 헬퍼 (ResourceManager에도 유사한 것 있음)
private Dictionary<string, int> CreateCostDictionaryFromBuildingData(BuildingData data)
{
    var costs = new Dictionary<string, int>();
    if (data.creationCost != null)
    {
        foreach (var cost in data.creationCost)
        {
            costs[cost.resourceName] = cost.amount;
        }
    }
    return costs;
}
}