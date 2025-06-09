using RTSGame.Commands; // �ʿ�� CommandSystem ����
using System.Collections.Generic;
using UnityEngine;

public class WorkerUnit : SimulatedObject // SimulatedObject ���
{
    [Header("Worker Specific Stats")]
    public int maxCarryCapacity { get; private set; } = 50; // �⺻��, UnitData���� ��� �� ����
    public int ticksPerGatherAction { get; private set; } = 20; // �⺻��

    [Header("Worker State")]
    public int currentCarryingAmount { get; private set; }
    public string carryingResourceType { get; private set; } // ���� ��� ���� �ڿ� ����
    public ResourceNode targetResourceNode { get; private set; } // ���� ��ǥ �ڿ� ���
    public SimulatedObject targetDropOffBuilding { get; private set; } // ���� ��ǥ �ڿ� �ݳ� �ǹ� (��: ����)

    private int _actionProgressTicks = 0; // ä��/�Ǽ� �� �۾� ���� ƽ ī����



    public override void Initialize(int instanceId, ulong ownerId, UnitData data, HexGridSystem gridSystem, UnitManager unitManager, Vector3Int initialCubeCoords)
    {
        base.Initialize(instanceId, ownerId, data, gridSystem, unitManager, initialCubeCoords); // �θ��� Initialize ȣ��

        // ���޹��� UnitData�� WorkerUnitData Ÿ������ Ȯ���ϰ� ĳ����
        if (data is WorkerUnitData workerData)
        {
            maxCarryCapacity = workerData.maxCarryCapacity;
            ticksPerGatherAction = workerData.ticksPerGatherAction;
            currentCarryingAmount = 0;
            carryingResourceType = null;
            targetResourceNode = null;
            targetDropOffBuilding = null;


            Debug.Log($"WorkerUnit {InstanceID} initialized with WorkerData: CarryCap={maxCarryCapacity}, GatherTicks={ticksPerGatherAction}");
        }
        else
        {
            // WorkerUnit �����տ� �Ϲ� UnitData�� �Ҵ�� ��� ��� �Ǵ� �⺻�� ���
            Debug.LogWarning($"WorkerUnit {InstanceID}�� WorkerUnitData�� �ƴ� �Ϲ� UnitData�� �Ҵ�Ǿ����ϴ�. �ϲ� Ưȭ �ɷ�ġ�� �⺻���� ���� �� �ֽ��ϴ�.");
            // �⺻�� ���� (WorkerUnit Ŭ������ ���ǵ� �⺻�� ��� �Ǵ� ���⼭ ���)
            // this.maxCarryCapacity = 50; // ���� �⺻��
            // this.ticksPerGatherAction = 20;
        }
    }


    // SimulateStep�� �������̵��ϰų�, ���� SimulateStep���� worker ���¸� ó���ϵ��� Ȯ��
    // ���⼭�� ������ ���� �ֿ� ���� ������ �� Ŭ������ ����
    public override void SimulateStep(float tickInterval)
    {
        base.SimulateStep(tickInterval);

        // Worker ���� ���� ����
        switch (CurrentActionState)
        {

            case UnitActionState.GatheringResource:
                HandleGatheringResourceState(tickInterval);
                break;
            //case UnitActionState.ReturningResource:
                HandleReturningResourceState(tickInterval);
                break;
            //case UnitActionState.MovingToBuild: // �ǹ� �Ǽ� ��ġ�� �̵� ��
                HandleMovingToBuildState(tickInterval);
                break;
            case UnitActionState.Building:
                HandleBuildingState(tickInterval);
                break;

            default:
                Debug.LogWarning($"SimulatedObject: ���� {CurrentActionState}�� ���� �ڵ鷯�� �����ϴ�.");
                break;
        }

        // �θ��� SimulateStep�� ���� ������ �ִٸ� ȣ��
        // (��: ���� ��ٿ� ���Ҵ� ��� ���� �����̹Ƿ� �θ𿡼� ó��)
        // base.SimulateStep(tickInterval); // ���� �θ� ������ �� �������� �;� �Ѵٸ� ���⿡
    }

    #region Worker State Handlers

    // Moving, Attacking ���� �ڵ鷯�� SimulatedObject�� ���� ����ϰų�,
    // Worker �������� �������̵�/Ȯ�� ����. ���⼭�� �θ���� ���ٰ� ����.

    private void HandleGatheringResourceState(float tickInterval)
    {
        if (targetResourceNode == null || targetResourceNode.IsDepleted)
        {
            // ��ǥ ��尡 ���ų� ��. �ݳ� ������ �ڿ��� �ִٸ� �ݳ��Ϸ�, ���ٸ� Idle.
            if (currentCarryingAmount > 0)
            {
                SetReturnResourceCommand(FindNearestDropOff()); // ���� ����� �ݳ� �ǹ� ã�� (���� �ʿ�)
            }
            else
            {
                CurrentActionState = UnitActionState.Idle;
            }
            return;
        }

        // ��ǥ �ڿ� ��忡 �����ߴ��� Ȯ�� (SimulatedObject�� �̵� �������� ��ǥ ���� �� ���� ���� ����)
        // ���⼭�� �̹� �ڿ� ��忡 ������ �ִ� ���¶�� �����ϰ� ä�� ���� ����

        SimulatedObject nodeSimObject = targetResourceNode.GetComponent<SimulatedObject>();
        if (nodeSimObject != null)
        {
            // Vector3Int.Distance�� float�� ��ȯ�մϴ�.
            // ��� �׸��忡�� ť�� ��ǥ�� ������ Ÿ��(��: (0,0,0)�� (1,0,-1))���� Vector3Int.Distance�� sqrt(2) approx 1.414f �Դϴ�.
            // ���� "1���� ũ��"�� ������ ������ Ÿ�ϵ� ���� �˴ϴ�.
            // "�������� ���� ���" (��, �Ÿ��� 1�� Ÿ�Ϻ��� �� ���)�� Ȯ���Ϸ��� �Ӱ谪�� �����ؾ� �մϴ�.
            // ���� ���, �Ÿ��� 1.5f (�Ǵ� Sqrt(2) + ���� epsilon ��) ���� ũ�� �������� �ʴٰ� �Ǵ��� �� �ֽ��ϴ�.
            // ���⼭�� ���� ������ �ǵ��� �ִ��� �����ϸ� ������ ������ �����մϴ�.
            // ���� ��Ȯ�� ��� �׸��� Ÿ�� �Ÿ��� �ʿ��ϴٸ� _hexGridSystemRef.GetDistance() ����� �����մϴ�.
            float distance = Vector3Int.Distance(CurrentCubeCoords, nodeSimObject.CurrentCubeCoords);
            if (distance > 1.0f) // ���� �ڵ��� ���� 1�� ���ϴ� ���� float 1.0f�� ����� ����.
                                 // ���� Ÿ��(�Ÿ� ~1.414f)�� �� ���ǿ� �ش�˴ϴ�.
                                 // ���� "�������� ���� ��쿡��" �̶�� �ǵ����ٸ� if (distance > 1.5f) �Ǵ� if (_hexGridSystemRef.GetDistance(CurrentCubeCoords, nodeSimObject.CurrentCubeCoords) > 1) ���
            {
                // ���� ���� ���� -> �̵� ���·� ���� (�̵� ������ �� ���¸� �����ϰ� ó���ؾ� ��)
                // �� �κ��� ���� ���̰� �������� �� ����.
                // ������ MovingToGather ���¿��� �����ϸ� GatheringResource ���·� �����.
                // ���⼭�� �̹� �����ߴٰ� ����.
                Debug.LogWarning($"WorkerUnit {InstanceID} in GatheringResource state but not strictly adjacent (distance: {distance}) to target node {targetResourceNode.name}. Current: {CurrentCubeCoords}, Target: {nodeSimObject.CurrentCubeCoords}. Consider using HexGridSystem.GetDistance for precise hex distance.");
                // CurrentState = UnitState.Idle; // �Ǵ� ���̵� ���
                return; // �߰� ���� �ߴ�
            }
        }
        else
        {
            // ResourceNode�� SimulatedObject ������Ʈ�� ���� ���.
            Debug.LogError($"WorkerUnit {InstanceID}: Target resource node {targetResourceNode.name} is missing SimulatedObject component. Returning to Idle.");
            CurrentActionState = UnitActionState.Idle;
            targetResourceNode = null; // ��ǥ �ʱ�ȭ
            return; // �߰� ���� �ߴ�
        }

        if (currentCarryingAmount >= maxCarryCapacity)
        {
            // �̹� �� ��. �ݳ��Ϸ� �̵�.
            SetReturnResourceCommand(FindNearestDropOff());
            return;
        }

        int canCarryMore = maxCarryCapacity - currentCarryingAmount;
        int desiredAmountThisTick = Mathf.Min(targetResourceNode.amountPerGatherTick, canCarryMore);

        if (desiredAmountThisTick > 0)
        {
            int gatheredAmount = targetResourceNode.GatherResource(desiredAmountThisTick);
            if (gatheredAmount > 0)
            {
                // �ڿ� ���� ��ġ Ȯ�� �� ó��
                if (string.IsNullOrEmpty(carryingResourceType) || carryingResourceType == targetResourceNode.GetResourceType())
                {
                    currentCarryingAmount += gatheredAmount;
                    carryingResourceType = targetResourceNode.GetResourceType();
                }
                else
                {
                    Debug.LogWarning($"�ϲ� {InstanceID}: �ٸ� ������ �ڿ�({targetResourceNode.GetResourceType()}) ä�� �õ�. ���� ����: {carryingResourceType}");
                    // ���� �ڿ��� �����ų�, ä�� �ߴ�
                    currentCarryingAmount = gatheredAmount; // ���� ĵ �ڿ����� ����� (������ ó��)
                    carryingResourceType = targetResourceNode.GetResourceType();
                }
            }
        }
    }
    private void HandleReturningResourceState(float tickInterval)
    {
        if (targetDropOffBuilding == null) // �ݳ��� �ǹ��� �������� �ʾ�����
        {
            targetDropOffBuilding = FindNearestDropOff(); // ���� ����� �ݳ� �ǹ� ã��
            if (targetDropOffBuilding == null)
            {
                Debug.LogWarning($"�ϲ� {InstanceID}: �ڿ��� �ݳ��� �ǹ��� ã�� �� �����ϴ�. Idle ���·� ����.");
                CurrentActionState = UnitActionState.Idle;
                return;
            }
            // ��ǥ �ǹ��� ���� �̵� ��� (SimulatedObject�� SetMoveCommand ���)
            // List<Vector3Int> pathToDropOff = ... A*�� ��� ã�� ...
            // SetMoveCommand(pathToDropOff);
            // CurrentState�� ������ ReturningResource, �̵� ������ �� ���¸� ���� ó��
            // �� ���������� �̵��� �̹� �Ϸ�Ǿ���, �ݳ� �ǹ��� �����ߴٰ� ����.
        }

        // ��ǥ �ݳ� �ǹ��� �����ߴ��� Ȯ��
        // ���⼭�� �̹� �����ߴٰ� �����ϰ� �ݳ� ���� ����
        if (Vector3Int.Distance(CurrentCubeCoords, targetDropOffBuilding.CurrentCubeCoords) <= 1) // �����ߴٰ� ����
        {
            if (currentCarryingAmount > 0 && !string.IsNullOrEmpty(carryingResourceType))
            {
                // ResourceManager�� �ڿ� �߰� ��û
                FindObjectOfType<ResourceManager>().AddResource(OwnerPlayerId, carryingResourceType, currentCarryingAmount); // ResourceManager ���� ��� ���� �ʿ�
                Debug.Log($"�ϲ� {InstanceID}: {currentCarryingAmount} {carryingResourceType} �ݳ� �Ϸ�.");
                currentCarryingAmount = 0;
                carryingResourceType = null;
            }

            // �ڿ� �ݳ� �� ���� �ൿ ���� (��: ���� �ڿ� ���� ���ư��ų�, Idle)
            if (targetResourceNode != null && !targetResourceNode.IsDepleted)
            {
                SetGatherResourceCommand(targetResourceNode); // ���� �ڿ� ���� �ٽ�
            }
            else
            {
                CurrentActionState = UnitActionState.Idle; // �Ǵ� ���� ����� �ٸ� �ڿ� ��� ã��
            }
        }
        else // ���� �ݳ� �ǹ��� ���� ����
        {
            // �̵� ���� (SimulatedObject�� HandleMovement)�� ó���ϵ��� ��.
            // �� ����(ReturningResource)������ �̵� ��ǥ�� targetDropOffBuilding�� �Ǿ�� ��.
            // SetMoveCommand�� ���� ��θ� �����ϰ� HandleMovement�� �۵��ϵ��� �ؾ� ��.
            // HandleMovement(tickInterval); // �̷��� ���� ȣ���ϴ� �ͺ��� ���� ������� SimulatedObject.SimulateStep���� �б��ϴ°� ����
        }
    }
    private void HandleMovingToBuildState(float tickInterval)
    {
        // �ǹ� �Ǽ� ��ġ�� �̵��ϴ� ���� (SimulatedObject�� HandleMovement ���)
        // ��ǥ ������ �����ϸ� CurrentState�� Building���� ����
        HandleMovement(tickInterval); // �θ��� �̵� ���� ���
        if (currentPath == null || _currentPathIndex >= currentPath.Count) // ��� �̵� �Ϸ�
        {
            // CurrentState = UnitState.Building;
            // _gatherTicksCounter = 0; // �Ǽ� �ð� ī���� �ʱ�ȭ
            // targetBuildSite.StartConstructionByWorker(this); // �Ǽ� ��� �ǹ��� �Ǽ� ���� �˸�
            Debug.Log($"�ϲ� {InstanceID}: �Ǽ� ��ġ ����. �Ǽ� ���� �غ�.");
            // ���� ���� ������ GameLogicManager�� BuildBuildingCommand ���� �� �̷���� �� ����.
            // �ϲ��� �̵� �Ϸ� �� ����ϰ�, �ǹ��� �����Ǹ� Building ���·� ����� �� ����.
        }
    }
    private void HandleBuildingState(float tickInterval)
    {
        // �ǹ� �Ǽ� ���� ����
        // _gatherTicksCounter++;
        // if (_gatherTicksCounter >= TicksPerBuildAction) // TicksPerBuildAction�� �Ǽ� �ൿ�� ƽ ��
        // {
        //    _gatherTicksCounter = 0;
        //    bool constructionFinished = targetBuildSite.AdvanceConstruction(BuildPowerPerAction);
        //    if(constructionFinished) { CurrentState = UnitState.Idle; }
        // }
        Debug.Log($"�ϲ� {InstanceID}: �ǹ� �Ǽ� ��...");
    }


    #endregion

    #region Command Setters (GameLogicManager���� ȣ���)

    public void SetGatherResourceCommand(ResourceNode resourceNode)
    {
        if (resourceNode == null || resourceNode.IsDepleted)
        {
            CurrentActionState = UnitActionState.Idle;
            return;
        }
        targetResourceNode = resourceNode;
        targetDropOffBuilding = null; // ���� �ݳ� ��ǥ �ʱ�ȭ
        currentCarryingAmount = 0; // �ڿ� ä�� ���� �� ���� ��� �ִ� �� �ʱ�ȭ (�ٸ� ���� �ڿ� ����)
        carryingResourceType = null;

        // �ڿ� ���� �̵��ϱ� ���� ��� ����
        // �� �κ��� GameLogicManager�� ExecuteCommand���� ��� Ž�� �� SetMoveCommand�� ȣ���ϴ� ������� ���յǾ�� ��.
        // ���⼭�� ���� ���游 ����ϰ�, �̵��� SetMoveCommand�� ���� �̷����.
        // CurrentState = UnitState.MovingToGather; // �� ���� ���� �ʿ� �Ǵ� Moving ���¿��� ��ǥ Ÿ�� ����
        // List<Vector3Int> pathToNode = ... A*�� ��� ã�� ...
        // SetMoveCommand(pathToNode);

        // �ӽ�: ���¸� �����ϰ�, �̵��� GameLogicManager���� SetMoveCommand�� ���۽�Ų�ٰ� ����.
        // �����δ� GameLogicManager�� �� �Լ��� ȣ���ϱ� ���� ���ֿ��� �̵� ����� ���� ����.
        // �� �Լ��� ������ �ڿ� ��忡 "�������� ��" GameLogicManager�� ȣ���Ͽ� ���¸� �����Ű�� �뵵�� �� ����.
        // �Ǵ�, �� �Լ��� ȣ��Ǹ� ������ ������ ��θ� ã�� �̵��ϵ��� �� ���� ����.
        // ���� ���迡���� GameLogicManager�� ��θ� ã�� unit.SetMoveCommand(path)�� ȣ���ϰ�,
        // �� �� unit.CurrentState = UnitState.GatheringResource (���� ��) �Ǵ� unit.CurrentState = UnitState.Moving (��ǥ�� �ڿ����) ������ ����.

        // ���� ����� �帧:
        // 1. PlayerInputHandler -> GatherResourceCommand (targetNode) -> GameLogicManager
        // 2. GameLogicManager: ���Ž��(unit.Pos -> targetNode.Pos) -> unit.SetMoveCommand(path)
        // 3. GameLogicManager: unit.SetTask_Gather(targetNode) -> unit.CurrentState = UnitState.Moving (���� ��ǥ�� ä��)
        // 4. unit.SimulateStep (Moving ����): ��� ���� �̵�. ��� �� ���� ��, ���� ��ǥ�� ä���̸� -> unit.CurrentState = UnitState.GatheringResource
        CurrentActionState = UnitActionState.GatheringResource; // �ӽ÷� �ٷ� ä�� ���·�. �����δ� �̵� �� ����.
        Debug.Log($"�ϲ� {InstanceID} �ڿ� ä�� ��� ����: {resourceNode.name}");
    }

    public void SetReturnResourceCommand(SimulatedObject dropOffBuilding)
    {
        if (dropOffBuilding == null || currentCarryingAmount <= 0)
        {
            CurrentActionState = UnitActionState.Idle; // �ݳ��� �ڿ��� ���ų�, �ݳ�ó�� ������ ���
            return;
        }
        targetDropOffBuilding = dropOffBuilding;
        targetResourceNode = null; // ���� ä�� ��ǥ �ʱ�ȭ

        // �ݳ� �ǹ��� �̵� (���� �����ϰ� GameLogicManager���� ��� ���� �� �̵� ���)
        // CurrentState = UnitState.MovingToReturn;
        //CurrentActionState = UnitActionState.ReturningResource; // �ӽ÷� �ٷ� �ݳ� ���·�.
        Debug.Log($"�ϲ� {InstanceID} �ڿ� �ݳ� ��� ����: {dropOffBuilding.name}");
    }

    // �ǹ� �Ǽ� ����� GameLogicManager���� BuildBuildingCommand�� ó���� ��,
    // �ϲۿ��� �̵� ���(�Ǽ� ��ġ����)�� ������, ���� �� �Ǽ� ���·� ����.
    // public void SetBuildCommand(BuildSite buildSite, Vector3Int buildLocation) { ... }

    #endregion

    #region Helper Methods

    private SimulatedObject FindNearestDropOff()
    {
        // TODO: UnitManager�� BuildingManager�� ���� ���� �÷��̾��� ��ȿ�� �ڿ� �ݳ� �ǹ�(���� ��) �� ���� ����� ���� ã�� ��.
        // ������ �ӽ÷� ù ��° ������ �ݳ� �ǹ��� ���� (�ſ� ����Ȯ)
        // return FindObjectOfType<UnitManager>()?.GetUnitById(0); // �ſ� �ӽ����� �ڵ�
        // �����δ� BuildingManager�� ���� "����" Ÿ���� �ǹ��� ã�ƾ� ��.
        var buildings = FindObjectsOfType<Building>(); // Building Ÿ���� �ִٰ� ����
        Building nearestDropOff = null;
        float minDistance = float.MaxValue;

        if (_hexGridSystemRef == null) _hexGridSystemRef = FindObjectOfType<HexGridSystem>();


        foreach (Building building in buildings)
        {
            if (building.OwnerPlayerId == this.OwnerPlayerId && building.ActsAsResourceDropOff) // Building�� canBeDropOffPoint ���� �÷��� �ʿ�
            {
                // �ǹ��� SimulatedObject�� ����ϰų�, CurrentCubeCoords�� ������ ��.
                int dist = _hexGridSystemRef.GetDistance(this.CurrentCubeCoords, building.GetComponent<SimulatedObject>().CurrentCubeCoords);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestDropOff = building;
                }
            }
        }
        return nearestDropOff?.GetComponent<SimulatedObject>(); // Building�� SimulatedObject�� ������ �ִٰ� ����
    }

    #endregion
}