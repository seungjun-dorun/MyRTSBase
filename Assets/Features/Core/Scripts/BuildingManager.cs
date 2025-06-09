using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq ���


public class BuildingManager : MonoBehaviour
{
public HexGridSystem hexGridSystemRef;
public ResourceManager resourceManagerRef;
public UnitManager unitManagerRef; // ���� ���� �� �ʿ��� �� ����
public List<BuildingData> availableBuildingTypes; // Inspector���� �Ǽ� ������ �ǹ� ������ ��� �Ҵ�

private Dictionary<int, Building> _allBuildings = new Dictionary<int, Building>();
private int _nextAvailableInstanceID = 10000; // ���� ID�� �����ϱ� ���� ���� ������ ���� (�ӽ�)

void Start()
{
    if (hexGridSystemRef == null) hexGridSystemRef = FindObjectOfType<HexGridSystem>();
    // ... �ٸ� �����鵵 Ȯ�� ...
}

// GameLogicManager � ���� ȣ��� �� ����
public void UpdateBuildingsForTick(float tickInterval)
{
    // Ȱ��ȭ�� (��: �Ǽ� ���̰ų�, ���� ���� ���̰ų�, ���� ����) �ǹ��鸸 ������Ʈ
    List<Building> activeBuildings = _allBuildings.Values.Where(b => b.IsAlive() && (!b.isConstructed || b.CanProduceUnits)).ToList();
    foreach (Building building in activeBuildings)
    {
        building.SimulateStep(tickInterval);
    }
}

/// <summary>
/// �Ǽ� ����� �޾� �� �ǹ��� "�Ǽ� ������"�� ����ϰ�, �ϲۿ��� �̵�/�Ǽ� ����.
/// ���� �Ǽ� ���� �� GameObject ������ �� �Լ� �Ǵ� GameLogicManager����.
/// </summary>
public Building StartPlacingBuilding(string buildingDataID, Vector3Int targetCubeCoord, ulong playerId, List<WorkerUnit> workers)
{
    BuildingData dataToBuild = availableBuildingTypes.Find(bd => bd.unitName == buildingDataID); // �Ǵ� ID�� ã��
    if (dataToBuild == null)
    {
        Debug.LogError($"BuildingData '{buildingDataID}'�� ã�� �� �����ϴ�.");
        return null;
    }

    // 1. �Ǽ� ��ġ ��ȿ�� �˻� (�����ΰ�? �ٸ� �ǹ��� ��ġ�� �ʴ°�? ��)
    if (!CanPlaceBuildingAt(dataToBuild, targetCubeCoord, playerId))
    {
        Debug.LogWarning($"�ǹ� {buildingDataID}�� {targetCubeCoord}�� �Ǽ��� �� �����ϴ�.");
        return null;
    }

    // 2. �ڿ� �Ҹ� (ResourceManager ����)
    if (resourceManagerRef != null)
    {
        if (!resourceManagerRef.TryConsumeResources(playerId, CreateCostDictionaryFromBuildingData(dataToBuild)))
        {
            Debug.LogWarning($"�÷��̾� {playerId}�� �ǹ� {buildingDataID} �Ǽ� ����� �����մϴ�.");
            return null; // �ڿ� ����
        }
    }

    // 3. �ǹ� GameObject ���� (�ʱ⿡�� "�Ǽ� ��" ���)
    GameObject buildingGO = Instantiate(dataToBuild.unitPrefab, hexGridSystemRef.CubeToWorld(targetCubeCoord), Quaternion.identity);
    Building newBuilding = buildingGO.GetComponent<Building>(); // �Ǵ� AddComponent
    if (newBuilding == null)
    {
        Debug.LogError("�ǹ� �����տ� Building ������Ʈ�� �����ϴ�!");
        Destroy(buildingGO);
        return null;
    }

    int newId = _nextAvailableInstanceID++;
    newBuilding.Initialize(newId, playerId, dataToBuild, hexGridSystemRef, unitManagerRef, targetCubeCoord);
    _allBuildings.Add(newId, newBuilding);

    // 4. �ش� ��ġ�� "�Ǽ� ��"���� ǥ�� (HexGridSystem�� Ÿ�� ������ ������Ʈ)
    MarkTilesAsOccupied(targetCubeCoord, dataToBuild.sizeInTiles, newBuilding);


    // 5. �ϲ� ���ֵ鿡�� �Ǽ� ��� ����
    if (workers != null && workers.Count > 0)
    {
        foreach (WorkerUnit worker in workers)
        {
            // worker.SetBuildCommand(newBuilding, targetCubeCoord); // �ϲۿ��� �Ǽ� ��� �ǹ��� ��ġ ����
            // �����δ� GameLogicManager�� BuildBuildingCommand�� ó���ϸ鼭
            // �ϲۿ��Դ� �Ǽ� ��ġ�� �̵��϶�� MoveCommand�� ���� ������,
            // ���� �� BuildingManager.AssignWorkerToConstruction(worker, newBuilding) ���� ȣ���� �� ����.
        }
    }
    else
    {
        // �ϲ� ���� �ڵ� �Ǽ��Ǵ� �ǹ��̶�� ���⼭ �ٷ� �Ǽ� ���� ���� ȣ�� ����
        // newBuilding.StartSelfConstruction();
    }

    Debug.Log($"�÷��̾� {playerId}�� {targetCubeCoord}�� {buildingDataID} �Ǽ� ���� (ID: {newId}).");
    return newBuilding;
}

// �ϲ��� �Ǽ� ���忡 �������� �� ȣ��� �� �ִ� �Լ�
public void AssignWorkerToConstruction(WorkerUnit worker, Building targetBuilding)
{
    if (worker == null || targetBuilding == null || targetBuilding.isConstructed) return;
    // worker�� ���¸� Building���� �����ϰ�, targetBuilding�� �Ǽ� ������� ����
    // worker.CurrentState = SimulatedObject.UnitState.Building;
    // worker.targetBuildingToConstruct = targetBuilding;
    Debug.Log($"�ϲ� {worker.InstanceID}�� �ǹ� {targetBuilding.InstanceID} �Ǽ��� �Ҵ��.");
}


public bool CanPlaceBuildingAt(BuildingData buildingData, Vector3Int centerCubeCoord, ulong playerId)
{
    // TODO: �Ǽ� ��ġ ��ȿ�� �˻� ����
    // 1. �� ��� ���ΰ�?
    // 2. �ش� Ÿ�ϵ��� ����ְ� �̵� ������(�Ǵ� �Ǽ� ������) �����ΰ�? (HexGridSystem ���� ����)
    // 3. �ٸ� �ǹ��̳� ���ְ� ��ġ�� �ʴ°�?
    // 4. (�ʿ��) Ư�� �ǹ� ������ ���� �� �ִ� ���� ��Ģ
    // ���� �׸��忡�� ���� Ÿ���� �����ϴ� �ǹ� ��ġ ��, �ش� ��� Ÿ�� �˻� �ʿ�
    List<Vector3Int> occupiedCoords = GetBuildingFootprint(centerCubeCoord, buildingData.sizeInTiles);
    foreach (var coord in occupiedCoords)
    {
        if (!hexGridSystemRef.IsValidHex(coord)) return false; // �� ��
        HexTile tile = hexGridSystemRef.GetTileAt(coord);
        if (!tile.isWalkable) return false; // �̵� �Ұ� ���� (�Ǵ� isBuildable �÷��� Ȯ��)
        // if (IsTileOccupiedByAnotherBuilding(coord)) return false; // �ٸ� �ǹ��� ��ġ���� Ȯ��
    }
    return true;
}

private void MarkTilesAsOccupied(Vector3Int centerCubeCoord, Vector3Int size, Building occupyingBuilding)
{
    // TODO: �ǹ��� �����ϴ� ��� Ÿ�ϵ��� "�̵� �Ұ�" �Ǵ� "������" ���·� ����
    // HexGridSystem�� HexTile �����Ϳ� isWalkable = false �Ǵ� occupyingBuildingId = building.InstanceID ����
    List<Vector3Int> occupiedCoords = GetBuildingFootprint(centerCubeCoord, size);
    foreach (var coord in occupiedCoords)
    {
        if (hexGridSystemRef.TryGetTileAt(coord, out HexTile tile))
        {
            // tile.isWalkable = false; // �̷��� ���� �ٲٷ��� HexTile�� Ŭ�������� �ϰų�,
            // _tileData�� ������Ʈ�ϴ� �Լ��� HexGridSystem�� ������ ��.
            // hexGridSystemRef.SetTileOccupancy(coord, false, occupyingBuilding.InstanceID);
        }
    }
}
private void MarkTilesAsVacant(Vector3Int centerCubeCoord, Vector3Int size)
{
    // �ǹ��� �ı��Ǿ��� �� �ش� Ÿ�ϵ��� �ٽ� "�̵� ����"���� ����
    // ... MarkTilesAsOccupied�� �ݴ� ���� ...
}


public List<Vector3Int> GetBuildingFootprint(Vector3Int centerCubeCoord, Vector3Int size)
{
    // TODO: �ǹ��� �߽� ��ǥ�� ũ�⸦ ������� ���� �����ϴ� ��� Ÿ���� ť�� ��ǥ ����Ʈ ��ȯ
    // ���� �׸��忡�� ���簢�� �ǹ��� ��ġ�ϴ� ���� ��ٷο� �� ����.
    // �ǹ��� ���¸� ������ ���(��: �߽� Ÿ�� + �ֺ� ��)���� �ϰų�,
    // �Ǵ� size.x �� size.y�� ���� �׸��� �࿡ ���� �ؼ��ؾ� ��.
    // ������ ������ �߽� Ÿ�ϸ� ��ȯ
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
        // GameObject �ı��� Building.OnDeath���� �̹� ó���Ǿ��ų�, ���⼭ ó��
        // if (destroyedBuilding.gameObject != null) Destroy(destroyedBuilding.gameObject);
    }
}

// GameLogicManager���� ��� ������ ���� �����ϱ� ���� ���� (ResourceManager���� ������ �� ����)
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