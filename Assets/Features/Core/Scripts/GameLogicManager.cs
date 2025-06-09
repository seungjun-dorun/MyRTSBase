using RTSGame.Commands; // CommandSystem에서 정의한 네임스페이스 사용 (CommandSystem.cs 파일 참고)
using System.Collections.Generic;
using UnityEngine;

public class GameLogicManager : MonoBehaviour
{
    [Header("Manager References")]
    [Tooltip("시간 및 틱 관리를 담당하는 매니저입니다.")]
    public DeterministicTimeManager timeManager; // Inspector에서 할당
    [Tooltip("유닛 생성, 관리, 업데이트를 담당하는 매니저입니다.")]
    public UnitManager unitManager;             // Inspector에서 할당

    /*
    [Tooltip("건물 관련 로직을 담당하는 매니저입니다. (선택 사항)")]
    public BuildingManager buildingManager;     // Inspector에서 할당 (필요시)
    [Tooltip("자원 관련 로직을 담당하는 매니저입니다. (선택 사항)")]
    public ResourceManager resourceManager;       // Inspector에서 할당 (필요시)
    [Tooltip("Flow Field 경로 탐색을 담당하는 매니저입니다. (선택 사항)")]
    public FlowFieldManager flowFieldManager;   // Inspector에서 할당 (FlowField 사용 시)
    */
    [Tooltip("육각 그리드 시스템입니다.")]
    public HexGridSystem hexGridSystem;         // Inspector에서 할당

    // --- 명령 처리 관련---
    // Key: 실행될 틱(ExecutionTick), Value: 해당 틱에 실행될 명령 리스트
    private Dictionary<ulong, List<ICommand>> _scheduledCommands = new Dictionary<ulong, List<ICommand>>();

    // --- AI 관련 (선택 사항) ---
    [Header("AI Settings (Optional)")]
    [Tooltip("AI 로직을 몇 틱마다 실행할지 결정합니다.")]
    public int aiUpdateIntervalTicks = 10; // 예: 10틱마다 AI 결정
    private ulong _lastAiUpdateTick = 0;

    #region Unity Lifecycle & Initialization

    void Awake()
    {
        // 필수 참조 확인
        if (timeManager == null)
            Debug.LogError("GameLogicManager: DeterministicTimeManager가 할당되지 않았습니다!");
        if (unitManager == null)
            Debug.LogError("GameLogicManager: UnitManager가 할당되지 않았습니다!");
        if (hexGridSystem == null)
            Debug.LogError("GameLogicManager: HexGridSystem이 할당되지 않았습니다!");
        // 선택적 참조는 null일 수 있으므로, 사용 전에 null 체크 필요
    }

    #endregion

    #region Command Processing (싱글 플레이어용)

    /// <summary>
    /// 로컬 플레이어의 입력을 통해 생성된 명령을 스케줄 큐에 추가합니다.
    /// (싱글 플레이어에서는 PlayerInputHandler가 이 함수를 직접 호출)
    /// </summary>
    public void ProcessLocalCommand(ICommand command)
    {
        if (command == null)
        {
            Debug.LogWarning("GameLogicManager: null 명령을 스케줄링하려고 시도했습니다.");
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
    /// DeterministicTimeManager에 의해 매 틱 호출됩니다.
    /// 해당 틱의 게임 로직을 순차적으로 처리합니다.
    /// </summary>
    public void ProcessGameLogicForTick(ulong currentTick, float tickInterval)
    {
        // 1. 스케줄된 명령 실행
        ExecuteScheduledCommands(currentTick);

        // 2. 유닛 상태 업데이트 (이동, 공격 등 기본 행동)
        if (unitManager != null)
        {
            unitManager.UpdateUnitsForTick(tickInterval); // UnitManager가 내부적으로 유닛들의 SimulateStep 호출
        }
        /*
        // 3. 건물 상태 업데이트 (생산, 수리 등)
        if (buildingManager != null)
        {
            // buildingManager.UpdateBuildingsForTick(currentTick, tickInterval);
        }

        // 4. 자원 관련 업데이트 (자원량 변화 등)
        if (resourceManager != null)
        {
            // resourceManager.UpdateResourcesForTick(currentTick, tickInterval);
        }
        */
        // 5. AI 로직 업데이트 (일정 간격으로)
        if (currentTick >= _lastAiUpdateTick + (ulong)aiUpdateIntervalTicks)
        {
            // ProcessAIMainLogic(currentTick);
            _lastAiUpdateTick = currentTick;
        }

        // ... 기타 틱 기반 게임 시스템 업데이트 ...
        // 예: 날씨 변화, 주기적인 이벤트 발생 등
    }

    /// <summary>
    /// 현재 틱에 스케줄된 모든 명령을 실행합니다.
    /// </summary>
    private void ExecuteScheduledCommands(ulong currentTick)
    {
        if (_scheduledCommands.TryGetValue(currentTick, out List<ICommand> commandsToExecute))
        {
            // 중요: 명령 실행 순서가 게임 결과에 영향을 미칠 수 있으므로,
            // 필요하다면 일관된 기준으로 정렬해야 합니다. (예: 플레이어 ID, 명령 타입 등)
            // 여기서는 수신된 순서대로 실행한다고 가정합니다.
            // commandsToExecute.Sort((a, b) => /* 정렬 로직 */ );

            for (int i = 0; i < commandsToExecute.Count; i++)
            {
                ExecuteSingleCommand(commandsToExecute[i]);
            }

            // 실행된 명령 리스트는 제거
            _scheduledCommands.Remove(currentTick);
        }
    }

    /// <summary>
    /// 단일 명령을 실제로 실행하는 로직입니다.
    /// </summary>
    private void ExecuteSingleCommand(ICommand command)
    {
        // Debug.Log($"[GameLogic] Executing Command: {command.Type} at Tick {timeManager.CurrentTick}");

        // 명령을 수행할 주체(유닛/건물)들을 가져옵니다.
        List<SimulatedObject> actors = new List<SimulatedObject>();
        if (command.ActorInstanceIDs != null)
        {
            foreach (int actorId in command.ActorInstanceIDs)
            {
                SimulatedObject actor = unitManager.GetUnitById(actorId); // 또는 BuildingManager 등에서도 찾아야 할 수 있음
                if (actor != null && actor.IsAlive()) // 살아있는 액터만
                {
                    actors.Add(actor);
                }
            }
        }

        // 명령 유형에 따라 분기하여 처리
        switch (command.Type)
        {
            case CommandType.Move:
                MoveCommand moveCmd = (MoveCommand)command;

                if (actors.Count == 0)
                {
                    Debug.Log("MoveCommand: 이동할 유닛이 없습니다.");
                    break; // switch 문 다음으로 넘어감 (현재 명령 처리 종료)
                }

                // // HexGridSystem이 할당되지 않았으면 포메이션 계산 불가
                if (hexGridSystem == null)
                {
                    Debug.LogError("HexGridSystem is not assigned. Cannot calculate formation. Moving units individually to group target.");
                    // 모든 유닛에게 동일한 그룹 목표 지점으로 경로 요청
                    foreach (SimulatedObject unit in actors)
                    {
                        // *** 이 부분도 RequestPathForUnit.FindPath로 변경 ***
                        // 단, 이 경우 hexGridSystem이 null이므로 RequestPathForUnit.FindPath도 실패할 것임.
                        // 따라서, hexGridSystem이 null이면 아예 경로 탐색 시도를 안 하거나,
                       // 매우 기본적인 대체 동작(예: 목표 지점으로 직선 이동 시도 - 장애물 무시)을 해야 함.
                        // 여기서는 경로 탐색 실패와 동일하게 처리 (Idle 상태로).
                        Debug.LogWarning($"유닛 {unit.InstanceID}: HexGridSystem 부재로 경로 탐색 불가. 그룹 목표: {moveCmd.TargetCubeCoord}");
                        unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                        unit.currentPath = null;
                    }
                    break;
                }

                // 포메이션 관련 파라미터 (MoveCommand에 이 정보가 있다면 사용, 없다면 기본값)
                string formationType = "Spiral"; // 기본값 또는 moveCmd.FormationType
                int spacing = 1;                // 기본값 또는 moveCmd.FormationSpacing

                // FormationUtility를 사용하여 각 유닛의 개별 목표 지점 계산
                List<Vector3Int> individualTargetCoords = FormationUtility.CalculateIndividualTargetPositions(
                    moveCmd.TargetCubeCoord,    // 그룹의 주 목표 지점 (Vector3Int)
                    actors,                     // 이동할 유닛 리스트 (List<SimulatedObject>)
                    hexGridSystem,              // HexGridSystem 인스턴스 (HexGridSystem)
                    formationType,              // 원하는 포메이션 타입 (string)
                    spacing                     // 유닛 간 간격 (int)
                );

                // 각 유닛에 대해 계산된 개별 목표 지점으로 경로 탐색 요청
                for (int i = 0; i < actors.Count; i++)
                {
                    SimulatedObject unit = actors[i];
                    Vector3Int unitSpecificTarget;
                    
                    // individualTargetCoords 리스트의 인덱스가 유효한지,
                    // 그리고 해당 좌표가 유효하고 걸을 수 있는 타일인지 확인
                    if (i < individualTargetCoords.Count && hexGridSystem.IsValidHex(individualTargetCoords[i]) && hexGridSystem.GetTileAt(individualTargetCoords[i]).isWalkable)
                    {
                        unitSpecificTarget = individualTargetCoords[i];
                    }
                    else
                    {
                        Debug.LogWarning($"유닛 {unit.InstanceID}: 포메이션 위치 { (i < individualTargetCoords.Count ? individualTargetCoords[i].ToString() : "N/A") } 가 유효하지 않거나 부족합니다. 그룹 목표 {moveCmd.TargetCubeCoord} 또는 유닛 현재 위치 사용 시도.");
                        // 대체 목표 설정: 그룹 목표 또는, 이미 해당 위치에 있다면 현재 위치 유지
                        if(unit.CurrentCubeCoords == moveCmd.TargetCubeCoord) // 이미 그룹 목표에 있다면
                             unitSpecificTarget = unit.CurrentCubeCoords;
                        else // 그룹 목표로 시도
                             unitSpecificTarget = moveCmd.TargetCubeCoord;

                        // 그룹 목표도 유효하지 않으면 최종적으로 현재 위치 (사실상 이동 안함)
                        if (!hexGridSystem.IsValidHex(unitSpecificTarget) || !hexGridSystem.GetTileAt(unitSpecificTarget).isWalkable) {
                            unitSpecificTarget = unit.CurrentCubeCoords;
                        }
                    }

                    // 이미 목표 지점이면 경로 탐색 생략
                    if (unit.CurrentCubeCoords == unitSpecificTarget)
                    {
                        unit.SetMoveCommand(unitSpecificTarget );
                        Debug.Log($"유닛 {unit.InstanceID}는 이미 목표 포메이션 위치 {unitSpecificTarget}에 있습니다.");
                        continue;
                    }

                    // 경로 탐색
                    if (unitSpecificTarget != null)
                    {
                        unit.SetMoveCommand(unitSpecificTarget);
                    }
                    else
                    {
                        Debug.LogWarning($"유닛 {unit.InstanceID}: 포메이션 목표 지점 {unitSpecificTarget}(으)로 가는 경로를 찾지 못했습니다. (그룹 목표: {moveCmd.TargetCubeCoord})");
                        // 경로 못찾으면 Idle 상태로 변경 또는 다른 대체 행동
                        unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                        unit.currentPath = null;
                    }
                }
                break; // case CommandType.Move 종료

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
                    // unit.SetStopCommand(); // SimulatedObject에 해당 함수 구현 필요
                    unit.CurrentActionState = SimulatedObject.UnitActionState.Idle;
                    unit.currentPath = null; // 이동 중지
                    unit.AttackTarget = null; // 공격 중지
                }
                break;

            case CommandType.GatherResource:
                GatherResourceCommand gatherCmd = (GatherResourceCommand)command;
                ResourceNode resourceNode = unitManager.GetUnitById(gatherCmd.ResourceInstanceID) as ResourceNode;
                if (resourceNode != null)
                {
                     foreach (SimulatedObject unit in actors)
                     {
                        WorkerUnit worker = unit as WorkerUnit; // WorkerUnit으로 캐스팅
                        if (worker != null)
                        {
                            worker.SetGatherResourceCommand(resourceNode);
                        }
                     }
                }
                Debug.Log($"자원 채취 명령: {gatherCmd.ResourceInstanceID}");
                break; 

            case CommandType.BuildBuilding:
                BuildBuildingCommand buildCmd = (BuildBuildingCommand)command;
                // 자원 확인 (ResourceManager 사용)
                // if (resourceManager.CanAfford(buildCmd.IssuingPlayerId, buildCmd.BuildingDataID)) {
                //     resourceManager.ConsumeResource(buildCmd.IssuingPlayerId, buildCmd.BuildingDataID);
                //     if (buildingManager != null) {
                //         buildingManager.StartConstruction(buildCmd.BuildingDataID, buildCmd.TargetBuildCubeCoord, buildCmd.IssuingPlayerId, actors); // actors는 일꾼
                //     }
                // }
                Debug.Log($"빌딩 건설 명령: {buildCmd.BuildingDataID} at {buildCmd.TargetBuildCubeCoord}");
                break;

            case CommandType.ProduceUnit:
                ProduceUnitCommand produceCmd = (ProduceUnitCommand)command;
                // 자원 확인 및 생산 건물 상태 확인
                // if (buildingManager != null && resourceManager != null) {
                //     foreach (int buildingId in produceCmd.ActorInstanceIDs) { // ActorInstanceIDs가 생산 건물 ID
                //         Building producingBuilding = buildingManager.GetBuildingById(buildingId);
                //         if (producingBuilding != null && producingBuilding.OwnerPlayerId == produceCmd.IssuingPlayerId &&
                //             resourceManager.CanAfford(produceCmd.IssuingPlayerId, produceCmd.UnitDataID)) {
                //             resourceManager.ConsumeResource(produceCmd.IssuingPlayerId, produceCmd.UnitDataID);
                //             producingBuilding.AddToProductionQueue(produceCmd.UnitDataID, produceCmd.Quantity);
                //         }
                //     }
                // }
                Debug.Log($"유닛 생산 명령: {produceCmd.UnitDataID} (수량: {produceCmd.Quantity})");
                break;

            // ... 기타 모든 CommandType에 대한 case 처리 ...

            default:
                Debug.LogWarning($"GameLogicManager: 처리되지 않은 명령 유형입니다 - {command.Type}");
                break;
        }
    }

    #endregion

    #region AI Processing (예시)

    private void ProcessAIMainLogic(ulong currentTick)
    {
        // 이 함수는 싱글 플레이어 게임의 AI (또는 여러 AI 플레이어)의 행동을 결정합니다.
        // 예: AI 플레이어의 자원 상태, 유닛 분포, 적 상황 등을 고려하여
        //      새로운 ICommand를 생성하고 ProcessLocalCommand()를 통해 스케줄합니다.
        // Debug.Log($"[AI] AI 로직 처리 중 - Tick: {currentTick}");

        // ulong aiPlayerId = 1; // 예시 AI 플레이어 ID

        // // 간단한 AI 예: 매번 일꾼 하나 생성 (자원이 있다면)
        // if (resourceManager != null && resourceManager.GetCurrentResource(aiPlayerId, "미네랄") >= 50)
        // {
        //     // 본진 건물 찾기 (BuildingManager 필요)
        //     Building mainBase = buildingManager.FindMainBase(aiPlayerId);
        //     if (mainBase != null)
        //     {
        //         ProduceUnitCommand aiCmd = new ProduceUnitCommand(aiPlayerId, new List<int> { mainBase.InstanceID }, "일꾼유닛ID", 1, currentTick + (ulong)timeManager.GetComponent<PlayerInputHandler>().inputDelayTicks + 1); // AI 명령은 약간 더 늦게 실행되도록
        //         ProcessLocalCommand(aiCmd);
        //     }
        // }
    }

    #endregion
}