using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using System.Linq;

// UnitData�� ������ float�� ���� �� ������, Initialize���� int�� ��ȯ�ǰų�,
// UnitData ��ü�� int ������� ����� �� ����. ���⼭�� Initialize���� ��ȯ ����.
// ����: public class UnitData : ScriptableObject { public float initialHealth; public float initialMoveSpeed; ... }

public class SimulatedObject : MonoBehaviour
{
    [Header("Core Stats (from UnitData)")]
    public int InstanceID { get; protected set; }
    public ulong OwnerPlayerId { get; protected set; }

    // --- ���� ��� �ٽ� �ɷ�ġ ---
    public int MaxHealth { get; protected set; }
    public int CurrentHealth { get; protected set; }

    // �̵� �ӵ�: 1ƽ�� �̵��� �� �ִ� "�̵� ����Ʈ" �Ǵ� "���� �Ÿ� ����".
    // ���� ���� �Ÿ��� (MovePointsPerTick * INTERNAL_DISTANCE_SCALE_FACTOR) / TICKS_PER_SECOND �� ȯ�� ����.
    public int MovePointsPerTick { get; protected set; }
    private const int INTERNAL_DISTANCE_SCALE_FACTOR = 1000; // ��: 1 �׸��� ���� = 1000 ���� ����

    public int AttackDamage { get; protected set; }
    public int AttackRangeInTiles { get; protected set; } // ���� �׸��� Ÿ�� ���� ��Ÿ�
    public int AttackCooldownInTicks { get; protected set; } // ƽ ���� ���� ��ٿ�

    protected int _currentAttackCooldownTimerInTicks;

    public int VisionRangeInTiles { get; protected set; }

    public enum UnitActionState { Idle, Moving, Attacking, GatheringResource, Building, Dead}
    public enum UnitPurposeState { Idle, MoveToPosition, Patrol, AttackingUnit, AttackingPos, Holding, GatheringResource, ReturnResource, Building, Dead}
    public UnitActionState CurrentActionState { get; set; }
    public UnitPurposeState CurrentPurposeState { get; set; }

    public List<ResourceCost> CreationCost { get; protected set; }
    public int CreationTimeInTicks { get; protected set; } // ���� �ð� (ƽ ����)


    // --- ���� ��ġ �� �̵� ���� ---
    public Vector3Int CurrentCubeCoords { get; protected set; }
    protected Vector3 _simulatedWorldPosition; // �ð��� ������ (float ����)
    protected Vector3 _previousSimulatedWorldPosition; // �ð��� ������ (float ����)

    public List<Vector3Int> currentPath = null;
    protected int _currentPathIndex = 0;
    protected int _accumulatedMovePoints = 0; // ���� ƽ���� �̵��ϰ� ���� �̵� ����Ʈ (���� ƽ �̿� X, ƽ �������� ���)
    
    public Vector3Int SizeInTiles { get; protected set; } // ������ �����ϴ� Ÿ�� ũ�� (���� �׸��忡���� ũ��)

    // --- �� ������ �ʿ��� �߰� ������ ---
    public Vector3Int TargetLocation { get; set; }  // �̵�, �Ǽ�, ���� ��ġ ��
    public Vector3Int SecondaryTargetLocation { get; set; }  // �̵�, �Ǽ�, ���� ��ġ ��
    public SimulatedObject AttackTarget { get; set; }
    public SimulatedObject TargetEntity { get; set; } // ����, ����, ���� ��� ��

    // --- ���� ---
    protected HexGridSystem _hexGridSystemRef; // HexGridSystem ����
    protected UnitManager _unitManagerRef;

    // --- ���ð��� ---
    public GameObject SelectionProjector;

    void Awake()
    {
        if (SelectionProjector != null)
        {
            SelectionProjector.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("SelectionProjector�� Inspector���� �Ҵ���� �ʾҽ��ϴ�.", this.gameObject);
        }
    }
    // --- �ʱ�ȭ ---
    public virtual void Initialize(int instanceId, ulong ownerId, UnitData data, HexGridSystem gridSystem, UnitManager unitManagerRef, Vector3Int initialCubeCoords)
    {
        InstanceID = instanceId;
        OwnerPlayerId = ownerId;
        _hexGridSystemRef = gridSystem;
        _unitManagerRef = unitManagerRef;

        MaxHealth = data.maxHealth;
        CurrentHealth = MaxHealth;

        // ����: MoveSpeed (�ʴ� ���� ����) -> MovePointsPerTick (ƽ�� ���� �Ÿ� ����)
        // float worldUnitsPerSecond = data.moveSpeed;
        // float worldUnitsPerTick = worldUnitsPerSecond / TicksPerSecond; (TicksPerSecond�� DeterministicTimeManager���� �����;� ��)
        // MovePointsPerTick = Mathf.RoundToInt(worldUnitsPerTick * INTERNAL_DISTANCE_SCALE_FACTOR);
        // ���⼭�� ������ UnitData�� int MovePointsPerTick �� �ִٰ� �����ϰų�, ������ ���
        MovePointsPerTick = data.movePointsPerTick; // UnitData�� int movePointsPerTick �� �ִٰ� ����
        SizeInTiles = data.sizeInTiles; // ���� �׸��忡�� ������ �����ϴ� Ÿ�� ũ��

        AttackDamage = data.attackDamage;
        AttackRangeInTiles = data.attackRangeInTiles;
        AttackCooldownInTicks = data.attackCooldownInTicks;
        _currentAttackCooldownTimerInTicks = 0;
        VisionRangeInTiles = data.visionRangeInTiles;

        CreationCost = data.creationCost;
        CreationTimeInTicks = data.creationTimeInTicks;

        CurrentCubeCoords = initialCubeCoords;
        if (_hexGridSystemRef.TryGetTileAt(CurrentCubeCoords, out HexTile startTile))
        {
            _simulatedWorldPosition = startTile.worldPosition; // �ʱ� �ð��� ��ġ
        }
        else
        {
            // ��ȿ���� ���� ���� ��ġ ó��
            Debug.LogError($"���� {InstanceID}�� ���� ť�� ��ǥ {initialCubeCoords}�� ��ȿ���� �ʽ��ϴ�.");
            _simulatedWorldPosition = Vector3.zero; // �⺻��
        }
        if (_unitManagerRef == null)
        {
            Debug.LogError($"SimulatedObject {InstanceID}: UnitManager ������ ã�� �� �����ϴ�.");
        }
        _previousSimulatedWorldPosition = _simulatedWorldPosition;
        CurrentActionState = UnitActionState.Idle;
    }

    // --- ƽ ��� ���� ---
    public virtual void SimulateStep(float tickInterval) // tickInterval�� �ð��� ��ҳ� float ��� ����� �ʿ��� �� ���
    {
        _previousSimulatedWorldPosition = _simulatedWorldPosition; // ������ ���� ���� ��ġ ����

        if (CurrentActionState == UnitActionState.Dead) return;

        // ���� ��ٿ� ó��
        if (_currentAttackCooldownTimerInTicks > 0)
        {
            _currentAttackCooldownTimerInTicks--;
        }

        // 1. Purpose�� ���� Action ���� �� ���� ����
        DetermineActionStateBasedOnPurpose();

        // 2. Action�� ���� ���� �ൿ ó��
        switch (CurrentActionState)
        {
            case UnitActionState.Idle:
                // Idle �� �ֺ� ��Ȳ�� ���� �ڵ� ���� (��: �� �߰� �� �ڵ� ����)�� Purpose�� ������ �� ����
                break;
            case UnitActionState.Moving:
                HandleMovement(tickInterval); // �̵� �Ϸ� �� Purpose�� ���� ���� Action ����
                break;
            case UnitActionState.Attacking:
                HandleAttacking(tickInterval); // ���� ����� �װų� ������� Purpose ���� �Ǵ� Idle
                break;
            case UnitActionState.GatheringResource: // ����: GatheringResource -> Gathering
                // HandleGatheringState(tickInterval); // WorkerUnit ��� ����
                break;
            // ... ��Ÿ Action State ó�� ...
            default:
                Debug.LogWarning($"SimulatedObject: ActionState {CurrentActionState}�� ���� �ڵ鷯�� �����ϴ�.");
                break;
        }
    }

    protected virtual void DetermineActionStateBasedOnPurpose()
    {
        // PurposeState�� Dead, Idle �� ��Ȯ�� Action�� ���� �����ϴ� ���
        if (CurrentPurposeState == UnitPurposeState.Dead) {
            CurrentActionState = UnitActionState.Dead;
            return;
        }
        if (CurrentPurposeState == UnitPurposeState.Idle) {
            CurrentActionState = UnitActionState.Idle;
            return;
        }

        // �� Purpose�� ���� �ʿ��� Action�� ����
        switch (CurrentPurposeState)
        {
            case UnitPurposeState.MoveToPosition: // �ܼ� �̵� ����
                if (TargetLocation == CurrentCubeCoords) // �̹� �������� �����ߴٸ�
                {
                    CurrentPurposeState = UnitPurposeState.Idle; // ���� �޼�
                    CurrentActionState = UnitActionState.Idle;
                }
                else if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != TargetLocation)
                {
                    // ��ΰ� ���ų�, ���� ����� ���� �������� TargetLocation�� �ƴ϶�� ���ο� ��� ��û
                    RequestAndSetPath(TargetLocation);
                    CurrentActionState = UnitActionState.Moving;
                } else {
                     CurrentActionState = UnitActionState.Moving; // ��ΰ� �ְ� ���� �̵� ��
                }
                break;

            case UnitPurposeState.AttackingUnit:
                if (AttackTarget == null || !AttackTarget.IsAlive())
                {
                    CurrentPurposeState = UnitPurposeState.Idle; // ���� ����� ������ Idle
                    CurrentActionState = UnitActionState.Idle;
                    AttackTarget = null;
                }
                else
                {
                    int distanceToTarget = _hexGridSystemRef.GetDistance(CurrentCubeCoords, AttackTarget.CurrentCubeCoords);
                    if (distanceToTarget <= AttackRangeInTiles)
                    {
                        CurrentActionState = UnitActionState.Attacking; // ��Ÿ� ���� ������ ���� �ൿ
                        currentPath = null; // �̵� ����
                    }
                    else // ��Ÿ� �ۿ� ������ �̵� �ൿ
                    {
                        // �̹� �ش� Ÿ������ �̵� ������ Ȯ��
                        if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != AttackTarget.CurrentCubeCoords)
                        {
                             RequestAndSetPath(AttackTarget.CurrentCubeCoords);
                        }
                        CurrentActionState = UnitActionState.Moving;
                    }
                }
                break;

            case UnitPurposeState.AttackingPos:
                // ��ǥ ���� �ֺ��� ���� Ž���ϰ�, �ִٸ� AttackUnit���� Purpose ����
                // ���ٸ� �ش� �������� �̵� �� ��� �Ǵ� �ֺ� ���� (Action�� Moving �Ǵ� Idle/Patrolling)
                // �� �κ��� �� ��ü���� ���� �ʿ�
                // ����:
                SimulatedObject nearbyEnemy = FindNearestEnemyNear(TargetLocation); // �ֺ� �� Ž�� ����
                if (nearbyEnemy != null) {
                    AttackTarget = nearbyEnemy;
                    CurrentPurposeState = UnitPurposeState.AttackingUnit; // ���� ����
                    // ���� SimulateStep���� AttackUnit �������� ó����
                } else if (CurrentCubeCoords != TargetLocation) {
                    if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != TargetLocation) {
                        RequestAndSetPath(TargetLocation);
                    }
                    CurrentActionState = UnitActionState.Moving;
                } else {
                    CurrentActionState = UnitActionState.Idle; // �Ǵ� Holding/Patrolling Action
                }
                break;

            case UnitPurposeState.Holding:
                CurrentActionState = UnitActionState.Idle; // �⺻�� Idle
                                                           // �þ� ���� �� ���� ����� ���� ã��
                SimulatedObject nearestEnemyInVision = FindNearestEnemyNear(this.CurrentCubeCoords);

                if (nearestEnemyInVision != null)
                {
                    // ã�� ���� ���� ��Ÿ� ���� �ִ��� Ȯ��
                    int distanceToEnemy = _hexGridSystemRef.GetDistance(this.CurrentCubeCoords, nearestEnemyInVision.CurrentCubeCoords);
                    if (distanceToEnemy <= this.AttackRangeInTiles)
                    {
                        AttackTarget = nearestEnemyInVision;
                        CurrentActionState = UnitActionState.Attacking; // ���� ��Ÿ� ���� ������ ����
                    }
                    // else : �þ߿��� ������ ���� ��Ÿ� ���� ���, �ٰ����ų� �ٸ� �ൿ�� �� ���� ���� (����� Idle ����)
                }
                break;
            // WorkerUnit �� �Ļ� Ŭ�������� ó���� Purpose��
            case UnitPurposeState.GatheringResource:
            case UnitPurposeState.ReturnResource:
            case UnitPurposeState.Building:
                // �Ļ� Ŭ�������� CurrentActionState�� �����ϵ��� �����ϰų�,
                // ���⼭ �⺻���� �̵�/�۾� Action�� ������ �� ����.
                // ��: if (��ǥ���� != ������ġ) CurrentActionState = UnitActionState.Moving;
                //     else CurrentActionState = UnitActionState.Gathering; (�Ǵ� Building)
                break;

            default:
                Debug.LogWarning($"SimulatedObject: PurposeState {CurrentPurposeState}�� ���� Action ���� ������ �����ϴ�.");
                CurrentActionState = UnitActionState.Idle; // ������ġ
                break;
        }
    }

    // ��� ��û �� ���� ���� �޼���
    protected void RequestAndSetPath(Vector3Int targetCoordinate)
    {
        List<Vector3Int> path = RequestPathForUnit.FindPath(CurrentCubeCoords, targetCoordinate, _hexGridSystemRef);
        if (path != null && path.Count > 0)
        {
            currentPath = path;
            _currentPathIndex = 0;
            _accumulatedMovePoints = 0;
        }
        else
        {
            currentPath = null; // ��� �� ã��
            // ��� ��ã�� �� ���� Purpose�� Idle�� �����ϰų� �ٸ� ��ü �ൿ ���
            // CurrentPurposeState = UnitPurposeState.Idle;
            Debug.LogWarning($"���� {InstanceID}: {targetCoordinate}�� ���� ��θ� ã�� ���߽��ϴ�.");
        }
    }

    protected SimulatedObject FindNearestEnemyNear(Vector3Int searchOriginCubeCoord)
    {
        if (_unitManagerRef == null)
        {
            Debug.LogError($"SimulatedObject {InstanceID}: UnitManager ������ ã�� �� �����ϴ�.");
            return null;
        }

        SimulatedObject nearestEnemy = null;
        int minDistance = int.MaxValue;

        // UnitManager�� ���� ���� �÷��̾��� �� ���� ����� �����ɴϴ�.
        IEnumerable<SimulatedObject> allEnemyUnits = _unitManagerRef.GetEnemyUnits(this.OwnerPlayerId);

        foreach (SimulatedObject enemyUnit in allEnemyUnits)
        {
            // �ڱ� �ڽ��̰ų�, �̹� �׾��ų�, ��ȿ���� ���� ������ ����
            if (enemyUnit == this || !enemyUnit.IsAlive() || enemyUnit.CurrentActionState == UnitActionState.Dead) continue;

            int distance = _hexGridSystemRef.GetDistance(searchOriginCubeCoord, enemyUnit.CurrentCubeCoords);

            // ���� ������ �þ�(VisionRangeInTiles) ���� ���� ���� ���
            if (distance <= this.VisionRangeInTiles)
            {
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEnemy = enemyUnit;
                }
                // �Ÿ��� ���� ��� �߰����� �켱���� ���� (��: ü���� ���� ���� ��)�� ���� �� ����
            }
        }
        return nearestEnemy;
    }

    protected virtual void HandleMovement(float tickInterval)
    {
        Vector3Int nextTargetCubeCoord = currentPath[_currentPathIndex];
        HexTile nextTargetTile;

        if (currentPath == null || currentPath.Count == 0 || _currentPathIndex >= currentPath.Count)
        {
            CurrentPurposeState = UnitPurposeState.Idle; // �̵� ��ΰ� ������ Idle�� ��ȯ
            currentPath = null;
            return;
        }

        if (!_hexGridSystemRef.TryGetTileAt(nextTargetCubeCoord, out nextTargetTile) || !nextTargetTile.isWalkable)
        {
            // ���� ��ΰ� ��ȿ���� ���� (��: ��ֹ� �߻�) -> ��� ��Ž�� �Ǵ� ����
            Debug.LogWarning($"���� {InstanceID}: ���� ��� {nextTargetCubeCoord}�� ��ȿ���� �ʽ��ϴ�. �̵� ����.");
            CurrentPurposeState = UnitPurposeState.Idle;
            currentPath = null;
            return;
        }

        int pointsNeededToNextTile = INTERNAL_DISTANCE_SCALE_FACTOR; // 1Ÿ�� = 1000����Ʈ ����
        _accumulatedMovePoints += MovePointsPerTick;

        if (_accumulatedMovePoints >= pointsNeededToNextTile)
        {
            _accumulatedMovePoints -= pointsNeededToNextTile; // ����� ����Ʈ ����

            // ���� ��ġ ������Ʈ
            CurrentCubeCoords = nextTargetCubeCoord;
            _simulatedWorldPosition = nextTargetTile.worldPosition; // �ð��� ��ǥ�� ������Ʈ

            _currentPathIndex++;
            if (_currentPathIndex >= currentPath.Count)
            {
                currentPath = null;
                _currentPathIndex = 0; // ��� �Ϸ�
                Debug.Log($"���� {InstanceID} �̵� �Ϸ�: {CurrentCubeCoords}");
            }
        }
    }

    protected virtual void HandleAttacking(float tickInterval)
    {
        if (AttackTarget == null || !AttackTarget.IsAlive())
        {
            CurrentPurposeState = UnitPurposeState.Idle;
            AttackTarget = null;
            return;
        }

        int distanceToTargetInTiles = _hexGridSystemRef.GetDistance(CurrentCubeCoords, AttackTarget.CurrentCubeCoords);

        if (distanceToTargetInTiles <= AttackRangeInTiles)
        {
            // ���� ���� ��
            if (_currentAttackCooldownTimerInTicks <= 0)
            {
                PerformAttack(AttackTarget);
                _currentAttackCooldownTimerInTicks = AttackCooldownInTicks;
            }
        }
        else
        {
            Debug.Log($"���� {InstanceID}: ���� ��� {AttackTarget.InstanceID}��(��) ��Ÿ� �ۿ� �ֽ��ϴ�. (���� {distanceToTargetInTiles}, ��Ÿ� {AttackRangeInTiles})");
            if (currentPath == null || (currentPath.Count > 0 && currentPath[currentPath.Count - 1] != AttackTarget.CurrentCubeCoords))
            {
                SetMoveCommand(AttackTarget.CurrentCubeCoords);
            }
        }
    }

    //�ӽ� �ð�ȭ
    private void Update()
    {
        UpdateVisuals(1f); // ���÷� 0.5f ���� ��� (������ DeterministicTimeManager���� ���� ���� ����)
    }
    // --- ������ ���� ---
    public virtual void UpdateVisuals(float interpolationFactor)
    {
        // _simulatedWorldPosition�� SimulateStep���� "�̹� ƽ�� ���� ���� ��ǥ ��ġ"�� ������Ʈ��.
        // _previousSimulatedWorldPosition�� "���� ƽ�� ���� ���� ��ǥ ��ġ".
        // ���� �� Lerp�� ���� Ÿ�� �߽ɿ��� ���� ��ǥ Ÿ�� �߽����� �ε巴�� �̵��ϴ� ���� ������.
        transform.position = Vector3.Lerp(_previousSimulatedWorldPosition, _simulatedWorldPosition, interpolationFactor);
        // ȸ�� ���� (�ʿ��)
        if (CurrentActionState == UnitActionState.Moving && currentPath != null && _currentPathIndex < currentPath.Count)
        {
            if (_hexGridSystemRef.TryGetTileAt(currentPath[_currentPathIndex], out HexTile nextTile))
            {
                Vector3 direction = (nextTile.worldPosition - _simulatedWorldPosition).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                    // Y�� ȸ���� ��� (XZ ��� �̵�)
                    targetRotation.x = 0;
                    targetRotation.z = 0;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f); // ȸ�� �ӵ� ����
                }
            }
        }
    }

    // --- ��� ���� ---
    public virtual void SetMoveCommand(Vector3Int TargetCoordinate)
    {
        TargetLocation = TargetCoordinate; // �̵� ��ǥ ��ǥ ����
        List<Vector3Int> path = RequestPathForUnit.FindPath(CurrentCubeCoords, TargetCoordinate, _hexGridSystemRef); // �̰͸� �ű���.
        if (path == null || path.Count == 0)
        {
            CurrentPurposeState = UnitPurposeState.Idle;
            currentPath = null;
            return;
        }
        currentPath = new List<Vector3Int>(path);
        _currentPathIndex = 0;
        _accumulatedMovePoints = 0; // �� ��� ���� �� �̵� ����Ʈ �ʱ�ȭ
        CurrentPurposeState = UnitPurposeState.MoveToPosition;
        AttackTarget = null; // �̵� ���� �� ���� Ÿ�� ����

        // ù ��° ��� ������ Ÿ�� ������ ������ �ð��� ��ǥ �ʱ�ȭ
        if (_hexGridSystemRef.TryGetTileAt(currentPath[_currentPathIndex], out HexTile firstPathTile))
        {
            // SimulateStep���� _simulatedWorldPosition�� ���� �̵��� �ݿ��ϵ��� ������Ʈ�ǹǷ�,
            // ���⼭�� _previousSimulatedWorldPosition�� ���� ��ġ�� �����Ͽ� ���� �������� ����.
            // _simulatedWorldPosition�� SimulateStep���� HandleMovement�� ���� ������Ʈ�� ����.
            // _previousSimulatedWorldPosition = transform.position; // ���� ������ ��ġ
            // _simulatedWorldPosition = transform.position; // ������ ��ġ�� ����� �ʱ�ȭ
        }
        Debug.Log($"���� {InstanceID} �̵� ��� ����. ��� ����: {currentPath.Count}");
    }

    public virtual void SetAttackUnitCommand(SimulatedObject target)
    {
        if (target == null || !target.IsAlive())
        {
            CurrentPurposeState = UnitPurposeState.Idle;
            AttackTarget = null;
            return;
        }
        AttackTarget = target;
        CurrentPurposeState = UnitPurposeState.AttackingUnit;
        currentPath = null; // ���� ��� �� ���� �̵� ��� ���
        Debug.Log($"���� {InstanceID} ���� ��� ����. ���: {target.InstanceID}");
    }

    public virtual void SetAttackPositionCommand(Vector3Int targetPosition)
    {

        TargetLocation = targetPosition;
        CurrentPurposeState = UnitPurposeState.AttackingPos;
        currentPath = null; // ���� ��ġ ��� �� ���� �̵� ��� ���
        Debug.Log($"���� {InstanceID} ��ġ ���� ��� ����. ��ġ: {targetPosition}");
    }

    // --- �׼� ---
    public virtual void TakeDamage(int damageAmount)
    {
        if (CurrentActionState == UnitActionState.Dead) return;

        CurrentHealth -= damageAmount;
        Debug.Log($"���� {InstanceID} ������ {damageAmount} ����. ���� ü��: {CurrentHealth}");
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            CurrentActionState = UnitActionState.Dead;
            // TODO: UnitManager�� ��� �˸� �Ǵ� �Ҹ� ó�� ��û
            Debug.Log($"���� {InstanceID} ���.");
            // gameObject.SetActive(false); // ������ ��Ȱ��ȭ (�����δ� Ǯ���̳� �ı�)
        }
    }

    protected virtual void PerformAttack(SimulatedObject target)
    {
        Debug.Log($"���� {InstanceID}��(��) ���� {target.InstanceID}��(��) ����!");
        target.TakeDamage(AttackDamage);
        // TODO: ���� ����Ʈ �߻� ��� (�������� ����Ʈ �ý��ۿ�)
    }



    public virtual bool IsAlive()
    {
        return CurrentHealth > 0;
    }

    protected virtual void OnDeath()
    {
        Debug.Log($"(ID: {InstanceID}) �ı���!");

    }

    public void Select()
    {
        if (SelectionProjector != null)
        {
            SelectionProjector.gameObject.SetActive(true);
        }
    }

    public void Deselect()
    {
        if (SelectionProjector != null)
        {
            SelectionProjector.gameObject.SetActive(false);
        }
    }
}

