using UnityEngine;

public class ResourceNode : SimulatedObject
{
    [Header("Resource Node Settings")]
    [Tooltip("�� ��尡 �����ϴ� �ڿ��� ���� (ResourceManager���� ����ϴ� �̸��� ��ġ�ؾ� ��)")]
    public string resourceType = "Mineral"; // �Ǵ� ResourceType enum ���

    [Tooltip("�� ��忡 �ʱ⿡ ����� �� �ڿ���")]
    public int initialAmount = 1000;

    [Tooltip("�� ���� ä�� �۾�(gather_tick)���� ���� �� �ִ� �ڿ��� ��")]
    public int amountPerGatherTick = 10;

    //���̻� ������� ����.
    //[Tooltip("�ϲ��� �� �� ��ȣ�ۿ�(ä�� ��� ���� �Ϸ�) �� �ִ�� ������ �� �ִ� ��. 0�̸� ���� ���� (amountPerGatherTick�� ����)")]
    //public int maxCarryAmountPerTrip = 50; // ��: �ϲ��� �� ���� 50������ ��� �� �� ����

    [Header("State (Read-Only)")]
    [SerializeField] // �ν����Ϳ��� ���� ����
    private int _currentAmount;
    public int CurrentAmount => _currentAmount;

    public bool IsDepleted => _currentAmount <= 0; // �ڿ��� ���Ǿ����� ����

    // --- �ð��� ��� (���� ����) ---
    // public GameObject depletedVisual; // �ڿ� �� �� ������ �ٸ� ��� (��: �� ����)
    // private MeshRenderer _originalVisual;

    // --- �̺�Ʈ (���� ����) ---
    public delegate void OnResourceDepletedHandler(ResourceNode depletedNode);
    public event OnResourceDepletedHandler OnResourceDepleted;

    public delegate void OnAmountChangedHandler(ResourceNode node, int newAmount, int oldAmount);
    public event OnAmountChangedHandler OnAmountChanged;


    #region Unity Lifecycle & Initialization

    void Awake()
    {
        _currentAmount = initialAmount;
        // _originalVisual = GetComponent<MeshRenderer>(); // �Ǵ� �ֵ� �ð��� ��� ����
        // if (depletedVisual != null) depletedVisual.SetActive(false);
    }

    #endregion

    #region Core Gathering Logic (��������)

    /// <summary>
    /// �ϲ� ������ �� �ڿ� ���κ��� �ڿ��� ä���Ϸ��� �õ��մϴ�.
    /// �� �Լ��� �������� ���� ������ �Ϻη�, Ư�� ƽ�� ȣ��Ǿ�� �մϴ�.
    /// (��: �ϲ� ������ SimulateStep ������, �ڿ� ��忡 �����ϰ� ä�� �۾� ƽ�� �Ǿ��� ��)
    /// </summary>
    /// <param name="workerCarryCapacity">���� �ϲ��� �� �� �� �ִ� �ִ� ��.</param>
    /// <returns>������ ä���� �ڿ��� ��.</returns>
    public int GatherResource(int workerCarryCapacity)
    {
        if (IsDepleted)
        {
            return 0; // �̹� ����
        }

        // �� ���� ä�� ƽ���� ���� �� �ִ� ��
        int amountToGatherThisTick = amountPerGatherTick;

        // ��忡 ���� �纸�� ���� ä���� �� ����
        amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, _currentAmount);

        /* ���̻� ������ ����.
        // �ϲ��� �� ���� ������ �� �ִ� �ִ� �� (maxCarryAmountPerTrip)��
        // �ϲ��� ���� �� �� �� �ִ� �� (workerCarryCapacity) �� �� ���� ������ ����
        if (maxCarryAmountPerTrip > 0) // maxCarryAmountPerTrip�� ������ ���
        {
            // �� ������ �ϲ��� �� �� "�湮"���� �� ������ �� �ִ� �ѷ��� ���� ��.
            // GatherResource�� "�� ƽ"�� ä���ϴ� ���� �ǹ��ϹǷ�,
            // �� �κ��� �ϲ� ������ �������� (���� ƽ�� ����) �����ϴ� ���� �� ������ �� ����.
            // ���⼭�� �� ƽ�� amountPerGatherTick��ŭ�� ä���ϰ�,
            // �ϲ��� ���� ƽ�� ���� maxCarryAmountPerTrip���� ä�쵵�� �ϴ� ���� �Ϲ���.
            // ����, workerCarryCapacity�� "�� ƽ����" ä���� ���� �����ϴ� �뵵�� ���.
            amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, workerCarryCapacity);
        }
        else
        {
            // maxCarryAmountPerTrip ������ ���ٸ�, �ϲ��� �� �� �ִ� ��ŭ�� ����
            amountToGatherThisTick = Mathf.Min(amountToGatherThisTick, workerCarryCapacity);
        }
        */


        if (amountToGatherThisTick <= 0) // ä���� ���� ������ (�ϲ��� �� á�ų�, ��尡 ���� ����ų�)
        {
            return 0;
        }

        int oldAmount = _currentAmount;
        _currentAmount -= amountToGatherThisTick;

        OnAmountChanged?.Invoke(this, _currentAmount, oldAmount);

        if (_currentAmount <= 0)
        {
            _currentAmount = 0; // ���� ����
            HandleDepletion();
        }

        // Debug.Log($"�ڿ� ��� {gameObject.name}: {amountToGatherThisTick} {resourceType} ä���. ���� ��: {_currentAmount}");
        return amountToGatherThisTick;
    }

    /// <summary>
    /// �ڿ��� ���Ǿ��� �� ȣ��Ǵ� ó���Դϴ�.
    /// </summary>
    private void HandleDepletion()
    {
        // Debug.Log($"�ڿ� ��� {gameObject.name} ({resourceType}) ����.");
        OnResourceDepleted?.Invoke(this);

        // �ð��� ���� (���� ����)
        // if (_originalVisual != null) _originalVisual.enabled = false;
        // if (depletedVisual != null) depletedVisual.SetActive(true);

        // �� �̻� ��ȣ�ۿ� �Ұ����ϵ��� ���� (��: Collider ��Ȱ��ȭ)
        // Collider col = GetComponent<Collider>();
        // if (col != null) col.enabled = false;
        // �Ǵ� isWalkable ���� �÷��׸� false�� ���� (HexGridSystem���� �����Ѵٸ�)
    }

    #endregion

    #region Public Utility (���� ����)
    public string GetResourceType()
    {
        return resourceType;
    }
    #endregion

    #region Editor Gizmos (���� ���� - ���� �� �ð�ȭ ��)
#if UNITY_EDITOR
    void OnDrawGizmosSelected() // �Ǵ� OnDrawGizmos
    {
        if (Application.isPlaying) // ���� �߿��� ���� �� ǥ��
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"{resourceType}\n{_currentAmount} / {initialAmount}");
        }
        else // ���� ������ �ʱ� �� ǥ��
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"{resourceType}\n{initialAmount}");
        }
    }
#endif
    #endregion
}