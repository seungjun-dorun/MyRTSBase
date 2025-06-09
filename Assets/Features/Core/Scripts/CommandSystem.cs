using UnityEngine; // Vector3Int, Vector2 �� ��� ��
using System.Collections.Generic; // List ��� ��

// ���ӽ����̽��� ����Ͽ� �ٸ� �ý��۰� �����ϴ� ���� ����
namespace RTSGame.Commands
{
    // 1. ��� ���� ����
    public enum CommandType
    {
        None,           // �⺻�� �Ǵ� �� ���
        Move,           // ���� �̵�
        AttackUnit,     // Ư�� ���� ����
        AttackPosition, // Ư�� ���� ���� (���ö�)
        Stop,           // ���� �ൿ ����
        HoldPosition,   // �� ��ġ ���
        Patrol,         // �� ���� ����

        GatherResource, // �ڿ� ä��
        ReturnResource, // ä���� �ڿ� ������ �ݳ�
        BuildBuilding,  // �ǹ� �Ǽ�

        ProduceUnit,    // ���� ����
        CancelProduction, // ���� ���
        SetRallyPoint,  // ���� ���� ����
        
        Ability_Q,      // Q ��ų ��� (����)
        Ability_W,      // W ��ų ��� (����)
        // ... ��Ÿ �ʿ��� ��� ���� �׼ǿ� ���� ��� ���� �߰�
    }

    // 2. ��� ����� �⺻ �������̽�
    public interface ICommand
    {
        CommandType Type { get; }
        ulong IssuingPlayerId { get; }      // ����� ���� �÷��̾��� ���� ID
        List<int> ActorInstanceIDs { get; } // �� ����� ������ ��ü(����, �ǹ� ��)�� InstanceID ����Ʈ
        ulong ExecutionTick { get; set; }    // �� ����� ����Ǿ�� �ϴ� ���� ƽ

        // (������) ����ȭ/������ȭ�� ���� �Լ� �ñ״�ó�� ���⿡ ���� ���� ����
        // void Serialize(System.IO.BinaryWriter writer);
        // void Deserialize(System.IO.BinaryReader reader);
    }

    // 3. �� ��� ������ ���� ��ü���� ������ ����ü (struct) �Ǵ� Ŭ���� (class)
    //    - struct�� �� Ÿ���̹Ƿ� ���� �� ����. ������ ������ ���޿��� ����.
    //    - class�� ���� Ÿ��. �� ������ �����ͳ� ����� �ʿ��� �� ���.
    //    - ���⼭�� �������� ���� struct�� �ַ� ����ϵ�, �ʿ信 ���� class�� ���� ����.
    //    - ��� ��� ����ü�� ICommand �������̽��� �����ؾ� ��.

    // --- �̵� ���� ��� ---
    public struct MoveCommand : ICommand
    {
        public CommandType Type => CommandType.Move;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // �̵��� ���ֵ��� ID

        public Vector3Int TargetCubeCoord { get; private set; } // ��ǥ ����
                                                                // �Ǵ� ��ǥ ��� (ť�� ��ǥ ����Ʈ) public List<Vector3Int> TargetPath { get; private set; }
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

    public struct HoldPositionCommand : ICommand // Stop�� ����������, ���� ���¸� Hold�� ����
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
        public List<int> ActorInstanceIDs { get; private set; } // �̵��� ���ֵ��� ID

        public Vector3Int TargetCubeCoord { get; private set; } // ��ǥ ����
                                                                // �Ǵ� ��ǥ ��� (ť�� ��ǥ ����Ʈ) public List<Vector3Int> TargetPath { get; private set; }
        public ulong ExecutionTick { get; set; }

        public PatrolCommand(ulong playerId, List<int> actorIds, Vector3Int targetCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetCubeCoord = targetCoord;
            ExecutionTick = execTick;
        }
    }
    // --- ���� ���� ��� ---
    public struct AttackUnitCommand : ICommand
    {
        public CommandType Type => CommandType.AttackUnit;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // �����ϴ� ���ֵ�
        public int TargetInstanceID { get; private set; }      // ���ݹ޴� ��� ���� ID
        public ulong ExecutionTick { get; set; }

        public AttackUnitCommand(ulong playerId, List<int> actorIds, int targetId, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetInstanceID = targetId;
            ExecutionTick = execTick;
        }
    }

    public struct AttackPositionCommand : ICommand // ���ö�
    {
        public CommandType Type => CommandType.AttackPosition;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; }
        public Vector3Int TargetCubeCoord { get; private set; } // ���� ��ǥ ����
        public ulong ExecutionTick { get; set; }

        public AttackPositionCommand(ulong playerId, List<int> actorIds, Vector3Int targetCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            TargetCubeCoord = targetCoord;
            ExecutionTick = execTick;
        }
    }

    // --- �Ǽ� �� ���� ���� ��� ---
    public struct GatherResourceCommand : ICommand
    {
        public CommandType Type => CommandType.GatherResource;
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // �ڿ��� ä���� ���� ID
        public int ResourceInstanceID { get; private set; }   // ä���� �ڿ� ������Ʈ ID
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
        public List<int> ActorInstanceIDs { get; private set; } // �ڿ��� �ݳ��� ���� ID
        public string BuildingDataID { get; private set; } // �Ǽ��� �ǹ��� ������ ID (ScriptableObject �̸� ��)
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
        public List<int> ActorInstanceIDs { get; private set; } // �Ǽ� �ϲ� ���� ID (���� ���� ����)
        public string BuildingDataID { get; private set; } // �Ǽ��� �ǹ��� ������ ID (ScriptableObject �̸� ��)
        public Vector3Int TargetBuildCubeCoord { get; private set; } // �Ǽ� ��ġ
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
        public List<int> ActorInstanceIDs { get; private set; } // ������ ������ �ǹ�(��)�� ID
        public string UnitDataID { get; private set; }     // ������ ������ ������ ID
        public int Quantity { get; private set; }          // ���� ���� (���� 1, ���� �� ���� ���� ���� ��)
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
        public List<int> ActorInstanceIDs { get; private set; } // ������ ����� �ǹ�(��)�� ID
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
        public List<int> ActorInstanceIDs { get; private set; } // ���� ������ ������ �ǹ�(��)�� ID
        public Vector3Int RallyPointCoord { get; private set; } // ���� ���� ��ǥ
        public ulong ExecutionTick { get; set; }
        public SetRallyPointCommand(ulong playerId, List<int> buildingIds, Vector3Int rallyPointCoord, ulong execTick)
        {
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(buildingIds);
            RallyPointCoord = rallyPointCoord;
            ExecutionTick = execTick;
        }
    }

    // --- ��ų/�ɷ� ��� ��� (����) ---
    // ��ų ������ ���� �ʿ��� �����Ͱ� �ٸ� �� �����Ƿ�, �Ϲ����� ���¿� ��ü���� ���¸� ���� �� ����.
    public struct AbilityCommand : ICommand // �������� �ɷ� ��� ���
    {
        public CommandType Type { get; private set; } // �����δ� Ability_Q, Ability_W ������ ������ ����
        public ulong IssuingPlayerId { get; private set; }
        public List<int> ActorInstanceIDs { get; private set; } // �ɷ��� ����ϴ� ���ֵ�
        public string AbilityID { get; private set; }           // �ɷ��� ���� ID
        // �ɷ� ��뿡 �ʿ��� �߰� Ÿ�� ���� (������)
        public int TargetUnitID { get; private set; }          // ��� ������ �ʿ��� ���
        public Vector3Int TargetCubeCoord { get; private set; } // ��� ������ �ʿ��� ���
        public ulong ExecutionTick { get; set; }

        // �����ڴ� �ʿ��� �Ķ���Ϳ� ���� ���� �� ���� �� ����
        public AbilityCommand(CommandType specificAbilityType, ulong playerId, List<int> actorIds, string abilityId, ulong execTick)
        {
            Type = specificAbilityType; // ��: CommandType.Ability_Q
            IssuingPlayerId = playerId;
            ActorInstanceIDs = new List<int>(actorIds);
            AbilityID = abilityId;
            TargetUnitID = -1; // ��� ���� ǥ��
            TargetCubeCoord = Vector3Int.one * int.MinValue; // ��� ���� ǥ��
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

    // 4. (������) ��� ���� ���� �Լ� �Ǵ� ���丮 Ŭ����
    // public static class CommandFactory
    // {
    //     public static MoveCommand CreateMoveCommand(...) { ... }
    //     public static AttackUnitCommand CreateAttackUnitCommand(...) { ... }
    // }

    // 5. (������) ��� ����ȭ/������ȭ ��ƿ��Ƽ
    //    - ���߿� ��Ʈ��ũ ���� �� �ʿ�. �̱��÷��̾���� ��ü ���� ����.
    //    - ��: public static class CommandSerializer
    //      {
    //          public static byte[] Serialize(ICommand command) { ... }
    //          public static ICommand Deserialize(byte[] data) { ... }
    //          // �Ǵ� �� ICommand ����ü�� Serialize/Deserialize �޼��� �߰�
    //      }
}