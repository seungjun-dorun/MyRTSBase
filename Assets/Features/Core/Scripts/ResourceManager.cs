using UnityEngine;
using System.Collections.Generic;

// 자원의 종류를 정의할 수 있는 열거형 (선택 사항, 문자열 ID를 사용할 수도 있음)
public enum ResourceType
{
    None,
    Mineral, // 예시 자원 1
    Gas,     // 예시 자원 2
    Supply,  // 인구수 (자원처럼 관리 가능)
    // ... 기타 필요한 자원 종류 추가
}

public class ResourceManager : MonoBehaviour
{
    [System.Serializable]
    public class PlayerResourceData
    {
        public ulong playerId;
        // Key: 자원 종류 (ResourceType enum 또는 string), Value: 해당 자원의 현재 양
        public Dictionary<string, int> resources = new Dictionary<string, int>();
        // Key: 자원 종류 (ResourceType enum 또는 string), Value: 해당 자원의 최대 한도 (예: 인구수)
        public Dictionary<string, int> resourceCaps = new Dictionary<string, int>();

        public PlayerResourceData(ulong id)
        {
            playerId = id;
        }
    }

    // 모든 플레이어의 자원 데이터를 저장 (Key: 플레이어 ID)
    private Dictionary<ulong, PlayerResourceData> _allPlayerResources = new Dictionary<ulong, PlayerResourceData>();

    // 게임 시작 시 각 플레이어에게 주어질 초기 자원 설정 (Inspector에서 설정 가능하도록)
    [System.Serializable]
    public struct StartingResource
    {
        public string resourceName; // 또는 ResourceType enum 사용
        public int amount;
        public int cap; // 이 자원의 최대 한도 (0이면 무제한 또는 일반 자원)
    }

    [System.Serializable]
    public struct PlayerStartingResources
    {
        public ulong playerId;
        public List<StartingResource> startingResources;
    }

    public List<PlayerStartingResources> allPlayersStartingResources; // Inspector에서 각 플레이어의 초기 자원 설정

    // --- 이벤트 (선택 사항이지만 UI 업데이트 등에 유용) ---
    public delegate void OnResourceChangedHandler(ulong playerId, string resourceName, int newAmount, int oldAmount);
    public event OnResourceChangedHandler OnResourceChanged;

    public delegate void OnResourceCapChangedHandler(ulong playerId, string resourceName, int newCap, int oldCap);
    public event OnResourceCapChangedHandler OnResourceCapChanged;


    #region Initialization

    void Awake()
    {
        InitializeAllPlayerResources();
    }

    void InitializeAllPlayerResources()
    {
        _allPlayerResources.Clear();
        foreach (PlayerStartingResources playerSetup in allPlayersStartingResources)
        {
            PlayerResourceData playerData = new PlayerResourceData(playerSetup.playerId);
            foreach (StartingResource res in playerSetup.startingResources)
            {
                playerData.resources[res.resourceName] = res.amount;
                if (res.cap > 0) // 한도가 설정된 자원만 (예: 인구수)
                {
                    playerData.resourceCaps[res.resourceName] = res.cap;
                }
                // 초기 자원 설정에 대한 이벤트 발생은 선택적 (게임 시작 시 한 번만 필요)
            }
            _allPlayerResources[playerSetup.playerId] = playerData;
            Debug.Log($"플레이어 {playerSetup.playerId} 초기 자원 설정 완료.");
        }
    }

    // 특정 플레이어의 데이터가 없다면 생성 (예: 게임 중 새 플레이어 참여 - 멀티플레이어 시)
    private PlayerResourceData GetOrAddPlayerData(ulong playerId)
    {
        if (!_allPlayerResources.TryGetValue(playerId, out PlayerResourceData playerData))
        {
            playerData = new PlayerResourceData(playerId);
            // 새 플레이어에게 기본 초기 자원을 줄 수도 있음
            // 여기서는 빈 상태로 시작한다고 가정하고, 필요시 InitializeAllPlayerResources와 유사한 로직 추가
            _allPlayerResources[playerId] = playerData;
            Debug.LogWarning($"플레이어 {playerId}의 자원 데이터가 없어 새로 생성합니다. 초기 자원이 없을 수 있습니다.");
        }
        return playerData;
    }

    #endregion

    #region Core Resource Operations (결정론적)

    /// <summary>
    /// 특정 플레이어의 특정 자원 현재 양을 반환합니다.
    /// 자원이 없으면 0을 반환합니다.
    /// </summary>
    public int GetResourceAmount(ulong playerId, string resourceName)
    {
        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        if (playerData.resources.TryGetValue(resourceName, out int amount))
        {
            return amount;
        }
        return 0; // 해당 자원이 등록되어 있지 않으면 0 반환
    }

    /// <summary>
    /// 특정 플레이어의 특정 자원 최대 한도를 반환합니다.
    /// 한도가 설정되지 않았으면 int.MaxValue 또는 특정 음수 값을 반환할 수 있습니다.
    /// </summary>
    public int GetResourceCap(ulong playerId, string resourceName)
    {
        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        if (playerData.resourceCaps.TryGetValue(resourceName, out int cap))
        {
            return cap;
        }
        return int.MaxValue; // 한도 없는 자원은 매우 큰 값으로 간주 (또는 0, -1 등 약속된 값)
    }


    /// <summary>
    /// 특정 플레이어에게 특정 자원을 추가합니다. (결정론적이어야 함)
    /// </summary>
    /// <returns>성공적으로 추가되었으면 true, 실패(예: 한도 초과)하면 false.</returns>
    public bool AddResource(ulong playerId, string resourceName, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            Debug.LogWarning($"ResourceManager: 0 이하의 자원({resourceName}, {amountToAdd})을 추가하려고 시도했습니다. (플레이어: {playerId})");
            return false; // 음수 또는 0 추가는 무시하거나 오류 처리
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int oldAmount = GetResourceAmount(playerId, resourceName); // 이벤트 발생을 위해 이전 값 저장
        int currentAmount = oldAmount;
        int cap = GetResourceCap(playerId, resourceName);

        int newAmount = currentAmount + amountToAdd;

        // 한도 체크 (일반 자원은 보통 한도 없음, 인구수 같은 경우에만 체크)
        if (newAmount > cap && cap != int.MaxValue) // cap이 int.MaxValue가 아니라는 것은 한도가 있다는 의미
        {
            newAmount = cap; // 한도까지만 채움
            // Debug.Log($"ResourceManager: 자원({resourceName}) 추가 시 한도({cap})에 도달했습니다. (플레이어: {playerId})");
            // return false; // 한도 초과로 추가 실패로 처리할 수도 있음. 여기서는 한도까지만 채우고 성공.
        }

        if (newAmount == currentAmount && amountToAdd > 0)
        { // 한도 때문에 실제 양이 변하지 않은 경우
          // 한도는 있지만, 이미 꽉차서 더이상 안올라갈때는 이벤트 발생 안하도록.
          // 다만, 로직상으로는 '추가 시도'는 성공한것으로 볼 수 있음.
          // return true;
        }


        playerData.resources[resourceName] = newAmount;

        if (newAmount != oldAmount)
        { // 실제 자원 양이 변경된 경우에만 이벤트 발생
            OnResourceChanged?.Invoke(playerId, resourceName, newAmount, oldAmount);
            // Debug.Log($"ResourceManager: 플레이어 {playerId}의 자원 {resourceName}이(가) {oldAmount}에서 {newAmount}로 변경됨 (+{amountToAdd})");
        }
        return true;
    }

    /// <summary>
    /// 특정 플레이어로부터 특정 자원을 소모합니다. (결정론적이어야 함)
    /// </summary>
    /// <returns>자원이 충분하여 성공적으로 소모했으면 true, 부족하면 false.</returns>
    public bool TryConsumeResource(ulong playerId, string resourceName, int amountToConsume)
    {
        if (amountToConsume <= 0)
        {
            // Debug.LogWarning($"ResourceManager: 0 이하의 자원({resourceName}, {amountToConsume})을 소모하려고 시도했습니다. (플레이어: {playerId})");
            return true; // 0 소모는 항상 성공으로 간주 (아무것도 안 함)
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int currentAmount = GetResourceAmount(playerId, resourceName);

        if (currentAmount >= amountToConsume)
        {
            int oldAmount = currentAmount;
            int newAmount = currentAmount - amountToConsume;
            playerData.resources[resourceName] = newAmount;

            OnResourceChanged?.Invoke(playerId, resourceName, newAmount, oldAmount);
            // Debug.Log($"ResourceManager: 플레이어 {playerId}의 자원 {resourceName}이(가) {oldAmount}에서 {newAmount}로 변경됨 (-{amountToConsume})");
            return true;
        }
        else
        {
            // Debug.Log($"ResourceManager: 플레이어 {playerId}의 자원 {resourceName} 부족. (요청: {amountToConsume}, 현재: {currentAmount})");
            return false; // 자원 부족
        }
    }

    /// <summary>
    /// 특정 플레이어가 특정 비용을 지불할 수 있는지 확인합니다.
    /// </summary>
    /// <param name="costs">Key: 자원 이름, Value: 필요한 양</param>
    public bool CanAfford(ulong playerId, Dictionary<string, int> costs)
    {
        if (costs == null || costs.Count == 0) return true; // 비용 없음

        foreach (var costEntry in costs)
        {
            if (GetResourceAmount(playerId, costEntry.Key) < costEntry.Value)
            {
                return false; // 하나라도 부족하면 지불 불가
            }
        }
        return true;
    }

    /// <summary>
    /// 특정 플레이어로부터 여러 자원을 한 번에 소모합니다. (CanAfford 먼저 확인 권장)
    /// </summary>
    /// <returns>모든 자원을 성공적으로 소모했으면 true, 하나라도 부족하면 false (롤백 없음 - 주의!).</returns>
    public bool TryConsumeResources(ulong playerId, Dictionary<string, int> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        // 먼저 지불 가능한지 전체적으로 확인 (선택 사항이지만 권장)
        if (!CanAfford(playerId, costs))
        {
            // Debug.Log($"ResourceManager: 플레이어 {playerId}가 비용을 지불할 수 없습니다.");
            return false;
        }

        // 실제 소모 (이 부분은 롤백 로직이 없으므로, CanAfford를 먼저 호출하는 것이 안전)
        foreach (var costEntry in costs)
        {
            // TryConsumeResource 내부에서 이미 충분한지 확인하지만,
            // 여러 자원 소모 시 하나 실패하면 나머지도 롤백할지 등을 결정해야 함.
            // 여기서는 각 자원을 개별적으로 소모 시도.
            if (!TryConsumeResource(playerId, costEntry.Key, costEntry.Value))
            {
                // 이 경우, 이미 일부 자원이 소모된 상태로 false가 반환될 수 있음.
                // 따라서 이 함수를 사용하기 전에 CanAfford를 반드시 호출하거나,
                // 이 함수 내에서 트랜잭션처럼 모든 자원 확인 후 한 번에 업데이트하는 로직 필요.
                // 지금은 간단하게 개별 TryConsumeResource 결과를 따름.
                Debug.LogError($"ResourceManager: 플레이어 {playerId}의 자원 {costEntry.Key} 소모 중 오류 발생. (비용: {costEntry.Value}) - 이미 일부 자원이 소모되었을 수 있음!");
                return false; // 하나라도 실패하면 전체 실패로 간주 (하지만 롤백은 안됨)
            }
        }
        return true;
    }

    /// <summary>
    /// 특정 플레이어의 특정 자원 최대 한도를 변경합니다. (예: 보급고 건설 시 인구수 한도 증가)
    /// </summary>
    public void UpdateResourceCap(ulong playerId, string resourceName, int newCapAmount)
    {
        if (newCapAmount < 0)
        {
            Debug.LogWarning($"ResourceManager: 자원({resourceName})의 최대 한도를 음수로 설정하려고 시도했습니다. (플레이어: {playerId})");
            return;
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int oldCap = GetResourceCap(playerId, resourceName);

        playerData.resourceCaps[resourceName] = newCapAmount;

        // 현재 자원량이 새 한도를 초과하면 조정 (선택적, 게임 규칙에 따라)
        // if (playerData.resources.TryGetValue(resourceName, out int currentAmount) && currentAmount > newCapAmount)
        // {
        //     playerData.resources[resourceName] = newCapAmount;
        //     OnResourceChanged?.Invoke(playerId, resourceName, newCapAmount, currentAmount);
        // }

        if (newCapAmount != oldCap)
        {
            OnResourceCapChanged?.Invoke(playerId, resourceName, newCapAmount, oldCap);
            // Debug.Log($"ResourceManager: 플레이어 {playerId}의 자원 {resourceName} 최대 한도가 {oldCap}에서 {newCapAmount}로 변경됨");
        }
    }

    #endregion

    #region Utility for GameLogicManager (예시)
    // GameLogicManager에서 비용 정보를 쉽게 구성할 수 있도록 헬퍼 함수 제공 가능
    public static Dictionary<string, int> CreateCostDictionary(string res1Name, int res1Cost, string res2Name = null, int res2Cost = 0)
    {
        var costs = new Dictionary<string, int>();
        if (!string.IsNullOrEmpty(res1Name) && res1Cost > 0) costs[res1Name] = res1Cost;
        if (!string.IsNullOrEmpty(res2Name) && res2Cost > 0) costs[res2Name] = res2Cost;
        return costs;
    }
    #endregion
}