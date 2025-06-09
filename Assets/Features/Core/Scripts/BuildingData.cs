using UnityEngine;
using System.Collections.Generic; // ResourceCost ��� ��

// UnitData.cs �� �ִ� ResourceCost ����ü�� ����� �ű�ų� �������� ���

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "RTS/Building Data")]
public class BuildingData : UnitData
{

    [Header("Functionality (Examples)")]
    public bool canProduceUnits = false;
    public List<string> producibleUnitDataIDs; // ���� ������ ���� ������ ID ���
    public int unitProductionQueueSize = 5;

    public bool canResearchUpgrades = false;
    public List<string> researchableUpgradeDataIDs;

    public bool actsAsResourceDropOff = false; // �ڿ� �ݳ� �ǹ� ���� (��: ����)
    public bool providesSupply = false;      // �α��� ���� ���� (��: ���ް�)
    public int supplyProvided = 0;

    // ... ��Ÿ �ǹ� Ưȭ ������ ...
}