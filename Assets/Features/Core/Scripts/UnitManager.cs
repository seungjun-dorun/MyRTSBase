using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq 사용을 위해 추가 (activeUnits 관리 시)

public class UnitManager : MonoBehaviour
{
    public HexGridSystem hexGridSystemRef; // Inspector 또는 Start에서 할당
    public List<UnitData> availableUnitTypes; // Inspector에서 유닛 데이터 목록 할당

    private Dictionary<int, SimulatedObject> _allUnits = new Dictionary<int, SimulatedObject>();
    // 활성 유닛 리스트는 이제 Unit의 CurrentState가 Idle이 아닌 경우로 필터링 가능
    // private List<SimulatedObject> _activeUnits = new List<SimulatedObject>(); // 직접 관리 대신 필터링

    private int _nextAvailableInstanceID = 1;

    void Start()
    {
        if (hexGridSystemRef == null) hexGridSystemRef = FindFirstObjectByType<HexGridSystem>();

        if (hexGridSystemRef == null)
        {
            Debug.LogError("UnitManager: HexGridSystem 참조가 없습니다!");
            return;
        }

        // 테스트용 유닛 생성 (예: 0,0,0 큐브 좌표에)
        if (availableUnitTypes != null && availableUnitTypes.Count > 0 && availableUnitTypes[0] != null)
        {
            Vector3Int spawnCoord = Vector3Int.zero; // 맵 중앙 근처 또는 유효한 좌표
            if (hexGridSystemRef.IsValidHex(spawnCoord)) // 생성 위치가 유효한지 확인
            {
                for (int i = 0; i < 3; i++)
                {

                    CreateUnit(availableUnitTypes[0].unitName, spawnCoord, 0); // 플레이어 ID 0으로 가정
                }
            }
            else
            {
                Debug.LogWarning($"테스트 유닛 생성 실패: {spawnCoord}는 유효한 타일이 아닙니다.");
            }



            // 두 번째 유닛 생성 (다른 위치, 다른 플레이어 ID로 가정 - AI 테스트 등)
            Vector3Int spawnCoord2 = new Vector3Int(1, 0, -1); // (0,0,0)에서 한 칸 옆
            if (hexGridSystemRef.IsValidHex(spawnCoord2))
            {

                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // 플레이어 ID 1로 가정
                spawnCoord2.x += 1; // 한 칸 더 옆으로 이동 (예: (2,0,-2))
                spawnCoord2.z += -1;
                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // 플레이어 ID 1로 가정
                spawnCoord2.x += 1; // 한 칸 더 옆으로 이동 (예: (2,0,-2))
                spawnCoord2.z += -1;
                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // 플레이어 ID 1로 가정

            }
            else
            {
                Debug.LogWarning($"테스트 유닛 생성 실패: {spawnCoord2}는 유효한 타일이 아닙니다.");
            }
        }
        else
        {
            Debug.LogWarning("UnitManager: 테스트할 유닛 데이터가 Available Unit Types에 설정되지 않았습니다.");
        }





    }

    public SimulatedObject CreateUnit(string unitTypeName, Vector3Int spawnCubeCoords, ulong ownerId)
    {
        UnitData unitDataToCreate = availableUnitTypes.Find(ud => ud.unitName == unitTypeName);
        if (unitDataToCreate == null)
        {
            Debug.LogError($"UnitData '{unitTypeName}'을(를) 찾을 수 없습니다.");
            return null;
        }

        if (unitDataToCreate.unitPrefab == null)
        {
            Debug.LogError($"UnitData '{unitTypeName}'에 프리팹이 할당되지 않았습니다.");
            return null;
        }

        // HexGridSystem에서 실제 월드 위치 가져오기
        Vector3 spawnWorldPos = hexGridSystemRef.CubeToWorld(spawnCubeCoords);

        GameObject unitGO = Instantiate(unitDataToCreate.unitPrefab, spawnWorldPos, Quaternion.identity);
        SimulatedObject unitSim = unitGO.GetComponent<SimulatedObject>();

        if (unitSim == null)
        {
            Debug.LogError($"유닛 프리팹 '{unitDataToCreate.unitPrefab.name}'에 SimulatedObject 컴포넌트가 없습니다.");
            Destroy(unitGO);
            return null;
        }

        int newId = _nextAvailableInstanceID++;
        unitSim.Initialize(newId, ownerId, unitDataToCreate, hexGridSystemRef, this, spawnCubeCoords);

        _allUnits.Add(newId, unitSim);
        Debug.Log($"유닛 생성: {unitSim.InstanceID} ({unitDataToCreate.unitName}), 위치: {spawnCubeCoords}, 소유자: {ownerId}");
        return unitSim;
    }

    public void DestroyUnit(int instanceId)
    {
        if (_allUnits.TryGetValue(instanceId, out SimulatedObject unitToDestroy))
        {
            _allUnits.Remove(instanceId);
            // _activeUnits.Remove(unitToDestroy); // 만약 _activeUnits를 직접 관리했다면 여기서도 제거
            Destroy(unitToDestroy.gameObject);
            Debug.Log($"유닛 파괴: {instanceId}");
        }
    }

    public SimulatedObject GetUnitById(int instanceId)
    {
        _allUnits.TryGetValue(instanceId, out SimulatedObject unit);
        return unit;
    }

    // 틱 기반 업데이트 관리 (GameLogicManager에 의해 호출됨)
    public void UpdateUnitsForTick(float tickInterval)
    {
        // 1. 모든 유닛 중 활성 유닛만 업데이트 (예: Idle이 아니거나, 특별한 감지가 필요한 유닛)
        //    여기서는 간단히 모든 유닛을 순회하며 Dead가 아닌 유닛만 업데이트.
        //    더 최적화하려면, 상태 변경 시 별도의 active list를 관리.
        List<SimulatedObject> unitsToUpdate = _allUnits.Values.Where(u => u.CurrentActionState != SimulatedObject.UnitActionState.Dead).ToList();

        foreach (SimulatedObject unit in unitsToUpdate)
        {
            unit.SimulateStep(tickInterval);
        }

        // 2. 사망한 유닛 처리 (SimulateStep에서 Dead 상태가 된 유닛들)
        List<int> deadUnitIds = new List<int>();
        foreach (var pair in _allUnits)
        {
            if (pair.Value.CurrentActionState == SimulatedObject.UnitActionState.Dead && pair.Value.gameObject.activeSelf) // activeSelf는 임시 방편
            {
                // TODO: 사망 애니메이션/효과 후 파괴 로직. 여기서는 즉시 파괴 준비.
                pair.Value.gameObject.SetActive(false); // 시각적으로만 제거 (실제 데이터는 남아있음)
                                                        // DestroyUnit(pair.Key); // 이렇게 하면 반복 중 컬렉션 수정 오류 발생 가능
                deadUnitIds.Add(pair.Key);
            }
        }
        // 실제 파괴는 루프 이후에
        foreach (int id in deadUnitIds)
        {
            // DestroyUnit(id); // 게임 로직 상에서 바로 파괴할지, 아니면 풀링할지 등 결정
            // 여기서는 랙돌이나 시체 오브젝트로 전환하는 로직이 들어갈 수 있음.
            // 지금은 SimulateStep에서 Dead가 되면 더 이상 업데이트 안 하도록만 되어 있음.
            // UnitManager가 명시적으로 DestroyUnit을 호출하여 _allUnits에서 제거해야 함.
        }
    }

    public SimulatedObject[] GetPlayerUnits(ulong localPlayerId)
    {
        return _allUnits.Values.Where(u => u.OwnerPlayerId == localPlayerId && u.CurrentActionState != SimulatedObject.UnitActionState.Dead).ToArray();
    }
    // 모든 살아있는 유닛 목록을 반환 (SimulatedObject에서 사용 가능하도록)
    public IEnumerable<SimulatedObject> GetAllAliveUnits()
    {
        return _allUnits.Values.Where(u => u.IsAlive());
    }

    // 특정 플레이어의 모든 적 유닛 목록을 반환
    public IEnumerable<SimulatedObject> GetEnemyUnits(ulong currentPlayerId)
    {
        return _allUnits.Values.Where(u => u.IsAlive() && u.OwnerPlayerId != currentPlayerId);
    }

}