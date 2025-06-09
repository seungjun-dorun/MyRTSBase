using UnityEngine; // Vector3Int, Vector2 등 사용 시
using System.Collections.Generic; // List 사용 시

// 네임스페이스를 사용하여 다른 시스템과 구분하는 것이 좋음
namespace RTSGame.Commands
{
    // 1. 명령 유형 정의
    public enum CommandType
    {
        None,           // 기본값 또는 빈 명령
        Move,           // 유닛 이동
        AttackUnit,     // 특정 유닛 공격
        AttackPosition, // 특정 지점 공격 (어택땅)
        Stop,           // 현재 행동 중지
        HoldPosition,   // 현 위치 사수
        Patrol,         // 두 지점 순찰

        GatherResource, // 자원 채취
        ReturnResource, // 채취한 자원 기지로 반납
        BuildBuilding,  // 건물 건설

        ProduceUnit,    // 유닛 생산
        CancelProduction, // 생산 취소
        SetRallyPoint,  // 집결 지점 설정
        
        Ability_Q,      // Q 스킬 사용 (예시)
        Ability_W,      // W 스킬 사용 (예시)
        // ... 기타 필요한 모든 게임 액션에 대한 명령 유형 추가
    }

    // 2. 모든 명령의 기본 인터페이스
    public interface ICommand
    {
        CommandType Type { get; }
        ulong IssuingPlayerId { get; }      // 명령을 내린 플레이어의 고유 ID
        List<int> ActorInstanceIDs { get; } // 이 명령을 수행할 주체(유닛, 건물 등)의 InstanceID 리스트
        ulong ExecutionTick { get; set; }    // 이 명령이 실행되어야 하는 게임 틱

        // (선택적) 직렬화/역직렬화를 위한 함수 시그니처를 여기에 넣을 수도 있음
        // void Serialize(System.IO.BinaryWriter writer);
        // void Deserialize(System.IO.BinaryReader reader);
    }

    // 3. 각 명령 유형에 대한 구체적인 데이터 구조체 (struct) 또는 클래스 (class)
    //    - struct는 값 타입이므로 복사 시 주의. 간단한 데이터 전달에는 좋음.
    //    - class는 참조 타입. 더 복잡한 데이터나 상속이 필요할 때 사용.
    //    - 여기서는 간단함을 위해 struct를 주로 사용하되, 필요에 따라 class로 변경 가능.
    //    - 모든 명령 구조체는 ICommand 인터페이스를 구현해야 함.

    // --- 이동 관련 명령 ---
    public struct MoveCommand : ICommand
    {
        public CommandType Type => CommandType.Move;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 이동할 유닛들의 ID

        public Vector3Int TargetCubeCoord { get; private set; } // 목표 지점
                                                                // 또는 목표 경로 (큐브 좌표 리스트) public List<Vector3Int> TargetPath { get; private set; }
        public ulong ExecutionTick { get; set; }

        public MoveCommand(ulong playerId, List<int> actorIds, Vector3Int targetCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetCubeCoord = targetCoord;
            ExecutionTick = execTick;
        }
    }

    public struct StopCommand : ICommand
    {
        public CommandType Type => CommandType.Stop;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; }
        public ulong ExecutionTick { get; set; }

        public StopCommand(ulong playerId, List<int> actorIds, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            ExecutionTick = execTick;
        }
    }

    public struct HoldPositionCommand : ICommand // Stop과 유사하지만, 내부 상태를 Hold로 변경
    {
        public CommandType Type => CommandType.HoldPosition;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; }
        public ulong ExecutionTick { get; set; }

        public HoldPositionCommand(ulong playerId, List<int> actorIds, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            ExecutionTick = execTick;
        }
    }

    public struct PatrolCommand : ICommand
    {
        public CommandType Type => CommandType.Patrol;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 이동할 유닛들의 ID

        public Vector3Int TargetCubeCoord { get; private set; } // 목표 지점
                                                                // 또는 목표 경로 (큐브 좌표 리스트) public List<Vector3Int> TargetPath { get; private set; }
        public ulong ExecutionTick { get; set; }

        public PatrolCommand(ulong playerId, List<int> actorIds, Vector3Int targetCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetCubeCoord = targetCoord;
            ExecutionTick = execTick;
        }
    }
    // --- 공격 관련 명령 ---
    public struct AttackUnitCommand : ICommand
    {
        public CommandType Type => CommandType.AttackUnit;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 공격하는 유닛들
        public int TargetInstanceID { get; private set; }      // 공격받는 대상 유닛 ID
        public ulong ExecutionTick { get; set; }

        public AttackUnitCommand(ulong playerId, List<int> actorIds, int targetId, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetInstanceID = targetId;
            ExecutionTick = execTick;
        }
    }

    public struct AttackPositionCommand : ICommand // 어택땅
    {
        public CommandType Type => CommandType.AttackPosition;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; }
        public Vector3Int TargetCubeCoord { get; private set; } // 공격 목표 지점
        public ulong ExecutionTick { get; set; }

        public AttackPositionCommand(ulong playerId, List<int> actorIds, Vector3Int targetCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetCubeCoord = targetCoord;
            ExecutionTick = execTick;
        }
    }

    // --- 건설 및 생산 관련 명령 ---
    public struct GatherResourceCommand : ICommand
    {
        public CommandType Type => CommandType.GatherResource;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 자원을 채취할 유닛 ID
        public int ResourceInstanceID { get; private set; }   // 채취할 자원 오브젝트 ID
        public ulong ExecutionTick { get; set; }
        public GatherResourceCommand(ulong playerId, List<int> actorIds, int resourceId, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            ResourceInstanceID = resourceId;
            ExecutionTick = execTick;
        }
    }

    public struct ReturnResourceCommand : ICommand
    {
        public CommandType Type => CommandType.ReturnResource;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 자원을 반납할 유닛 ID
        public string BuildingDataID { get; private set; } // 건설할 건물의 데이터 ID (ScriptableObject 이름 등)
        public ulong ExecutionTick { get; set; }
        public ReturnResourceCommand(ulong playerId, List<int> actorIds, string buildingDataId, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            BuildingDataID = buildingDataId;
            ExecutionTick = execTick;
        }
    }

    public struct BuildBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.BuildBuilding;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 건설 일꾼 유닛 ID (없을 수도 있음)
        public string BuildingDataID { get; private set; } // 건설할 건물의 데이터 ID (ScriptableObject 이름 등)
        public Vector3Int TargetBuildCubeCoord { get; private set; } // 건설 위치
        public ulong ExecutionTick { get; set; }

        public BuildBuildingCommand(ulong playerId, List<int> workerIds, string buildingDataId, Vector3Int buildCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = workerIds != null ? new List<int>(workerIds) : new List<int>();
            BuildingDataID = buildingDataId;
            TargetBuildCubeCoord = buildCoord;
            ExecutionTick = execTick;
        }
    }

    public struct ProduceUnitCommand : ICommand
    {
        public CommandType Type => CommandType.ProduceUnit;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 유닛을 생산할 건물(들)의 ID
        public string UnitDataID { get; private set; }     // 생산할 유닛의 데이터 ID
        public int Quantity { get; private set; }          // 생산 수량 (보통 1, 여러 개 동시 생산 지원 시)
        public ulong ExecutionTick { get; set; }

        public ProduceUnitCommand(ulong playerId, List<int> buildingIds, string unitDataId, int quantity, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(buildingIds);
            UnitDataID = unitDataId;
            Quantity = quantity;
            ExecutionTick = execTick;
        }
    }

    public struct CancelProductionCommand : ICommand
    {
        public CommandType Type => CommandType.CancelProduction;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 생산을 취소할 건물(들)의 ID
        public ulong ExecutionTick { get; set; }
        public CancelProductionCommand(ulong playerId, List<int> buildingIds, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(buildingIds);
            ExecutionTick = execTick;
        }
    }

    public struct SetRallyPointCommand : ICommand
    {
        public CommandType Type => CommandType.SetRallyPoint;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 집결 지점을 설정할 건물(들)의 ID
        public Vector3Int RallyPointCoord { get; private set; } // 집결 지점 좌표
        public ulong ExecutionTick { get; set; }
        public SetRallyPointCommand(ulong playerId, List<int> buildingIds, Vector3Int rallyPointCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(buildingIds);
            RallyPointCoord = rallyPointCoord;
            ExecutionTick = execTick;
        }
    }

    // --- 스킬/능력 사용 명령 (예시) ---
    // 스킬 종류에 따라 필요한 데이터가 다를 수 있으므로, 일반적인 형태와 구체적인 형태를 만들 수 있음.
    public struct AbilityCommand : ICommand // 범용적인 능력 사용 명령
    {
        public CommandType Type { get; private set; } // 실제로는 Ability_Q, Ability_W 등으로 설정될 것임
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // 능력을 사용하는 유닛들
        public string AbilityID { get; private set; }           // 능력의 고유 ID
        // 능력 사용에 필요한 추가 타겟 정보 (선택적)
        public int TargetUnitID { get; private set; }          // 대상 유닛이 필요한 경우
        public Vector3Int TargetCubeCoord { get; private set; } // 대상 지점이 필요한 경우
        public ulong ExecutionTick { get; set; }

        // 생성자는 필요한 파라미터에 따라 여러 개 만들 수 있음
        public AbilityCommand(CommandType specificAbilityType, ulong playerId, List<int> actorIds, string abilityId, ulong execTick)
        {
            Type = specificAbilityType; // 예: CommandType.Ability_Q
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            AbilityID = abilityId;
            TargetUnitID = -1; // 대상 없음 표시
            TargetCubeCoord = Vector3Int.one * int.MinValue; // 대상 없음 표시
            ExecutionTick = execTick;
        }

        public AbilityCommand(CommandType specificAbilityType, ulong playerId, List<int> actorIds, string abilityId, int targetUnitId, ulong execTick)
            : this(specificAbilityType, playerId, actorIds, abilityId, execTick)
        {
            TargetUnitID = targetUnitId;
        }

        public AbilityCommand(CommandType specificAbilityType, ulong playerId, List<int> actorIds, string abilityId, Vector3Int targetCoord, ulong execTick)
            : this(specificAbilityType, playerId, actorIds, abilityId, execTick)
        {
            TargetCubeCoord = targetCoord;
        }
    }

    // 4. (선택적) 명령 생성 헬퍼 함수 또는 팩토리 클래스
    // public static class CommandFactory
    // {
    //     public static MoveCommand CreateMoveCommand(...) { ... }
    //     public static AttackUnitCommand CreateAttackUnitCommand(...) { ... }
    // }

    // 5. (선택적) 명령 직렬화/역직렬화 유틸리티
    //    - 나중에 네트워크 구현 시 필요. 싱글플레이어에서는 객체 직접 전달.
    //    - 예: public static class CommandSerializer
    //      {
    //          public static byte[] Serialize(ICommand command) { ... }
    //          public static ICommand Deserialize(byte[] data) { ... }
    //          // 또는 각 ICommand 구현체에 Serialize/Deserialize 메서드 추가
    //      }
}