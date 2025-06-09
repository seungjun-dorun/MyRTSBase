using UnityEngine;

public class ResourceNode : SimulatedObject
{
    [Header("Resource Node Settings")]
    [Tooltip("이 노드가 제공하는 자원의 종류 (ResourceManager에서 사용하는 이름과 일치해야 함)")]
    public string resourceType = "Mineral"; // 또는 ResourceType enum 사용

    [Tooltip("이 노드에 초기에 매장된 총 자원량")]
    public int initialAmount = 1000;

    [Tooltip("한 번의 채취 작업(gather_tick)으로 얻을 수 있는 자원의 양")]
    public int amountPerGatherTick = 10;

    //더이상 사용하지 않음.
    //[Tooltip("일꾼이 한 번 상호작용(채취 명령 수행 완료) 시 최대로 가져갈 수 있는 양. 0이면 제한 없음 (amountPerGatherTick만 적용)")]
    //public int maxCarryAmountPerTrip = 50; // 예: 일꾼이 한 번에 50까지만 들고 갈 수 있음

    [Header("State (Read-Only)")]
    [SerializeField] // 인스펙터에서 보기 위해
    private int _currentAmount;
    public int CurrentAmount => _currentAmount;

    public bool IsDepleted => _currentAmount <= 0; // 자원이 고갈되었는지 여부

    // --- 시각적 요소 (선택 사항) ---
    // public GameObject depletedVisual; // 자원 고갈 시 보여줄 다른 모습 (예: 빈 광맥)
    // private MeshRenderer _originalVisual;

    // --- 이벤트 (선택 사항) ---
    public delegate void OnResourceDepletedHandler(ResourceNode depletedNode);
    public event OnResourceDepletedHandler OnResourceDepleted;

    public delegate void OnAmountChangedHandler(ResourceNode node, int newAmount, int oldAmount);
    public event OnAmountChangedHandler OnAmountChanged;


    #region Unity Lifecycle & Initialization

    void Awake()
    {
        _currentAmount = initialAmount;
        // _originalVisual = GetComponent<MeshRenderer>(); // 또는 주된 시각적 요소 참조
        // if (depletedVisual != null) depletedVisual.SetActive(false);
    }

    #endregion

    #region Core Gathering Logic (결정론적)

    /// <summary>
    /// 일꾼 유닛이 이 자원 노드로부터 자원을 채취하려고 시도합니다.
    /// 이 함수는 결정론적 게임 로직의 일부로, 특정 틱에 호출되어야 합니다.
    /// (예: 일꾼 유닛의 SimulateStep 내에서, 자원 노드에 도달하고 채취 작업 틱이 되었을 때)
    /// </summary>
    /// <param name="workerCarryCapacity">현재 일꾼이 더 들 수 있는 최대 양.</param>
    /// <returns>실제로 채취한 자원의 양.</returns>
    public int GatherResource(int workerCarryCapacity)
    {
        if (IsDepleted)
        {
            return 0; // 이미 고갈됨
        }

        // 한 번의 채취 틱으로 얻을 수 있는 양
        int amountToGatherThisTick = amountPerGatherTick;

        // 노드에 남은 양보다 많이 채취할 수 없음
        amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, _currentAmount);

        /* 더이상 사용되지 않음.
        // 일꾼이 한 번에 가져갈 수 있는 최대 양 (maxCarryAmountPerTrip)과
        // 일꾼이 현재 더 들 수 있는 양 (workerCarryCapacity) 중 더 작은 값으로 제한
        if (maxCarryAmountPerTrip > 0) // maxCarryAmountPerTrip이 설정된 경우
        {
            // 이 로직은 일꾼이 한 번 "방문"했을 때 가져갈 수 있는 총량에 대한 것.
            // GatherResource는 "한 틱"에 채취하는 양을 의미하므로,
            // 이 부분은 일꾼 유닛의 로직에서 (여러 틱에 걸쳐) 관리하는 것이 더 적합할 수 있음.
            // 여기서는 한 틱에 amountPerGatherTick만큼만 채취하고,
            // 일꾼이 여러 틱에 걸쳐 maxCarryAmountPerTrip까지 채우도록 하는 것이 일반적.
            // 따라서, workerCarryCapacity는 "이 틱에서" 채취할 양을 제한하는 용도로 사용.
            amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, workerCarryCapacity);
        }
        else
        {
            // maxCarryAmountPerTrip 제한이 없다면, 일꾼이 들 수 있는 만큼만 제한
            amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, workerCarryCapacity);
        }
        */


        if (amountToGatherThisTick <= 0) // 채취할 양이 없으면 (일꾼이 꽉 찼거나, 노드가 거의 비었거나)
        {
            return 0;
        }

        int oldAmount = _currentAmount;
        _currentAmount -= amountToGatherThisTick;

        OnAmountChanged?.Invoke(this, _currentAmount, oldAmount);

        if (_currentAmount <= 0)
        {
            _currentAmount = 0; // 음수 방지
            HandleDepletion();
        }

        // Debug.Log($"자원 노드 {gameObject.name}: {amountToGatherThisTick} {resourceType} 채취됨. 남은 양: {_currentAmount}");
        return amountToGatherThisTick;
    }

    /// <summary>
    /// 자원이 고갈되었을 때 호출되는 처리입니다.
    /// </summary>
    private void HandleDepletion()
    {
        // Debug.Log($"자원 노드 {gameObject.name} ({resourceType}) 고갈됨.");
        OnResourceDepleted?.Invoke(this);

        // 시각적 변경 (선택 사항)
        // if (_originalVisual != null) _originalVisual.enabled = false;
        // if (depletedVisual != null) depletedVisual.SetActive(true);

        // 더 이상 상호작용 불가능하도록 설정 (예: Collider 비활성화)
        // Collider col = GetComponent<Collider>();
        // if (col != null) col.enabled = false;
        // 또는 isWalkable 같은 플래그를 false로 변경 (HexGridSystem에서 참조한다면)
    }

    #endregion

    #region Public Utility (선택 사항)
    public string GetResourceType()
    {
        return resourceType;
    }
    #endregion

    #region Editor Gizmos (선택 사항 - 남은 양 시각화 등)
#if UNITY_EDITOR
    void OnDrawGizmosSelected() // 또는 OnDrawGizmos
    {
        if (Application.isPlaying) // 실행 중에만 현재 양 표시
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"{resourceType}\n{_currentAmount} / {initialAmount}");
        }
        else // 실행 전에는 초기 양 표시
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"{resourceType}\n{initialAmount}");
        }
    }
#endif
    #endregion
}