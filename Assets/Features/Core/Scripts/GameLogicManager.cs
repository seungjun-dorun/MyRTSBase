using RTSGame.Commands; // CommandSystem���� ������ ���ӽ����̽� ��� (CommandSystem.cs ���� ����)
using System.Collections.Generic;
using UnityEngine;

public class GameLogicManager : MonoBehaviour
{
    [Header("Manager References")]
    [Tooltip("�ð� �� ƽ ������ ����ϴ� �Ŵ����Դϴ�.")]
    public DeterministicTimeManager timeManager; // Inspector���� �Ҵ�
    [Tooltip("���� ����, ����, ������Ʈ�� ����ϴ� �Ŵ����Դϴ�.")]
    public UnitManager unitManager;             // Inspector���� �Ҵ�

    /*
    [Tooltip("�ǹ� ���� ������ ����ϴ� �Ŵ����Դϴ�. (���� ����)")]
    public BuildingManager buildingManager;     // Inspector���� �Ҵ� (�ʿ��)
    [Tooltip("�ڿ� ���� ������ ����ϴ� �Ŵ����Դϴ�. (���� ����)")]
    public ResourceManager resourceManager;       // Inspector���� �Ҵ� (�ʿ��)
    [Tooltip("Flow Field ��� Ž���� ����ϴ� �Ŵ����Դϴ�. (���� ����)")]
    public FlowFieldManager flowFieldManager;   // Inspector���� �Ҵ� (FlowField ��� ��)
    */
    [Tooltip("���� �׸��� �ý����Դϴ�.")]
    public HexGridSystem hexGridSystem;         // Inspector���� �Ҵ�

    // --- ��� ó�� ����---
    // Key: ����� ƽ(ExecutionTick), Value: �ش� ƽ�� ����� ��� ����Ʈ
    private Dictionary<ulong, List<ICommand>> _scheduledCommands = new Dictionary<ulong, List<ICommand>>();

    // --- AI ���� (���� ����) ---
    [Header("AI Settings (Optional)")]
    [Tooltip("AI ������ �� ƽ���� �������� �����մϴ�.")]
    public int aiUpdateIntervalTicks = 10; // ��: 10ƽ���� AI ����
    private ulong _lastAiUpdateTick = 0;

    #region Unity Lifecycle & Initialization

    void Awake()
    {
        // �ʼ� ���� Ȯ��
        if (timeManager == null)
            Debug.LogError("GameLogicManager: DeterministicTimeManager�� �Ҵ���� �ʾҽ��ϴ�!");
        if (unitManager == null)
            Debug.LogError("GameLogicManager: UnitManager�� �Ҵ���� �ʾҽ��ϴ�!");
        if (hexGridSystem == null)
            Debug.LogError("GameLogicManager: HexGridSystem�� �Ҵ���� �ʾҽ��ϴ�!");
        // ������ ������ null�� �� �����Ƿ�, ��� ���� null üũ �ʿ�
    }

    #endregion

    #region Command Processing (�̱� �÷��̾��)

    /// <summary>
    /// ���� �÷��̾��� �Է��� ���� ������ ����� ������ ť�� �߰��մϴ�.
    /// (�̱� �÷��̾���� PlayerInputHandler�� �� �Լ��� ���� ȣ��)
    /// </summary>
    public void ProcessLocalCommand(ICommand command)
    {
        if (command == null)
        {
            Debug.LogWarning("GameLogicManager: null ����� �����ٸ��Ϸ��� �õ��߽��ϴ�.");
            return;
        }

        ulong executionTick = command.ExecutionTick;

        if (!_scheduledCommands.ContainsKey(executionTick))
        {
            _scheduledCommands[executionTick] = new List<ICommand>();
        }
        _scheduledCommands[executionTick].Add(command);

        Debug.Log($"[GameLogic] Command Queued: {command.Type} for Tick {executionTick}");
    }

    #endregion

    #region Tick Processing

    /// <summary>
    /// DeterministicTimeManager�� ���� �� ƽ ȣ��˴ϴ�.
    /// �ش� ƽ�� ���� ������ ���������� ó���մϴ�.
    /// </summary>
    public void ProcessGameLogicForTick(ulong currentTick, float tickInterval)
    {
        // 1. �����ٵ� ��� ����
        ExecuteScheduledCommands(currentTick);

        // 2. ���� ���� ������Ʈ (�̵�, ���� �� �⺻ �ൿ)
        if (unitManager != null)
        {
            unitManager.UpdateUnitsForTick(tickInterval); // UnitManager�� ���������� ���ֵ��� SimulateStep ȣ��
        }
        /*
        // 3. �ǹ� ���� ������Ʈ (����, ���� ��)
        if (buildingManager != null)
        {
            // buildingManager.UpdateBuildingsForTick(currentTick, tickInterval);
        }

        // 4. �ڿ� ���� ������Ʈ (�ڿ��� ��ȭ ��)
        if (resourceManager != null)
        {
            // resourceManager.UpdateResourcesForTick(currentTick, tickInterval);
        }
        */
        // 5. AI ���� ������Ʈ (���� ��������)
        if (currentTick >= _lastAiUpdateTick + (ulong)aiUpdateIntervalTicks)
        {
            // ProcessAIMainLogic(currentTick);
            _lastAiUpdateTick = currentTick;
        }

        // ... ��Ÿ ƽ ��� ���� �ý��� ������Ʈ ...
        // ��: ���� ��ȭ, �ֱ����� �̺�Ʈ �߻� ��
    }

    /// <summary>
    /// ���� ƽ�� �����ٵ� ��� ����� �����մϴ�.
    /// </summary>
    private void ExecuteScheduledCommands(ulong currentTick)
    {
        if (_scheduledCommands.TryGetValue(currentTick, out List<ICommand> commandsToExecute))
        {
            // �߿�: ��� ���� ������ ���� ����� ������ ��ĥ �� �����Ƿ�,
            // �ʿ��ϴٸ� �ϰ��� �������� �����ؾ� �մϴ�. (��: �÷��̾� ID, ��� Ÿ�� ��)
            // ���⼭�� ���ŵ� ������� �����Ѵٰ� �����մϴ�.
            // commandsToExecute.Sort((a, b) => /* ���� ���� */ );

            for (int i = 0; i < commandsToExecute.Count; i++)
            {
                ExecuteSingleCommand(commandsToExecute[i]);
            }

            // ����� ��� ����Ʈ�� ����
            _scheduledCommands.Remove(currentTick);
        }
    }

    /// <summary>
    /// ���� ����� ������ �����ϴ� �����Դϴ�.
    /// </summary>
    private void ExecuteSingleCommand(ICommand command)
    {
        // Debug.Log($"[GameLogic] Executing Command: {command.Type} at Tick {timeManager.CurrentTick}");

        // ����� ������ ��ü(����/�ǹ�)���� �����ɴϴ�.
        List<SimulatedObject> actors = new List<SimulatedObject>();
        if (command.ActorInstanceIDs != null)
        {
            foreach (int actorId in command.ActorInstanceIDs)
            {
                SimulatedObject actor = unitManager.GetUnitById(actorId); // �Ǵ� BuildingManager ����� ã�ƾ� �� �� ����
                if (actor != null && actor.IsAlive()) // ����ִ� ���͸�
                {
                    actors.Add(actor);
                }
            }
        }

        // ��� ������ ���� �б��Ͽ� ó��
        switch (command.Type)
        {
            case CommandType.Move:
                MoveCommand moveCmd = (MoveCommand)command;

                if (actors.Count == 0)
                {
                    Debug.Log("MoveCommand: �̵��� ������ �����ϴ�.");
                    break; // switch �� �������� �Ѿ (���� ��� ó�� ����)
                }

                // // HexGridSystem�� �Ҵ���� �ʾ����� �����̼� ��� �Ұ�
                if (hexGridSystem == null)
                {
                    Debug.LogError("HexGridSystem is not assigned. Cannot calculate formation. Moving units individually to group target.");
                    // ��� ���ֿ��� ������ �׷� ��ǥ �������� ��� ��û
                    foreach (SimulatedObject unit in actors)
                    {
                        // *** �� �κе� RequestPathForUnit.FindPath�� ���� ***
                        // ��, �� ��� hexGridSystem�� null�̹Ƿ� RequestPathForUnit.FindPath�� ������ ����.
                        // ����, hexGridSystem�� null�̸� �ƿ� ��� Ž�� �õ��� �� �ϰų�,
                       // �ſ� �⺻���� ��ü ����(��: ��ǥ �������� ���� �̵� �õ� - ��ֹ� ����)�� �ؾ� ��.
                        // ���⼭�� ��� Ž�� ���п� �����ϰ� ó�� (Idle ���·�).
                        Debug.LogWarning($"���� {unit.InstanceID}: HexGridSystem ����� ��� Ž�� �Ұ�. �׷� ��ǥ: {moveCmd.TargetCubeCoord}");
                        unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                        unit.currentPath = null;
                    }
                    break;
                }

                // �����̼� ���� �Ķ���� (MoveCommand�� �� ������ �ִٸ� ���, ���ٸ� �⺻��)
                string formationType = "Spiral"; // �⺻�� �Ǵ� moveCmd.FormationType
                int spacing = 1;                // �⺻�� �Ǵ� moveCmd.FormationSpacing

                // FormationUtility�� ����Ͽ� �� ������ ���� ��ǥ ���� ���
                List<Vector3Int> individualTargetCoords = FormationUtility.CalculateIndividualTargetPositions(
                    moveCmd.TargetCubeCoord,    // �׷��� �� ��ǥ ���� (Vector3Int)
                    actors,                     // �̵��� ���� ����Ʈ (List<SimulatedObject>)
                    hexGridSystem,              // HexGridSystem �ν��Ͻ� (HexGridSystem)
                    formationType,              // ���ϴ� �����̼� Ÿ�� (string)
                    spacing                     // ���� �� ���� (int)
                );

                // �� ���ֿ� ���� ���� ���� ��ǥ �������� ��� Ž�� ��û
                for (int i = 0; i < actors.Count; i++)
                {
                    SimulatedObject unit = actors[i];
                    Vector3Int unitSpecificTarget;
                    
                    // individualTargetCoords ����Ʈ�� �ε����� ��ȿ����,
                    // �׸��� �ش� ��ǥ�� ��ȿ�ϰ� ���� �� �ִ� Ÿ������ Ȯ��
                    if (i < individualTargetCoords.Count && hexGridSystem.IsValidHex(individualTargetCoords[i]) && hexGridSystem.GetTileAt(individualTargetCoords[i]).isWalkable)
                    {
                        unitSpecificTarget = individualTargetCoords[i];
                    }
                    else
                    {
                        Debug.LogWarning($"���� {unit.InstanceID}: �����̼� ��ġ { (i < individualTargetCoords.Count ? individualTargetCoords[i].ToString() : "N/A") } �� ��ȿ���� �ʰų� �����մϴ�. �׷� ��ǥ {moveCmd.TargetCubeCoord} �Ǵ� ���� ���� ��ġ ��� �õ�.");
                        // ��ü ��ǥ ����: �׷� ��ǥ �Ǵ�, �̹� �ش� ��ġ�� �ִٸ� ���� ��ġ ����
                        if(unit.CurrentCubeCoords == moveCmd.TargetCubeCoord) // �̹� �׷� ��ǥ�� �ִٸ�
                             unitSpecificTarget = unit.CurrentCubeCoords;
                        else // �׷� ��ǥ�� �õ�
                             unitSpecificTarget = moveCmd.TargetCubeCoord;

                        // �׷� ��ǥ�� ��ȿ���� ������ ���������� ���� ��ġ (��ǻ� �̵� ����)
                        if (!hexGridSystem.IsValidHex(unitSpecificTarget) || !hexGridSystem.GetTileAt(unitSpecificTarget).isWalkable) {
                            unitSpecificTarget = unit.CurrentCubeCoords;
                        }
                    }

                    // �̹� ��ǥ �����̸� ��� Ž�� ����
                    if (unit.CurrentCubeCoords == unitSpecificTarget)
                    {
                        unit.SetMoveCommand(unitSpecificTarget );
                        Debug.Log($"���� {unit.InstanceID}�� �̹� ��ǥ �����̼� ��ġ {unitSpecificTarget}�� �ֽ��ϴ�.");
                        continue;
                    }

                    // ��� Ž��
                    if (unitSpecificTarget != null)
                    {
                        unit.SetMoveCommand(unitSpecificTarget);
                    }
                    else
                    {
                        Debug.LogWarning($"���� {unit.InstanceID}: �����̼� ��ǥ ���� {unitSpecificTarget}(��)�� ���� ��θ� ã�� ���߽��ϴ�. (�׷� ��ǥ: {moveCmd.TargetCubeCoord})");
                        // ��� ��ã���� Idle ���·� ���� �Ǵ� �ٸ� ��ü �ൿ
                        unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                        unit.currentPath = null;
                    }
                }
                break; // case CommandType.Move ����

            case CommandType.AttackUnit:
                AttackUnitCommand attackUnitCmd = (AttackUnitCommand)command;
                SimulatedObject targetUnit = unitManager.GetUnitById(attackUnitCmd.TargetInstanceID);
                if (targetUnit != null && targetUnit.IsAlive())
                {
                    foreach (SimulatedObject attacker in actors)
                    {
                        attacker.SetAttackUnitCommand(targetUnit);
                    }
                }
                break;

            case CommandType.AttackPosition:
                AttackPositionCommand attackPosCmd = (AttackPositionCommand)command;
                Vector3Int targetPosition = attackPosCmd.TargetCubeCoord;
                if (hexGridSystem.IsValidHex(targetPosition) && hexGridSystem.GetTileAt(targetPosition).isWalkable)
                {
                    foreach (SimulatedObject attacker in actors)
                    {
                        attacker.SetAttackPositionCommand(targetPosition);
                    }
                }
                break;

            case CommandType.Stop:
                foreach (SimulatedObject unit in actors)
                {
                    // unit.SetStopCommand(); // SimulatedObject�� �ش� �Լ� ���� �ʿ�
                    unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                    unit.currentPath = null; // �̵� ����
                    unit.AttackTarget = null; // ���� ����
                }
                break;

            case CommandType.GatherResource:
                GatherResourceCommand gatherCmd = (GatherResourceCommand)command;
                ResourceNode resourceNode = unitManager.GetUnitById(gatherCmd.ResourceInstanceID) as ResourceNode;
                if (resourceNode != null)
                {
                     foreach (SimulatedObject unit in actors)
                     {
                        WorkerUnit worker = unit as WorkerUnit; // WorkerUnit���� ĳ����
                        if (worker != null)
                        {
                            worker.SetGatherResourceCommand(resourceNode);
                        }
                     }
                }
                Debug.Log($"�ڿ� ä�� ���: {gatherCmd.ResourceInstanceID}");
                break; 

            case CommandType.BuildBuilding:
                BuildBuildingCommand buildCmd = (BuildBuildingCommand)command;
                // �ڿ� Ȯ�� (ResourceManager ���)
                // if (resourceManager.CanAfford(buildCmd.IssuingPlayerId, buildCmd.BuildingDataID)) {
                //     resourceManager.ConsumeResource(buildCmd.IssuingPlayerId, buildCmd.BuildingDataID);
                //     if (buildingManager != null) {
                //         buildingManager.StartConstruction(buildCmd.BuildingDataID, buildCmd.TargetBuildCubeCoord, buildCmd.IssuingPlayerId, actors); // actors�� �ϲ�
                //     }
                // }
                Debug.Log($"���� �Ǽ� ���: {buildCmd.BuildingDataID} at {buildCmd.TargetBuildCubeCoord}");
                break;

            case CommandType.ProduceUnit:
                ProduceUnitCommand produceCmd = (ProduceUnitCommand)command;
                // �ڿ� Ȯ�� �� ���� �ǹ� ���� Ȯ��
                // if (buildingManager != null && resourceManager != null) {
                //     foreach (int buildingId in produceCmd.ActorInstanceIDs) { // ActorInstanceIDs�� ���� �ǹ� ID
                //         Building producingBuilding = buildingManager.GetBuildingById(buildingId);
                //         if (producingBuilding != null && producingBuilding.OwnerPlayerId == produceCmd.IssuingPlayerId &&
                //             resourceManager.CanAfford(produceCmd.IssuingPlayerId, produceCmd.UnitDataID)) {
                //             resourceManager.ConsumeResource(produceCmd.IssuingPlayerId, produceCmd.UnitDataID);
                //             producingBuilding.AddToProductionQueue(produceCmd.UnitDataID, produceCmd.Quantity);
                //         }
                //     }
                // }
                Debug.Log($"���� ���� ���: {produceCmd.UnitDataID} (����: {produceCmd.Quantity})");
                break;

            // ... ��Ÿ ��� CommandType�� ���� case ó�� ...

            default:
                Debug.LogWarning($"GameLogicManager: ó������ ���� ��� �����Դϴ� - {command.Type}");
                break;
        }
    }

    #endregion

    #region AI Processing (����)

    private void ProcessAIMainLogic(ulong currentTick)
    {
        // �� �Լ��� �̱� �÷��̾� ������ AI (�Ǵ� ���� AI �÷��̾�)�� �ൿ�� �����մϴ�.
        // ��: AI �÷��̾��� �ڿ� ����, ���� ����, �� ��Ȳ ���� ����Ͽ�
        //      ���ο� ICommand�� �����ϰ� ProcessLocalCommand()�� ���� �������մϴ�.
        // Debug.Log($"[AI] AI ���� ó�� �� - Tick: {currentTick}");

        // ulong aiPlayerId = 1; // ���� AI �÷��̾� ID

        // // ������ AI ��: �Ź� �ϲ� �ϳ� ���� (�ڿ��� �ִٸ�)
        // if (resourceManager != null && resourceManager.GetCurrentResource(aiPlayerId, "�̳׶�") >= 50)
        // {
        //     // ���� �ǹ� ã�� (BuildingManager �ʿ�)
        //     Building mainBase = buildingManager.FindMainBase(aiPlayerId);
        //     if (mainBase != null)
        //     {
        //         ProduceUnitCommand aiCmd = new ProduceUnitCommand(aiPlayerId, new List<int> { mainBase.InstanceID }, "�ϲ�����ID", 1, currentTick + (ulong)timeManager.GetComponent<PlayerInputHandler>().inputDelayTicks + 1); // AI ����� �ణ �� �ʰ� ����ǵ���
        //         ProcessLocalCommand(aiCmd);
        //     }
        // }
    }

    #endregion
}