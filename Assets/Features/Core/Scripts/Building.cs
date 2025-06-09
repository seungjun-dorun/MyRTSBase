using UnityEngine;
using System.Collections.Generic;

public class Building : SimulatedObject // SimulatedObject 상속
{
    [Header("Building State")]
    public bool isConstructed = false;
    public int currentConstructionProgressTicks = 0; // 현재까지 진행된 건설 틱

    public bool CanProduceUnits { get; protected set; }
    public List<string> ProducibleUnitDataIDs { get; protected set; } // 생산 가능한 유닛 데이터 ID 목록
    public int UnitProductionQueueSize { get; protected set; } // 최대 생산 대기열 크기

    public bool CanResearchUpgrades { get; protected set; }
    public List<string> ResearchableUpgradeDataIDs { get; protected set; } // 연구 가능한 업그레이드 데이터 ID 목록

    public bool ActsAsResourceDropOff { get; protected set; } // 자원 반납 건물 여부 (예: 본진)
    public bool ProvidesSupply { get; protected set; }      // 인구수 제공 여부 (예: 보급고)
    public int SupplyProvided { get; protected set; } // 제공하는 인구수


    // 유닛 생산 관련 (예시 - 생산 건물이라면)
    private Queue<string> _unitProductionQueue = new Queue<string>();
    private string _currentProducingUnitDataID = null;
    private int _currentUnitProductionProgressTicks = 0;

    // BuildingData로부터 초기화 (SimulatedObject의 Initialize를 오버라이드)
    public override void Initialize(int instanceId, ulong ownerId, UnitData data, HexGridSystem gridSystem, UnitManager unitManagerRef, Vector3Int initialCubeCoords)
    {
        base.Initialize(instanceId, ownerId, data, gridSystem, unitManagerRef,  initialCubeCoords); // 부모 초기화 호출

        // 전달받은 UnitData가 WorkerUnitData 타입인지 확인하고 캐스팅
        if (data is BuildingData buildingData)
        {
            CanProduceUnits = buildingData.canProduceUnits;
            ProducibleUnitDataIDs = buildingData.producibleUnitDataIDs ?? new List<string>();
            UnitProductionQueueSize = buildingData.unitProductionQueueSize;

            CanResearchUpgrades = buildingData.canResearchUpgrades;
            ResearchableUpgradeDataIDs = buildingData.researchableUpgradeDataIDs ?? new List<string>();

            ActsAsResourceDropOff = buildingData.actsAsResourceDropOff;
            ProvidesSupply = buildingData.providesSupply;
            SupplyProvided = buildingData.supplyProvided;
        }


        else
        {
            Debug.LogWarning($"BuildingUnit {InstanceID}에 BuildingData가 아닌 일반 UnitData가 할당되었습니다.");
        }

        isConstructed = false;
        currentConstructionProgressTicks = 0;

        // 건설 시작 시 건물의 외형을 "건설 중" 모습으로 변경 (선택 사항)
        UpdateConstructionVisuals(0f);
    }

    // GameLogicManager에 의해 매 틱 호출될 수 있음 (또는 BuildingManager가 호출)
    public new virtual void SimulateStep(float tickInterval) // new 또는 override
    {
        // base.SimulateStep(tickInterval); // 부모의 공통 로직 (예: 사망 체크)

        if (!isConstructed)
        {
            // 건설이 아직 완료되지 않았다면, 추가 로직 없음 (일꾼이 건설 진행)
            // 또는, 여기서 일꾼 없이도 천천히 지어지는 로직 추가 가능
        }
        else // 건설 완료 후
        {
            if (CurrentActionState == UnitActionState.Dead) return; // 이미 파괴됨

            // 건물 고유 기능 수행 (예: 유닛 생산 진행)
            if (CanProduceUnits)
            {
                UpdateUnitProduction(tickInterval);
            }
            // 방어 타워라면 주변 적 자동 공격 로직
            // if (buildingData.attackDamage > 0) { HandleAttacking(tickInterval); }
        }
    }

    // 일꾼에 의해 건설 진행 (결정론적)
    public bool AdvanceConstruction(int buildPowerPerTick) // 일꾼의 건설 능력치
    {
        if (isConstructed) return true;

        currentConstructionProgressTicks += buildPowerPerTick;
        float progressRatio = (float)currentConstructionProgressTicks / CreationTimeInTicks;
        UpdateConstructionVisuals(progressRatio); // 건설 진행에 따른 시각적 변화

        // 건설 진행에 따라 현재 체력도 서서히 증가시키는 것이 일반적
        CurrentHealth = Mathf.Max(1, Mathf.FloorToInt(MaxHealth * progressRatio));


        if (currentConstructionProgressTicks >= CreationTimeInTicks)
        {
            CompleteConstruction();
            return true;
        }
        return false;
    }

    private void CompleteConstruction()
    {
        isConstructed = true;
        CurrentHealth = MaxHealth; // 건설 완료 시 체력 최대로
        currentConstructionProgressTicks = CreationTimeInTicks; // 정확히 맞춤
        Debug.Log($"(ID: {InstanceID}) 건설 완료!");
        UpdateConstructionVisuals(1f); // 최종 모습으로

        // 건설 완료 이벤트 발생 (BuildingManager나 ResourceManager에 알림 - 예: 인구수 증가)
        if (ProvidesSupply)
        {
            FindObjectOfType<ResourceManager>()?.UpdateResourceCap(OwnerPlayerId, "Supply", FindObjectOfType<ResourceManager>().GetResourceCap(OwnerPlayerId, "Supply") + SupplyProvided);
        }
    }

    private void UpdateConstructionVisuals(float progressRatio)
    {
        // TODO: 건설 진행률에 따라 건물의 외형을 변경하는 로직
        // 예: 투명도 조절, 점진적으로 모델이 나타나거나, 건설 애니메이션 재생
        // 간단히는 체력바 UI로 진행률 표시
        // transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1.0f, progressRatio); // 임시 스케일 조절
    }

    // --- 유닛 생산 관련 함수 (생산 건물이라면) ---
    public bool AddToProductionQueue(string unitDataID)
    {
        if (!isConstructed || !CanProduceUnits || !ProducibleUnitDataIDs.Contains(unitDataID))
        {
            return false; // 건설 안됐거나, 생산 불가 건물이거나, 생산 못하는 유닛
        }
        if (_unitProductionQueue.Count >= UnitProductionQueueSize)
        {
            Debug.LogWarning($"건물 {InstanceID}: 유닛 생산 큐가 가득 찼습니다.");
            return false; // 큐 꽉 참
        }

        _unitProductionQueue.Enqueue(unitDataID);
        Debug.Log($"건물 {InstanceID}: 유닛 {unitDataID} 생산 큐에 추가됨.");
        return true;
    }

    private void UpdateUnitProduction(float tickInterval)
    {
        if (_currentProducingUnitDataID == null) // 현재 생산 중인 유닛이 없다면
        {
            if (_unitProductionQueue.Count > 0) // 큐에 대기 중인 유닛이 있다면
            {
                _currentProducingUnitDataID = _unitProductionQueue.Dequeue();
                _currentUnitProductionProgressTicks = 0;
                // UnitData를 가져와서 생산 시간 설정 (실제로는 UnitManager가 UnitData 관리)
                // UnitData producingUnitData = FindObjectOfType<UnitManager>().GetUnitDataByID(_currentProducingUnitDataID);
                // if (producingUnitData == null) { _currentProducingUnitDataID = null; return; }
                // _targetProductionTicks = producingUnitData.productionTimeInTicks;
                Debug.Log($"건물 {InstanceID}: 유닛 {_currentProducingUnitDataID} 생산 시작.");
            }
        }
        else // 현재 생산 중인 유닛이 있다면
        {
            _currentUnitProductionProgressTicks++;
            //UnitData producingUnitData = FindObjectOfType<UnitManager>()?.GetUnitDataByID(_currentProducingUnitDataID); // 임시 참조
            //if (producingUnitData != null && _currentUnitProductionProgressTicks >= GetProductionTimeInTicks(producingUnitData)) // 생산 시간은 UnitData에 있어야 함
            //{
                // 생산 완료!
            //    FindObjectOfType<UnitManager>()?.CreateUnit(_currentProducingUnitDataID, GetRallyPointOrDefault(), OwnerPlayerId); // RallyPoint 또는 건물 옆에 생성
            //    Debug.Log($"건물 {InstanceID}: 유닛 {_currentProducingUnitDataID} 생산 완료!");
            //    _currentProducingUnitDataID = null;
            //    _currentUnitProductionProgressTicks = 0;
                // 다음 유닛 생산 시도 (다음 틱에)
            //}
        }
    }

    // 임시: 유닛 데이터에서 생산 시간 가져오는 함수 (실제로는 UnitManager가 UnitData를 관리하고 이 정보 제공)
    private int GetProductionTimeInTicks(UnitData unitData)
    {
        // return unitData.productionTimeInTicks; // UnitData에 이 필드가 있다고 가정
        return 100; // 임시 고정값
    }

    private Vector3Int GetRallyPointOrDefault()
    {
        // TODO: 건물의 집결 지점(Rally Point) 반환 로직. 없으면 건물 주변 빈 타일.
        // 지금은 임시로 건물 옆 칸 반환
        if (_hexGridSystemRef != null)
        {
            var neighbors = _hexGridSystemRef.GetNeighbors(CurrentCubeCoords);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.isWalkable) return neighbor.cubeCoords;
            }
        }
        return CurrentCubeCoords; // 적절한 위치 못 찾으면 건물 위치
    }

    // SimulatedObject로부터 상속받은 TakeDamage, OnDeath 등도 건물에 맞게 작동
    public override void TakeDamage(int damageAmount)
    {
        if (!isConstructed && CurrentActionState != UnitActionState.Dead) // 건설 중일 때도 데미지 받음
        {
            // 건설 중 피해 처리: 건설 진행률을 깎거나, 체력을 더 빨리 닳게 하거나, 일꾼이 수리해야 하도록.
            // 간단히는 그냥 체력만 깎음.
        }
        base.TakeDamage(damageAmount); // 부모의 TakeDamage 호출
    }

    protected override void OnDeath()
    {
        base.OnDeath(); // 부모의 OnDeath 호출
        // 건물 파괴 시 추가 처리 (예: 잔해 남기기, 인구수 감소 알림)
        Debug.Log($"(ID: {InstanceID}) 파괴됨!");
        if (ProvidesSupply)
        {
            FindObjectOfType<ResourceManager>()?.UpdateResourceCap(OwnerPlayerId, "Supply", FindObjectOfType<ResourceManager>().GetResourceCap(OwnerPlayerId, "Supply") - SupplyProvided);
        }
        // BuildingManager에 파괴 알림
        //FindObjectOfType<BuildingManager>()?.NotifyBuildingDestroyed(this);
    }
}