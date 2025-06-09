using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using System.Linq;

// UnitData는 여전히 float을 가질 수 있지만, Initialize에서 int로 변환되거나,
// UnitData 자체도 int 기반으로 설계될 수 있음. 여기서는 Initialize에서 변환 가정.
// 예시: public class UnitData : ScriptableObject { public float initialHealth; public float initialMoveSpeed; ... }

public class SimulatedObject : MonoBehaviour
{
    [Header("Core Stats (from UnitData)")]
    public int InstanceID { get; protected set; }
    public ulong OwnerPlayerId { get; protected set; }

    // --- 정수 기반 핵심 능력치 ---
    public int MaxHealth { get; protected set; }
    public int CurrentHealth { get; protected set; }

    // 이동 속도: 1틱당 이동할 수 있는 "이동 포인트" 또는 "내부 거리 단위".
    // 실제 월드 거리는 (MovePointsPerTick * INTERNAL_DISTANCE_SCALE_FACTOR) / TICKS_PER_SECOND 로 환산 가능.
    public int MovePointsPerTick { get; protected set; }
    private const int INTERNAL_DISTANCE_SCALE_FACTOR = 1000; // 예: 1 그리드 유닛 = 1000 내부 유닛

    public int AttackDamage { get; protected set; }
    public int AttackRangeInTiles { get; protected set; } // 육각 그리드 타일 단위 사거리
    public int AttackCooldownInTicks { get; protected set; } // 틱 단위 공격 쿨다운

    protected int _currentAttackCooldownTimerInTicks;

    public int VisionRangeInTiles { get; protected set; }

    public enum UnitActionState { Idle, Moving, Attacking, GatheringResource, Building, Dead}
    public enum UnitPurposeState { Idle, MoveToPosition, Patrol, AttackingUnit, AttackingPos, Holding, GatheringResource, ReturnResource, Building, Dead}
    public UnitActionState CurrentActionState { get; set; }
    public UnitPurposeState CurrentPurposeState { get; set; }

    public List<ResourceCost> CreationCost { get; protected set; }
    public int CreationTimeInTicks { get; protected set; } // 생성 시간 (틱 단위)


    // --- 논리적 위치 및 이동 관련 ---
    public Vector3Int CurrentCubeCoords { get; protected set; }
    protected Vector3 _simulatedWorldPosition; // 시각적 보간용 (float 유지)
    protected Vector3 _previousSimulatedWorldPosition; // 시각적 보간용 (float 유지)

    public List<Vector3Int> currentPath = null;
    protected int _currentPathIndex = 0;
    protected int _accumulatedMovePoints = 0; // 현재 틱에서 이동하고 남은 이동 포인트 (다음 틱 이월 X, 틱 내에서만 사용)
    
    public Vector3Int SizeInTiles { get; protected set; } // 유닛이 차지하는 타일 크기 (육각 그리드에서의 크기)

    // --- 각 목적에 필요한 추가 데이터 ---
    public Vector3Int TargetLocation { get; set; }  // 이동, 건설, 정찰 위치 등
    public Vector3Int SecondaryTargetLocation { get; set; }  // 이동, 건설, 정찰 위치 등
    public SimulatedObject AttackTarget { get; set; }
    public SimulatedObject TargetEntity { get; set; } // 공격, 추적, 수리 대상 등

    // --- 참조 ---
    protected HexGridSystem _hexGridSystemRef; // HexGridSystem 참조
    protected UnitManager _unitManagerRef;

    // --- 선택관련 ---
    public GameObject SelectionProjector;

    void Awake()
    {
        if (SelectionProjector != null)
        {
            SelectionProjector.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("SelectionProjector가 Inspector에서 할당되지 않았습니다.", this.gameObject);
        }
    }
    // --- 초기화 ---
    public virtual void Initialize(int instanceId, ulong ownerId, UnitData data, HexGridSystem gridSystem, UnitManager unitManagerRef, Vector3Int initialCubeCoords)
    {
        InstanceID = instanceId;
        OwnerPlayerId = ownerId;
        _hexGridSystemRef = gridSystem;
        _unitManagerRef = unitManagerRef;

        MaxHealth = data.maxHealth;
        CurrentHealth = MaxHealth;

        // 예시: MoveSpeed (초당 월드 유닛) -> MovePointsPerTick (틱당 내부 거리 단위)
        // float worldUnitsPerSecond = data.moveSpeed;
        // float worldUnitsPerTick = worldUnitsPerSecond / TicksPerSecond; (TicksPerSecond는 DeterministicTimeManager에서 가져와야 함)
        // MovePointsPerTick = Mathf.RoundToInt(worldUnitsPerTick * INTERNAL_DISTANCE_SCALE_FACTOR);
        // 여기서는 간단히 UnitData에 int MovePointsPerTick 가 있다고 가정하거나, 고정값 사용
        MovePointsPerTick = data.movePointsPerTick; // UnitData에 int movePointsPerTick 가 있다고 가정
        SizeInTiles = data.sizeInTiles; // 육각 그리드에서 유닛이 차지하는 타일 크기

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
            _simulatedWorldPosition = startTile.worldPosition; // 초기 시각적 위치
        }
        else
        {
            // 유효하지 않은 시작 위치 처리
            Debug.LogError($"유닛 {InstanceID}의 시작 큐브 좌표 {initialCubeCoords}가 유효하지 않습니다.");
            _simulatedWorldPosition = Vector3.zero; // 기본값
        }
        if (_unitManagerRef == null)
        {
            Debug.LogError($"SimulatedObject {InstanceID}: UnitManager 참조를 찾을 수 없습니다.");
        }
        _previousSimulatedWorldPosition = _simulatedWorldPosition;
        CurrentActionState = UnitActionState.Idle;
    }

    // --- 틱 기반 로직 ---
    public virtual void SimulateStep(float tickInterval) // tickInterval은 시각적 요소나 float 기반 계산이 필요할 때 사용
    {
        _previousSimulatedWorldPosition = _simulatedWorldPosition; // 보간을 위해 이전 위치 저장

        if (CurrentActionState == UnitActionState.Dead) return;

        // 공격 쿨다운 처리
        if (_currentAttackCooldownTimerInTicks > 0)
        {
            _currentAttackCooldownTimerInTicks--;
        }

        // 1. Purpose에 따른 Action 결정 및 상태 전이
        DetermineActionStateBasedOnPurpose();

        // 2. Action에 따른 실제 행동 처리
        switch (CurrentActionState)
        {
            case UnitActionState.Idle:
                // Idle 시 주변 상황에 따른 자동 반응 (예: 적 발견 시 자동 공격)은 Purpose를 변경할 수 있음
                break;
            case UnitActionState.Moving:
                HandleMovement(tickInterval); // 이동 완료 시 Purpose에 따라 다음 Action 결정
                break;
            case UnitActionState.Attacking:
                HandleAttacking(tickInterval); // 공격 대상이 죽거나 사라지면 Purpose 변경 또는 Idle
                break;
            case UnitActionState.GatheringResource: // 예시: GatheringResource -> Gathering
                // HandleGatheringState(tickInterval); // WorkerUnit 등에서 구현
                break;
            // ... 기타 Action State 처리 ...
            default:
                Debug.LogWarning($"SimulatedObject: ActionState {CurrentActionState}에 대한 핸들러가 없습니다.");
                break;
        }
    }

    protected virtual void DetermineActionStateBasedOnPurpose()
    {
        // PurposeState가 Dead, Idle 등 명확한 Action을 직접 지시하는 경우
        if (CurrentPurposeState == UnitPurposeState.Dead) {
            CurrentActionState = UnitActionState.Dead;
            return;
        }
        if (CurrentPurposeState == UnitPurposeState.Idle) {
            CurrentActionState = UnitActionState.Idle;
            return;
        }

        // 각 Purpose에 따라 필요한 Action을 결정
        switch (CurrentPurposeState)
        {
            case UnitPurposeState.MoveToPosition: // 단순 이동 목적
                if (TargetLocation == CurrentCubeCoords) // 이미 목적지에 도착했다면
                {
                    CurrentPurposeState = UnitPurposeState.Idle; // 목적 달성
                    CurrentActionState = UnitActionState.Idle;
                }
                else if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != TargetLocation)
                {
                    // 경로가 없거나, 현재 경로의 최종 목적지가 TargetLocation이 아니라면 새로운 경로 요청
                    RequestAndSetPath(TargetLocation);
                    CurrentActionState = UnitActionState.Moving;
                } else {
                     CurrentActionState = UnitActionState.Moving; // 경로가 있고 아직 이동 중
                }
                break;

            case UnitPurposeState.AttackingUnit:
                if (AttackTarget == null || !AttackTarget.IsAlive())
                {
                    CurrentPurposeState = UnitPurposeState.Idle; // 공격 대상이 없으면 Idle
                    CurrentActionState = UnitActionState.Idle;
                    AttackTarget = null;
                }
                else
                {
                    int distanceToTarget = _hexGridSystemRef.GetDistance(CurrentCubeCoords, AttackTarget.CurrentCubeCoords);
                    if (distanceToTarget <= AttackRangeInTiles)
                    {
                        CurrentActionState = UnitActionState.Attacking; // 사거리 내에 있으면 공격 행동
                        currentPath = null; // 이동 중지
                    }
                    else // 사거리 밖에 있으면 이동 행동
                    {
                        // 이미 해당 타겟으로 이동 중인지 확인
                        if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != AttackTarget.CurrentCubeCoords)
                        {
                             RequestAndSetPath(AttackTarget.CurrentCubeCoords);
                        }
                        CurrentActionState = UnitActionState.Moving;
                    }
                }
                break;

            case UnitPurposeState.AttackingPos:
                // 목표 지점 주변의 적을 탐색하고, 있다면 AttackUnit으로 Purpose 변경
                // 없다면 해당 지점으로 이동 후 대기 또는 주변 순찰 (Action은 Moving 또는 Idle/Patrolling)
                // 이 부분은 더 구체적인 로직 필요
                // 예시:
                SimulatedObject nearbyEnemy = FindNearestEnemyNear(TargetLocation); // 주변 적 탐색 로직
                if (nearbyEnemy != null) {
                    AttackTarget = nearbyEnemy;
                    CurrentPurposeState = UnitPurposeState.AttackingUnit; // 목적 변경
                    // 다음 SimulateStep에서 AttackUnit 로직으로 처리됨
                } else if (CurrentCubeCoords != TargetLocation) {
                    if (currentPath == null || currentPath.Count == 0 || currentPath[currentPath.Count-1] != TargetLocation) {
                        RequestAndSetPath(TargetLocation);
                    }
                    CurrentActionState = UnitActionState.Moving;
                } else {
                    CurrentActionState = UnitActionState.Idle; // 또는 Holding/Patrolling Action
                }
                break;

            case UnitPurposeState.Holding:
                CurrentActionState = UnitActionState.Idle; // 기본은 Idle
                                                           // 시야 범위 내 가장 가까운 적을 찾음
                SimulatedObject nearestEnemyInVision = FindNearestEnemyNear(this.CurrentCubeCoords);

                if (nearestEnemyInVision != null)
                {
                    // 찾은 적이 공격 사거리 내에 있는지 확인
                    int distanceToEnemy = _hexGridSystemRef.GetDistance(this.CurrentCubeCoords, nearestEnemyInVision.CurrentCubeCoords);
                    if (distanceToEnemy <= this.AttackRangeInTiles)
                    {
                        AttackTarget = nearestEnemyInVision;
                        CurrentActionState = UnitActionState.Attacking; // 공격 사거리 내에 있으면 공격
                    }
                    // else : 시야에는 있지만 공격 사거리 밖인 경우, 다가가거나 다른 행동을 할 수도 있음 (현재는 Idle 유지)
                }
                break;
            // WorkerUnit 등 파생 클래스에서 처리할 Purpose들
            case UnitPurposeState.GatheringResource:
            case UnitPurposeState.ReturnResource:
            case UnitPurposeState.Building:
                // 파생 클래스에서 CurrentActionState를 설정하도록 위임하거나,
                // 여기서 기본적인 이동/작업 Action을 설정할 수 있음.
                // 예: if (목표지점 != 현재위치) CurrentActionState = UnitActionState.Moving;
                //     else CurrentActionState = UnitActionState.Gathering; (또는 Building)
                break;

            default:
                Debug.LogWarning($"SimulatedObject: PurposeState {CurrentPurposeState}에 대한 Action 결정 로직이 없습니다.");
                CurrentActionState = UnitActionState.Idle; // 안전장치
                break;
        }
    }

    // 경로 요청 및 설정 헬퍼 메서드
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
            currentPath = null; // 경로 못 찾음
            // 경로 못찾을 시 현재 Purpose를 Idle로 변경하거나 다른 대체 행동 고려
            // CurrentPurposeState = UnitPurposeState.Idle;
            Debug.LogWarning($"유닛 {InstanceID}: {targetCoordinate}로 가는 경로를 찾지 못했습니다.");
        }
    }

    protected SimulatedObject FindNearestEnemyNear(Vector3Int searchOriginCubeCoord)
    {
        if (_unitManagerRef == null)
        {
            Debug.LogError($"SimulatedObject {InstanceID}: UnitManager 참조를 찾을 수 없습니다.");
            return null;
        }

        SimulatedObject nearestEnemy = null;
        int minDistance = int.MaxValue;

        // UnitManager를 통해 현재 플레이어의 적 유닛 목록을 가져옵니다.
        IEnumerable<SimulatedObject> allEnemyUnits = _unitManagerRef.GetEnemyUnits(this.OwnerPlayerId);

        foreach (SimulatedObject enemyUnit in allEnemyUnits)
        {
            // 자기 자신이거나, 이미 죽었거나, 유효하지 않은 유닛은 제외
            if (enemyUnit == this || !enemyUnit.IsAlive() || enemyUnit.CurrentActionState == UnitActionState.Dead) continue;

            int distance = _hexGridSystemRef.GetDistance(searchOriginCubeCoord, enemyUnit.CurrentCubeCoords);

            // 현재 유닛의 시야(VisionRangeInTiles) 범위 내의 적만 고려
            if (distance <= this.VisionRangeInTiles)
            {
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEnemy = enemyUnit;
                }
                // 거리가 같은 경우 추가적인 우선순위 로직 (예: 체력이 가장 낮은 적)을 넣을 수 있음
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
            CurrentPurposeState = UnitPurposeState.Idle; // 이동 경로가 없으면 Idle로 전환
            currentPath = null;
            return;
        }

        if (!_hexGridSystemRef.TryGetTileAt(nextTargetCubeCoord, out nextTargetTile) || !nextTargetTile.isWalkable)
        {
            // 다음 경로가 유효하지 않음 (예: 장애물 발생) -> 경로 재탐색 또는 정지
            Debug.LogWarning($"유닛 {InstanceID}: 다음 경로 {nextTargetCubeCoord}가 유효하지 않습니다. 이동 중지.");
            CurrentPurposeState = UnitPurposeState.Idle;
            currentPath = null;
            return;
        }

        int pointsNeededToNextTile = INTERNAL_DISTANCE_SCALE_FACTOR; // 1타일 = 1000포인트 가정
        _accumulatedMovePoints += MovePointsPerTick;

        if (_accumulatedMovePoints >= pointsNeededToNextTile)
        {
            _accumulatedMovePoints -= pointsNeededToNextTile; // 사용한 포인트 차감

            // 논리적 위치 업데이트
            CurrentCubeCoords = nextTargetCubeCoord;
            _simulatedWorldPosition = nextTargetTile.worldPosition; // 시각적 목표도 업데이트

            _currentPathIndex++;
            if (_currentPathIndex >= currentPath.Count)
            {
                currentPath = null;
                _currentPathIndex = 0; // 경로 완료
                Debug.Log($"유닛 {InstanceID} 이동 완료: {CurrentCubeCoords}");
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
            // 공격 범위 내
            if (_currentAttackCooldownTimerInTicks <= 0)
            {
                PerformAttack(AttackTarget);
                _currentAttackCooldownTimerInTicks = AttackCooldownInTicks;
            }
        }
        else
        {
            Debug.Log($"유닛 {InstanceID}: 공격 대상 {AttackTarget.InstanceID}이(가) 사거리 밖에 있습니다. (현재 {distanceToTargetInTiles}, 사거리 {AttackRangeInTiles})");
            if (currentPath == null || (currentPath.Count > 0 && currentPath[currentPath.Count - 1] != AttackTarget.CurrentCubeCoords))
            {
                SetMoveCommand(AttackTarget.CurrentCubeCoords);
            }
        }
    }

    //임시 시각화
    private void Update()
    {
        UpdateVisuals(1f); // 예시로 0.5f 보간 사용 (실제는 DeterministicTimeManager에서 보간 팩터 제공)
    }
    // --- 렌더링 보간 ---
    public virtual void UpdateVisuals(float interpolationFactor)
    {
        // _simulatedWorldPosition은 SimulateStep에서 "이번 틱의 최종 논리적 목표 위치"로 업데이트됨.
        // _previousSimulatedWorldPosition은 "이전 틱의 최종 논리적 목표 위치".
        // 따라서 이 Lerp는 이전 타일 중심에서 현재 목표 타일 중심으로 부드럽게 이동하는 것을 보여줌.
        transform.position = Vector3.Lerp(_previousSimulatedWorldPosition, _simulatedWorldPosition, interpolationFactor);
        // 회전 보간 (필요시)
        if (CurrentActionState == UnitActionState.Moving && currentPath != null && _currentPathIndex < currentPath.Count)
        {
            if (_hexGridSystemRef.TryGetTileAt(currentPath[_currentPathIndex], out HexTile nextTile))
            {
                Vector3 direction = (nextTile.worldPosition - _simulatedWorldPosition).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                    // Y축 회전만 사용 (XZ 평면 이동)
                    targetRotation.x = 0;
                    targetRotation.z = 0;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f); // 회전 속도 조절
                }
            }
        }
    }

    // --- 명령 수신 ---
    public virtual void SetMoveCommand(Vector3Int TargetCoordinate)
    {
        TargetLocation = TargetCoordinate; // 이동 목표 좌표 설정
        List<Vector3Int> path = RequestPathForUnit.FindPath(CurrentCubeCoords, TargetCoordinate, _hexGridSystemRef); // 이것만 옮기자.
        if (path == null || path.Count == 0)
        {
            CurrentPurposeState = UnitPurposeState.Idle;
            currentPath = null;
            return;
        }
        currentPath = new List<Vector3Int>(path);
        _currentPathIndex = 0;
        _accumulatedMovePoints = 0; // 새 경로 시작 시 이동 포인트 초기화
        CurrentPurposeState = UnitPurposeState.MoveToPosition;
        AttackTarget = null; // 이동 시작 시 공격 타겟 해제

        // 첫 번째 경로 지점의 타일 정보를 가져와 시각적 목표 초기화
        if (_hexGridSystemRef.TryGetTileAt(currentPath[_currentPathIndex], out HexTile firstPathTile))
        {
            // SimulateStep에서 _simulatedWorldPosition이 실제 이동을 반영하도록 업데이트되므로,
            // 여기서는 _previousSimulatedWorldPosition을 현재 위치로 설정하여 보간 시작점을 잡음.
            // _simulatedWorldPosition은 SimulateStep에서 HandleMovement를 통해 업데이트될 것임.
            // _previousSimulatedWorldPosition = transform.position; // 현재 렌더링 위치
            // _simulatedWorldPosition = transform.position; // 로직상 위치도 현재로 초기화
        }
        Debug.Log($"유닛 {InstanceID} 이동 명령 받음. 경로 길이: {currentPath.Count}");
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
        currentPath = null; // 공격 명령 시 기존 이동 경로 취소
        Debug.Log($"유닛 {InstanceID} 공격 명령 받음. 대상: {target.InstanceID}");
    }

    public virtual void SetAttackPositionCommand(Vector3Int targetPosition)
    {

        TargetLocation = targetPosition;
        CurrentPurposeState = UnitPurposeState.AttackingPos;
        currentPath = null; // 공격 위치 명령 시 기존 이동 경로 취소
        Debug.Log($"유닛 {InstanceID} 위치 공격 명령 받음. 위치: {targetPosition}");
    }

    // --- 액션 ---
    public virtual void TakeDamage(int damageAmount)
    {
        if (CurrentActionState == UnitActionState.Dead) return;

        CurrentHealth -= damageAmount;
        Debug.Log($"유닛 {InstanceID} 데미지 {damageAmount} 받음. 현재 체력: {CurrentHealth}");
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            CurrentActionState = UnitActionState.Dead;
            // TODO: UnitManager에 사망 알림 또는 소멸 처리 요청
            Debug.Log($"유닛 {InstanceID} 사망.");
            // gameObject.SetActive(false); // 간단한 비활성화 (실제로는 풀링이나 파괴)
        }
    }

    protected virtual void PerformAttack(SimulatedObject target)
    {
        Debug.Log($"유닛 {InstanceID}이(가) 유닛 {target.InstanceID}을(를) 공격!");
        target.TakeDamage(AttackDamage);
        // TODO: 공격 이펙트 발생 명령 (결정론적 이펙트 시스템에)
    }



    public virtual bool IsAlive()
    {
        return CurrentHealth > 0;
    }

    protected virtual void OnDeath()
    {
        Debug.Log($"(ID: {InstanceID}) 파괴됨!");

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

