using UnityEngine;
using System.Collections.Generic;

public class Building : SimulatedObject // SimulatedObject ���
{
    [Header("Building State")]
    public bool isConstructed = false;
    public int currentConstructionProgressTicks = 0; // ������� ����� �Ǽ� ƽ

    public bool CanProduceUnits { get; protected set; }
    public List<string> ProducibleUnitDataIDs { get; protected set; } // ���� ������ ���� ������ ID ���
    public int UnitProductionQueueSize { get; protected set; } // �ִ� ���� ��⿭ ũ��

    public bool CanResearchUpgrades { get; protected set; }
    public List<string> ResearchableUpgradeDataIDs { get; protected set; } // ���� ������ ���׷��̵� ������ ID ���

    public bool ActsAsResourceDropOff { get; protected set; } // �ڿ� �ݳ� �ǹ� ���� (��: ����)
    public bool ProvidesSupply { get; protected set; }      // �α��� ���� ���� (��: ���ް�)
    public int SupplyProvided { get; protected set; } // �����ϴ� �α���


    // ���� ���� ���� (���� - ���� �ǹ��̶��)
    private Queue<string> _unitProductionQueue = new Queue<string>();
    private string _currentProducingUnitDataID = null;
    private int _currentUnitProductionProgressTicks = 0;

    // BuildingData�κ��� �ʱ�ȭ (SimulatedObject�� Initialize�� �������̵�)
    public override void Initialize(int instanceId, ulong ownerId, UnitData data, HexGridSystem gridSystem, UnitManager unitManagerRef, Vector3Int initialCubeCoords)
    {
        base.Initialize(instanceId, ownerId, data, gridSystem, unitManagerRef,  initialCubeCoords); // �θ� �ʱ�ȭ ȣ��

        // ���޹��� UnitData�� WorkerUnitData Ÿ������ Ȯ���ϰ� ĳ����
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
            Debug.LogWarning($"BuildingUnit {InstanceID}�� BuildingData�� �ƴ� �Ϲ� UnitData�� �Ҵ�Ǿ����ϴ�.");
        }

        isConstructed = false;
        currentConstructionProgressTicks = 0;

        // �Ǽ� ���� �� �ǹ��� ������ "�Ǽ� ��" ������� ���� (���� ����)
        UpdateConstructionVisuals(0f);
    }

    // GameLogicManager�� ���� �� ƽ ȣ��� �� ���� (�Ǵ� BuildingManager�� ȣ��)
    public new virtual void SimulateStep(float tickInterval) // new �Ǵ� override
    {
        // base.SimulateStep(tickInterval); // �θ��� ���� ���� (��: ��� üũ)

        if (!isConstructed)
        {
            // �Ǽ��� ���� �Ϸ���� �ʾҴٸ�, �߰� ���� ���� (�ϲ��� �Ǽ� ����)
            // �Ǵ�, ���⼭ �ϲ� ���̵� õõ�� �������� ���� �߰� ����
        }
        else // �Ǽ� �Ϸ� ��
        {
            if (CurrentActionState == UnitActionState.Dead) return; // �̹� �ı���

            // �ǹ� ���� ��� ���� (��: ���� ���� ����)
            if (CanProduceUnits)
            {
                UpdateUnitProduction(tickInterval);
            }
            // ��� Ÿ����� �ֺ� �� �ڵ� ���� ����
            // if (buildingData.attackDamage > 0) { HandleAttacking(tickInterval); }
        }
    }

    // �ϲۿ� ���� �Ǽ� ���� (��������)
    public bool AdvanceConstruction(int buildPowerPerTick) // �ϲ��� �Ǽ� �ɷ�ġ
    {
        if (isConstructed) return true;

        currentConstructionProgressTicks += buildPowerPerTick;
        float progressRatio = (float)currentConstructionProgressTicks / CreationTimeInTicks;
        UpdateConstructionVisuals(progressRatio); // �Ǽ� ���࿡ ���� �ð��� ��ȭ

        // �Ǽ� ���࿡ ���� ���� ü�µ� ������ ������Ű�� ���� �Ϲ���
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
        CurrentHealth = MaxHealth; // �Ǽ� �Ϸ� �� ü�� �ִ��
        currentConstructionProgressTicks = CreationTimeInTicks; // ��Ȯ�� ����
        Debug.Log($"(ID: {InstanceID}) �Ǽ� �Ϸ�!");
        UpdateConstructionVisuals(1f); // ���� �������

        // �Ǽ� �Ϸ� �̺�Ʈ �߻� (BuildingManager�� ResourceManager�� �˸� - ��: �α��� ����)
        if (ProvidesSupply)
        {
            FindObjectOfType<ResourceManager>()?.UpdateResourceCap(OwnerPlayerId, "Supply", FindObjectOfType<ResourceManager>().GetResourceCap(OwnerPlayerId, "Supply") + SupplyProvided);
        }
    }

    private void UpdateConstructionVisuals(float progressRatio)
    {
        // TODO: �Ǽ� ������� ���� �ǹ��� ������ �����ϴ� ����
        // ��: ���� ����, ���������� ���� ��Ÿ���ų�, �Ǽ� �ִϸ��̼� ���
        // �������� ü�¹� UI�� ����� ǥ��
        // transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1.0f, progressRatio); // �ӽ� ������ ����
    }

    // --- ���� ���� ���� �Լ� (���� �ǹ��̶��) ---
    public bool AddToProductionQueue(string unitDataID)
    {
        if (!isConstructed || !CanProduceUnits || !ProducibleUnitDataIDs.Contains(unitDataID))
        {
            return false; // �Ǽ� �ȵưų�, ���� �Ұ� �ǹ��̰ų�, ���� ���ϴ� ����
        }
        if (_unitProductionQueue.Count >= UnitProductionQueueSize)
        {
            Debug.LogWarning($"�ǹ� {InstanceID}: ���� ���� ť�� ���� á���ϴ�.");
            return false; // ť �� ��
        }

        _unitProductionQueue.Enqueue(unitDataID);
        Debug.Log($"�ǹ� {InstanceID}: ���� {unitDataID} ���� ť�� �߰���.");
        return true;
    }

    private void UpdateUnitProduction(float tickInterval)
    {
        if (_currentProducingUnitDataID == null) // ���� ���� ���� ������ ���ٸ�
        {
            if (_unitProductionQueue.Count > 0) // ť�� ��� ���� ������ �ִٸ�
            {
                _currentProducingUnitDataID = _unitProductionQueue.Dequeue();
                _currentUnitProductionProgressTicks = 0;
                // UnitData�� �����ͼ� ���� �ð� ���� (�����δ� UnitManager�� UnitData ����)
                // UnitData producingUnitData = FindObjectOfType<UnitManager>().GetUnitDataByID(_currentProducingUnitDataID);
                // if (producingUnitData == null) { _currentProducingUnitDataID = null; return; }
                // _targetProductionTicks = producingUnitData.productionTimeInTicks;
                Debug.Log($"�ǹ� {InstanceID}: ���� {_currentProducingUnitDataID} ���� ����.");
            }
        }
        else // ���� ���� ���� ������ �ִٸ�
        {
            _currentUnitProductionProgressTicks++;
            //UnitData producingUnitData = FindObjectOfType<UnitManager>()?.GetUnitDataByID(_currentProducingUnitDataID); // �ӽ� ����
            //if (producingUnitData != null && _currentUnitProductionProgressTicks >= GetProductionTimeInTicks(producingUnitData)) // ���� �ð��� UnitData�� �־�� ��
            //{
                // ���� �Ϸ�!
            //    FindObjectOfType<UnitManager>()?.CreateUnit(_currentProducingUnitDataID, GetRallyPointOrDefault(), OwnerPlayerId); // RallyPoint �Ǵ� �ǹ� ���� ����
            //    Debug.Log($"�ǹ� {InstanceID}: ���� {_currentProducingUnitDataID} ���� �Ϸ�!");
            //    _currentProducingUnitDataID = null;
            //    _currentUnitProductionProgressTicks = 0;
                // ���� ���� ���� �õ� (���� ƽ��)
            //}
        }
    }

    // �ӽ�: ���� �����Ϳ��� ���� �ð� �������� �Լ� (�����δ� UnitManager�� UnitData�� �����ϰ� �� ���� ����)
    private int GetProductionTimeInTicks(UnitData unitData)
    {
        // return unitData.productionTimeInTicks; // UnitData�� �� �ʵ尡 �ִٰ� ����
        return 100; // �ӽ� ������
    }

    private Vector3Int GetRallyPointOrDefault()
    {
        // TODO: �ǹ��� ���� ����(Rally Point) ��ȯ ����. ������ �ǹ� �ֺ� �� Ÿ��.
        // ������ �ӽ÷� �ǹ� �� ĭ ��ȯ
        if (_hexGridSystemRef != null)
        {
            var neighbors = _hexGridSystemRef.GetNeighbors(CurrentCubeCoords);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.isWalkable) return neighbor.cubeCoords;
            }
        }
        return CurrentCubeCoords; // ������ ��ġ �� ã���� �ǹ� ��ġ
    }

    // SimulatedObject�κ��� ��ӹ��� TakeDamage, OnDeath � �ǹ��� �°� �۵�
    public override void TakeDamage(int damageAmount)
    {
        if (!isConstructed && CurrentActionState != UnitActionState.Dead) // �Ǽ� ���� ���� ������ ����
        {
            // �Ǽ� �� ���� ó��: �Ǽ� ������� ��ų�, ü���� �� ���� ��� �ϰų�, �ϲ��� �����ؾ� �ϵ���.
            // �������� �׳� ü�¸� ����.
        }
        base.TakeDamage(damageAmount); // �θ��� TakeDamage ȣ��
    }

    protected override void OnDeath()
    {
        base.OnDeath(); // �θ��� OnDeath ȣ��
        // �ǹ� �ı� �� �߰� ó�� (��: ���� �����, �α��� ���� �˸�)
        Debug.Log($"(ID: {InstanceID}) �ı���!");
        if (ProvidesSupply)
        {
            FindObjectOfType<ResourceManager>()?.UpdateResourceCap(OwnerPlayerId, "Supply", FindObjectOfType<ResourceManager>().GetResourceCap(OwnerPlayerId, "Supply") - SupplyProvided);
        }
        // BuildingManager�� �ı� �˸�
        //FindObjectOfType<BuildingManager>()?.NotifyBuildingDestroyed(this);
    }
}