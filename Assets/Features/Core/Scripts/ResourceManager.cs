using UnityEngine;
using System.Collections.Generic;

// �ڿ��� ������ ������ �� �ִ� ������ (���� ����, ���ڿ� ID�� ����� ���� ����)
public enum ResourceType
{
    None,
    Mineral, // ���� �ڿ� 1
    Gas,     // ���� �ڿ� 2
    Supply,  // �α��� (�ڿ�ó�� ���� ����)
    // ... ��Ÿ �ʿ��� �ڿ� ���� �߰�
}

public class ResourceManager : MonoBehaviour
{
    [System.Serializable]
    public class PlayerResourceData
    {
        public ulong playerId;
        // Key: �ڿ� ���� (ResourceType enum �Ǵ� string), Value: �ش� �ڿ��� ���� ��
        public Dictionary<string, int> resources = new Dictionary<string, int>();
        // Key: �ڿ� ���� (ResourceType enum �Ǵ� string), Value: �ش� �ڿ��� �ִ� �ѵ� (��: �α���)
        public Dictionary<string, int> resourceCaps = new Dictionary<string, int>();

        public PlayerResourceData(ulong id)
        {
            playerId = id;
        }
    }

    // ��� �÷��̾��� �ڿ� �����͸� ���� (Key: �÷��̾� ID)
    private Dictionary<ulong, PlayerResourceData> _allPlayerResources = new Dictionary<ulong, PlayerResourceData>();

    // ���� ���� �� �� �÷��̾�� �־��� �ʱ� �ڿ� ���� (Inspector���� ���� �����ϵ���)
    [System.Serializable]
    public struct StartingResource
    {
        public string resourceName; // �Ǵ� ResourceType enum ���
        public int amount;
        public int cap; // �� �ڿ��� �ִ� �ѵ� (0�̸� ������ �Ǵ� �Ϲ� �ڿ�)
    }

    [System.Serializable]
    public struct PlayerStartingResources
    {
        public ulong playerId;
        public List<StartingResource> startingResources;
    }

    public List<PlayerStartingResources> allPlayersStartingResources; // Inspector���� �� �÷��̾��� �ʱ� �ڿ� ����

    // --- �̺�Ʈ (���� ���������� UI ������Ʈ � ����) ---
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
                if (res.cap > 0) // �ѵ��� ������ �ڿ��� (��: �α���)
                {
                    playerData.resourceCaps[res.resourceName] = res.cap;
                }
                // �ʱ� �ڿ� ������ ���� �̺�Ʈ �߻��� ������ (���� ���� �� �� ���� �ʿ�)
            }
            _allPlayerResources[playerSetup.playerId] = playerData;
            Debug.Log($"�÷��̾� {playerSetup.playerId} �ʱ� �ڿ� ���� �Ϸ�.");
        }
    }

    // Ư�� �÷��̾��� �����Ͱ� ���ٸ� ���� (��: ���� �� �� �÷��̾� ���� - ��Ƽ�÷��̾� ��)
    private PlayerResourceData GetOrAddPlayerData(ulong playerId)
    {
        if (!_allPlayerResources.TryGetValue(playerId, out PlayerResourceData playerData))
        {
            playerData = new PlayerResourceData(playerId);
            // �� �÷��̾�� �⺻ �ʱ� �ڿ��� �� ���� ����
            // ���⼭�� �� ���·� �����Ѵٰ� �����ϰ�, �ʿ�� InitializeAllPlayerResources�� ������ ���� �߰�
            _allPlayerResources[playerId] = playerData;
            Debug.LogWarning($"�÷��̾� {playerId}�� �ڿ� �����Ͱ� ���� ���� �����մϴ�. �ʱ� �ڿ��� ���� �� �ֽ��ϴ�.");
        }
        return playerData;
    }

    #endregion

    #region Core Resource Operations (��������)

    /// <summary>
    /// Ư�� �÷��̾��� Ư�� �ڿ� ���� ���� ��ȯ�մϴ�.
    /// �ڿ��� ������ 0�� ��ȯ�մϴ�.
    /// </summary>
    public int GetResourceAmount(ulong playerId, string resourceName)
    {
        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        if (playerData.resources.TryGetValue(resourceName, out int amount))
        {
            return amount;
        }
        return 0; // �ش� �ڿ��� ��ϵǾ� ���� ������ 0 ��ȯ
    }

    /// <summary>
    /// Ư�� �÷��̾��� Ư�� �ڿ� �ִ� �ѵ��� ��ȯ�մϴ�.
    /// �ѵ��� �������� �ʾ����� int.MaxValue �Ǵ� Ư�� ���� ���� ��ȯ�� �� �ֽ��ϴ�.
    /// </summary>
    public int GetResourceCap(ulong playerId, string resourceName)
    {
        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        if (playerData.resourceCaps.TryGetValue(resourceName, out int cap))
        {
            return cap;
        }
        return int.MaxValue; // �ѵ� ���� �ڿ��� �ſ� ū ������ ���� (�Ǵ� 0, -1 �� ��ӵ� ��)
    }


    /// <summary>
    /// Ư�� �÷��̾�� Ư�� �ڿ��� �߰��մϴ�. (���������̾�� ��)
    /// </summary>
    /// <returns>���������� �߰��Ǿ����� true, ����(��: �ѵ� �ʰ�)�ϸ� false.</returns>
    public bool AddResource(ulong playerId, string resourceName, int amountToAdd)
    {
        if (amountToAdd <= 0)
        {
            Debug.LogWarning($"ResourceManager: 0 ������ �ڿ�({resourceName}, {amountToAdd})�� �߰��Ϸ��� �õ��߽��ϴ�. (�÷��̾�: {playerId})");
            return false; // ���� �Ǵ� 0 �߰��� �����ϰų� ���� ó��
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int oldAmount = GetResourceAmount(playerId, resourceName); // �̺�Ʈ �߻��� ���� ���� �� ����
        int currentAmount = oldAmount;
        int cap = GetResourceCap(playerId, resourceName);

        int newAmount = currentAmount + amountToAdd;

        // �ѵ� üũ (�Ϲ� �ڿ��� ���� �ѵ� ����, �α��� ���� ��쿡�� üũ)
        if (newAmount > cap && cap != int.MaxValue) // cap�� int.MaxValue�� �ƴ϶�� ���� �ѵ��� �ִٴ� �ǹ�
        {
            newAmount = cap; // �ѵ������� ä��
            // Debug.Log($"ResourceManager: �ڿ�({resourceName}) �߰� �� �ѵ�({cap})�� �����߽��ϴ�. (�÷��̾�: {playerId})");
            // return false; // �ѵ� �ʰ��� �߰� ���з� ó���� ���� ����. ���⼭�� �ѵ������� ä��� ����.
        }

        if (newAmount == currentAmount && amountToAdd > 0)
        { // �ѵ� ������ ���� ���� ������ ���� ���
          // �ѵ��� ������, �̹� ������ ���̻� �ȿö󰥶��� �̺�Ʈ �߻� ���ϵ���.
          // �ٸ�, ���������δ� '�߰� �õ�'�� �����Ѱ����� �� �� ����.
          // return true;
        }


        playerData.resources[resourceName] = newAmount;

        if (newAmount != oldAmount)
        { // ���� �ڿ� ���� ����� ��쿡�� �̺�Ʈ �߻�
            OnResourceChanged?.Invoke(playerId, resourceName, newAmount, oldAmount);
            // Debug.Log($"ResourceManager: �÷��̾� {playerId}�� �ڿ� {resourceName}��(��) {oldAmount}���� {newAmount}�� ����� (+{amountToAdd})");
        }
        return true;
    }

    /// <summary>
    /// Ư�� �÷��̾�κ��� Ư�� �ڿ��� �Ҹ��մϴ�. (���������̾�� ��)
    /// </summary>
    /// <returns>�ڿ��� ����Ͽ� ���������� �Ҹ������� true, �����ϸ� false.</returns>
    public bool TryConsumeResource(ulong playerId, string resourceName, int amountToConsume)
    {
        if (amountToConsume <= 0)
        {
            // Debug.LogWarning($"ResourceManager: 0 ������ �ڿ�({resourceName}, {amountToConsume})�� �Ҹ��Ϸ��� �õ��߽��ϴ�. (�÷��̾�: {playerId})");
            return true; // 0 �Ҹ�� �׻� �������� ���� (�ƹ��͵� �� ��)
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int currentAmount = GetResourceAmount(playerId, resourceName);

        if (currentAmount >= amountToConsume)
        {
            int oldAmount = currentAmount;
            int newAmount = currentAmount - amountToConsume;
            playerData.resources[resourceName] = newAmount;

            OnResourceChanged?.Invoke(playerId, resourceName, newAmount, oldAmount);
            // Debug.Log($"ResourceManager: �÷��̾� {playerId}�� �ڿ� {resourceName}��(��) {oldAmount}���� {newAmount}�� ����� (-{amountToConsume})");
            return true;
        }
        else
        {
            // Debug.Log($"ResourceManager: �÷��̾� {playerId}�� �ڿ� {resourceName} ����. (��û: {amountToConsume}, ����: {currentAmount})");
            return false; // �ڿ� ����
        }
    }

    /// <summary>
    /// Ư�� �÷��̾ Ư�� ����� ������ �� �ִ��� Ȯ���մϴ�.
    /// </summary>
    /// <param name="costs">Key: �ڿ� �̸�, Value: �ʿ��� ��</param>
    public bool CanAfford(ulong playerId, Dictionary<string, int> costs)
    {
        if (costs == null || costs.Count == 0) return true; // ��� ����

        foreach (var costEntry in costs)
        {
            if (GetResourceAmount(playerId, costEntry.Key) < costEntry.Value)
            {
                return false; // �ϳ��� �����ϸ� ���� �Ұ�
            }
        }
        return true;
    }

    /// <summary>
    /// Ư�� �÷��̾�κ��� ���� �ڿ��� �� ���� �Ҹ��մϴ�. (CanAfford ���� Ȯ�� ����)
    /// </summary>
    /// <returns>��� �ڿ��� ���������� �Ҹ������� true, �ϳ��� �����ϸ� false (�ѹ� ���� - ����!).</returns>
    public bool TryConsumeResources(ulong playerId, Dictionary<string, int> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        // ���� ���� �������� ��ü������ Ȯ�� (���� ���������� ����)
        if (!CanAfford(playerId, costs))
        {
            // Debug.Log($"ResourceManager: �÷��̾� {playerId}�� ����� ������ �� �����ϴ�.");
            return false;
        }

        // ���� �Ҹ� (�� �κ��� �ѹ� ������ �����Ƿ�, CanAfford�� ���� ȣ���ϴ� ���� ����)
        foreach (var costEntry in costs)
        {
            // TryConsumeResource ���ο��� �̹� ������� Ȯ��������,
            // ���� �ڿ� �Ҹ� �� �ϳ� �����ϸ� �������� �ѹ����� ���� �����ؾ� ��.
            // ���⼭�� �� �ڿ��� ���������� �Ҹ� �õ�.
            if (!TryConsumeResource(playerId, costEntry.Key, costEntry.Value))
            {
                // �� ���, �̹� �Ϻ� �ڿ��� �Ҹ�� ���·� false�� ��ȯ�� �� ����.
                // ���� �� �Լ��� ����ϱ� ���� CanAfford�� �ݵ�� ȣ���ϰų�,
                // �� �Լ� ������ Ʈ�����ó�� ��� �ڿ� Ȯ�� �� �� ���� ������Ʈ�ϴ� ���� �ʿ�.
                // ������ �����ϰ� ���� TryConsumeResource ����� ����.
                Debug.LogError($"ResourceManager: �÷��̾� {playerId}�� �ڿ� {costEntry.Key} �Ҹ� �� ���� �߻�. (���: {costEntry.Value}) - �̹� �Ϻ� �ڿ��� �Ҹ�Ǿ��� �� ����!");
                return false; // �ϳ��� �����ϸ� ��ü ���з� ���� (������ �ѹ��� �ȵ�)
            }
        }
        return true;
    }

    /// <summary>
    /// Ư�� �÷��̾��� Ư�� �ڿ� �ִ� �ѵ��� �����մϴ�. (��: ���ް� �Ǽ� �� �α��� �ѵ� ����)
    /// </summary>
    public void UpdateResourceCap(ulong playerId, string resourceName, int newCapAmount)
    {
        if (newCapAmount < 0)
        {
            Debug.LogWarning($"ResourceManager: �ڿ�({resourceName})�� �ִ� �ѵ��� ������ �����Ϸ��� �õ��߽��ϴ�. (�÷��̾�: {playerId})");
            return;
        }

        PlayerResourceData playerData = GetOrAddPlayerData(playerId);
        int oldCap = GetResourceCap(playerId, resourceName);

        playerData.resourceCaps[resourceName] = newCapAmount;

        // ���� �ڿ����� �� �ѵ��� �ʰ��ϸ� ���� (������, ���� ��Ģ�� ����)
        // if (playerData.resources.TryGetValue(resourceName, out int currentAmount) && currentAmount > newCapAmount)
        // {
        //     playerData.resources[resourceName] = newCapAmount;
        //     OnResourceChanged?.Invoke(playerId, resourceName, newCapAmount, currentAmount);
        // }

        if (newCapAmount != oldCap)
        {
            OnResourceCapChanged?.Invoke(playerId, resourceName, newCapAmount, oldCap);
            // Debug.Log($"ResourceManager: �÷��̾� {playerId}�� �ڿ� {resourceName} �ִ� �ѵ��� {oldCap}���� {newCapAmount}�� �����");
        }
    }

    #endregion

    #region Utility for GameLogicManager (����)
    // GameLogicManager���� ��� ������ ���� ������ �� �ֵ��� ���� �Լ� ���� ����
    public static Dictionary<string, int> CreateCostDictionary(string res1Name, int res1Cost, string res2Name = null, int res2Cost = 0)
    {
        var costs = new Dictionary<string, int>();
        if (!string.IsNullOrEmpty(res1Name) && res1Cost > 0) costs[res1Name] = res1Cost;
        if (!string.IsNullOrEmpty(res2Name) && res2Cost > 0) costs[res2Name] = res2Cost;
        return costs;
    }
    #endregion
}