using UnityEngine;

public class DeterministicTimeManager : MonoBehaviour
{
    [Header("Tick Settings")]
    [Tooltip("�ʴ� �߻��ϴ� ���� ������Ʈ(ƽ)�� ���Դϴ�.")]
    public float ticksPerSecond = 20.0f; // ��: 1�ʿ� 20�� ƽ �߻�

    // --- ���� ���� �ð� ���� ---
    [Header("Current Game Time (Read-Only)")]
    [Tooltip("���� ���� �� ����� �� ƽ ���Դϴ�.")]
    [SerializeField] // �ν����Ϳ��� ���� ���� (���� �Ұ�)
    private ulong _currentTick = 0;
    public ulong CurrentTick => _currentTick; // �ܺο����� �б⸸ ����

    [Tooltip("���� ƽ���� ����� �� ���� �ð� (��)�Դϴ�.")]
    [SerializeField]
    private double _currentGameTimeInSeconds = 0.0;
    public double CurrentGameTimeInSeconds => _currentGameTimeInSeconds;

    // --- ���� ���� ---
    private float _tickInterval;      // �� ƽ ������ �ð� ���� (��)
    private float _accumulator = 0.0f; // ���� �ð�(Time.deltaTime)�� �����ϴ� ����

    // --- �ٸ� �Ŵ��� ���� ---
    [Header("References")]
    [Tooltip("�� ƽ���� ���� ������ ó���� �Ŵ����Դϴ�.")]
    public GameLogicManager gameLogicManager; // Inspector���� �Ҵ�

    // --- �̺�Ʈ (���� ���������� ������) ---
    public delegate void OnTickProcessedHandler(ulong tick, float tickInterval);
    public event OnTickProcessedHandler OnTickProcessed;

    #region Unity Lifecycle Methods



    void Awake()
    {
        if (ticksPerSecond <= 0)
        {
            Debug.LogError("TicksPerSecond�� 0���� Ŀ�� �մϴ�. �⺻�� 20���� �����մϴ�.");
            ticksPerSecond = 20.0f;
        }
        _tickInterval = 1.0f / ticksPerSecond;

        if (gameLogicManager == null)
        {
            Debug.LogError("GameLogicManager�� �Ҵ���� �ʾҽ��ϴ�! DeterministicTimeManager�� ����� �۵����� ���� �� �ֽ��ϴ�.");
            // �ʿ��ϴٸ� FindObjectOfType<GameLogicManager>() ������ ã�� �� ������, ����� �Ҵ��� �� ����.
        }
    }

    void Update()
    {
        // ���� ��� �ð�(������ �ð�)�� �����⿡ �߰�
        _accumulator += Time.deltaTime;

        // ������ �ð��� ƽ ���ݺ��� ũ�ų� ������ ƽ ó��
        // �� �����ӿ� ���� ƽ�� �߻��� �� �����Ƿ� while ���� ���
        while (_accumulator >= _tickInterval)
        {
            // 1. ���� ���� ó�� ��û
            if (gameLogicManager != null)
            {
                // GameLogicManager�� ���� ƽ ������ ƽ ����(�ʿ��) ����
                gameLogicManager.ProcessGameLogicForTick(_currentTick, _tickInterval);
            }

            // 2. ƽ ó�� �̺�Ʈ �߻� (�����ڰ� �ִٸ�)
            OnTickProcessed?.Invoke(_currentTick, _tickInterval);

            // 3. �����⿡�� ƽ ���ݸ�ŭ �ð� ����
            _accumulator -= _tickInterval;

            // 4. ���� ƽ �� ���� �ð� ������Ʈ
            _currentTick++;
            _currentGameTimeInSeconds += _tickInterval; // double�� ��Ȯ�� ����
        }
    }

    #endregion

    #region Public Utility Methods (���� ����)

    /// <summary>
    /// Ư�� ƽ ���� ���� ���� �ð�(��)���� ��ȯ�մϴ�.
    /// </summary>
    public double TicksToSeconds(ulong ticks)
    {
        return (double)ticks * _tickInterval;
    }

    /// <summary>
    /// ���� ���� �ð�(��)�� ���� ����� ƽ ���� ��ȯ�մϴ�.
    /// </summary>
    public ulong SecondsToTicks(double seconds)
    {
        return (ulong)Mathf.RoundToInt((float)(seconds / _tickInterval));
    }

    /// <summary>
    /// ���� �����ӿ��� ���� ƽ���� ���� �ð��� ������ ��ȯ�մϴ� (0.0 ~ 1.0).
    /// ������ ������ ���� �� �ֽ��ϴ�.
    /// </summary>
    public float GetInterpolationFactor()
    {
        // _tickInterval�� 0�� �Ǵ� �ش����� ��� ����
        if (_tickInterval <= 0.0f) return 0.0f;
        return _accumulator / _tickInterval;
    }

    #endregion

    #region Editor Gizmos (���� ����)
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // ���� ���� �ƴ� ���� _tickInterval ��� (�ν����� �� ���� �ݿ�)
        if (!Application.isPlaying && ticksPerSecond > 0)
        {
            _tickInterval = 1.0f / ticksPerSecond;
        }
    }
#endif
    #endregion
}