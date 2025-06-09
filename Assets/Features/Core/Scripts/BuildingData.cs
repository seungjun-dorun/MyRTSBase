using UnityEngine;
using System.Collections.Generic; // ResourceCost 사용 시

// UnitData.cs 에 있던 ResourceCost 구조체를 여기로 옮기거나 공용으로 사용

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "RTS/Building Data")]
public class BuildingData : UnitData
{

    [Header("Functionality (Examples)")]
    public bool canProduceUnits = false;
    public List<string> producibleUnitDataIDs; // 생산 가능한 유닛 데이터 ID 목록
    public int unitProductionQueueSize = 5;

    public bool canResearchUpgrades = false;
    public List<string> researchableUpgradeDataIDs;

    public bool actsAsResourceDropOff = false; // 자원 반납 건물 여부 (예: 본진)
    public bool providesSupply = false;      // 인구수 제공 여부 (예: 보급고)
    public int supplyProvided = 0;

    // ... 기타 건물 특화 데이터 ...
}