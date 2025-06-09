using UnityEngine;

public class DeterministicTimeManager : MonoBehaviour
{
    [Header("Tick Settings")]
    [Tooltip("초당 발생하는 논리적 업데이트(틱)의 수입니다.")]
    public float ticksPerSecond = 20.0f; // 예: 1초에 20번 틱 발생

    // --- 현재 게임 시간 정보 ---
    [Header("Current Game Time (Read-Only)")]
    [Tooltip("게임 시작 후 경과한 총 틱 수입니다.")]
    [SerializeField] // 인스펙터에서 보기 위해 (수정 불가)
    private ulong _currentTick = 0;
    public ulong CurrentTick => _currentTick; // 외부에서는 읽기만 가능

    [Tooltip("현재 틱까지 경과한 총 게임 시간 (초)입니다.")]
    [SerializeField]
    private double _currentGameTimeInSeconds = 0.0;
    public double CurrentGameTimeInSeconds => _currentGameTimeInSeconds;

    // --- 내부 변수 ---
    private float _tickInterval;      // 각 틱 사이의 시간 간격 (초)
    private float _accumulator = 0.0f; // 실제 시간(Time.deltaTime)을 누적하는 변수

    // --- 다른 매니저 참조 ---
    [Header("References")]
    [Tooltip("각 틱마다 게임 로직을 처리할 매니저입니다.")]
    public GameLogicManager gameLogicManager; // Inspector에서 할당

    // --- 이벤트 (선택 사항이지만 유용함) ---
    public delegate void OnTickProcessedHandler(ulong tick, float tickInterval);
    public event OnTickProcessedHandler OnTickProcessed;

    #region Unity Lifecycle Methods



    void Awake()
    {
        if (ticksPerSecond <= 0)
        {
            Debug.LogError("TicksPerSecond는 0보다 커야 합니다. 기본값 20으로 설정합니다.");
            ticksPerSecond = 20.0f;
        }
        _tickInterval = 1.0f / ticksPerSecond;

        if (gameLogicManager == null)
        {
            Debug.LogError("GameLogicManager가 할당되지 않았습니다! DeterministicTimeManager가 제대로 작동하지 않을 수 있습니다.");
            // 필요하다면 FindObjectOfType<GameLogicManager>() 등으로 찾을 수 있지만, 명시적 할당이 더 좋음.
        }
    }

    void Update()
    {
        // 실제 경과 시간(프레임 시간)을 누적기에 추가
        _accumulator += Time.deltaTime;

        // 누적된 시간이 틱 간격보다 크거나 같으면 틱 처리
        // 한 프레임에 여러 틱이 발생할 수 있으므로 while 루프 사용
        while (_accumulator >= _tickInterval)
        {
            // 1. 게임 로직 처리 요청
            if (gameLogicManager != null)
            {
                // GameLogicManager에 현재 틱 정보와 틱 간격(필요시) 전달
                gameLogicManager.ProcessGameLogicForTick(_currentTick, _tickInterval);
            }

            // 2. 틱 처리 이벤트 발생 (구독자가 있다면)
            OnTickProcessed?.Invoke(_currentTick, _tickInterval);

            // 3. 누적기에서 틱 간격만큼 시간 차감
            _accumulator -= _tickInterval;

            // 4. 현재 틱 및 게임 시간 업데이트
            _currentTick++;
            _currentGameTimeInSeconds += _tickInterval; // double로 정확도 유지
        }
    }

    #endregion

    #region Public Utility Methods (선택 사항)

    /// <summary>
    /// 특정 틱 수를 실제 게임 시간(초)으로 변환합니다.
    /// </summary>
    public double TicksToSeconds(ulong ticks)
    {
        return (double)ticks * _tickInterval;
    }

    /// <summary>
    /// 실제 게임 시간(초)을 가장 가까운 틱 수로 변환합니다.
    /// </summary>
    public ulong SecondsToTicks(double seconds)
    {
        return (ulong)Mathf.RoundToInt((float)(seconds / _tickInterval));
    }

    /// <summary>
    /// 현재 프레임에서 다음 틱까지 남은 시간의 비율을 반환합니다 (0.0 ~ 1.0).
    /// 렌더링 보간에 사용될 수 있습니다.
    /// </summary>
    public float GetInterpolationFactor()
    {
        // _tickInterval이 0이 되는 극단적인 경우 방지
        if (_tickInterval <= 0.0f) return 0.0f;
        return _accumulator / _tickInterval;
    }

    #endregion

    #region Editor Gizmos (선택 사항)
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // 실행 중이 아닐 때도 _tickInterval 계산 (인스펙터 값 변경 반영)
        if (!Application.isPlaying && ticksPerSecond > 0)
        {
            _tickInterval = 1.0f / ticksPerSecond;
        }
    }
#endif
    #endregion
}