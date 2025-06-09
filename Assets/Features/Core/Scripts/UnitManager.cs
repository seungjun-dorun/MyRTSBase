using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Linq ����� ���� �߰� (activeUnits ���� ��)

public class UnitManager : MonoBehaviour
{
    public HexGridSystem hexGridSystemRef; // Inspector �Ǵ� Start���� �Ҵ�
    public List<UnitData> availableUnitTypes; // Inspector���� ���� ������ ��� �Ҵ�

    private Dictionary<int, SimulatedObject> _allUnits = new Dictionary<int, SimulatedObject>();
    // Ȱ�� ���� ����Ʈ�� ���� Unit�� CurrentState�� Idle�� �ƴ� ���� ���͸� ����
    // private List<SimulatedObject> _activeUnits = new List<SimulatedObject>(); // ���� ���� ��� ���͸�

    private int _nextAvailableInstanceID = 1;

    void Start()
    {
        if (hexGridSystemRef == null) hexGridSystemRef = FindFirstObjectByType<HexGridSystem>();

        if (hexGridSystemRef == null)
        {
            Debug.LogError("UnitManager: HexGridSystem ������ �����ϴ�!");
            return;
        }

        // �׽�Ʈ�� ���� ���� (��: 0,0,0 ť�� ��ǥ��)
        if (availableUnitTypes != null && availableUnitTypes.Count > 0 && availableUnitTypes[0] != null)
        {
            Vector3Int spawnCoord = Vector3Int.zero; // �� �߾� ��ó �Ǵ� ��ȿ�� ��ǥ
            if (hexGridSystemRef.IsValidHex(spawnCoord)) // ���� ��ġ�� ��ȿ���� Ȯ��
            {
                for (int i = 0; i < 3; i++)
                {

                    CreateUnit(availableUnitTypes[0].unitName, spawnCoord, 0); // �÷��̾� ID 0���� ����
                }
            }
            else
            {
                Debug.LogWarning($"�׽�Ʈ ���� ���� ����: {spawnCoord}�� ��ȿ�� Ÿ���� �ƴմϴ�.");
            }



            // �� ��° ���� ���� (�ٸ� ��ġ, �ٸ� �÷��̾� ID�� ���� - AI �׽�Ʈ ��)
            Vector3Int spawnCoord2 = new Vector3Int(1, 0, -1); // (0,0,0)���� �� ĭ ��
            if (hexGridSystemRef.IsValidHex(spawnCoord2))
            {

                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // �÷��̾� ID 1�� ����
                spawnCoord2.x += 1; // �� ĭ �� ������ �̵� (��: (2,0,-2))
                spawnCoord2.z += -1;
                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // �÷��̾� ID 1�� ����
                spawnCoord2.x += 1; // �� ĭ �� ������ �̵� (��: (2,0,-2))
                spawnCoord2.z += -1;
                CreateUnit(availableUnitTypes[1].unitName, spawnCoord2, 1); // �÷��̾� ID 1�� ����

            }
            else
            {
                Debug.LogWarning($"�׽�Ʈ ���� ���� ����: {spawnCoord2}�� ��ȿ�� Ÿ���� �ƴմϴ�.");
            }
        }
        else
        {
            Debug.LogWarning("UnitManager: �׽�Ʈ�� ���� �����Ͱ� Available Unit Types�� �������� �ʾҽ��ϴ�.");
        }





    }

    public SimulatedObject CreateUnit(string unitTypeName, Vector3Int spawnCubeCoords, ulong ownerId)
    {
        UnitData unitDataToCreate = availableUnitTypes.Find(ud => ud.unitName == unitTypeName);
        if (unitDataToCreate == null)
        {
            Debug.LogError($"UnitData '{unitTypeName}'��(��) ã�� �� �����ϴ�.");
            return null;
        }

        if (unitDataToCreate.unitPrefab == null)
        {
            Debug.LogError($"UnitData '{unitTypeName}'�� �������� �Ҵ���� �ʾҽ��ϴ�.");
            return null;
        }

        // HexGridSystem���� ���� ���� ��ġ ��������
        Vector3 spawnWorldPos = hexGridSystemRef.CubeToWorld(spawnCubeCoords);

        GameObject unitGO = Instantiate(unitDataToCreate.unitPrefab, spawnWorldPos, Quaternion.identity);
        SimulatedObject unitSim = unitGO.GetComponent<SimulatedObject>();

        if (unitSim == null)
        {
            Debug.LogError($"���� ������ '{unitDataToCreate.unitPrefab.name}'�� SimulatedObject ������Ʈ�� �����ϴ�.");
            Destroy(unitGO);
            return null;
        }

        int newId = _nextAvailableInstanceID++;
        unitSim.Initialize(newId, ownerId, unitDataToCreate, hexGridSystemRef, this, spawnCubeCoords);

        _allUnits.Add(newId, unitSim);
        Debug.Log($"���� ����: {unitSim.InstanceID} ({unitDataToCreate.unitName}), ��ġ: {spawnCubeCoords}, ������: {ownerId}");
        return unitSim;
    }

    public void DestroyUnit(int instanceId)
    {
        if (_allUnits.TryGetValue(instanceId, out SimulatedObject unitToDestroy))
        {
            _allUnits.Remove(instanceId);
            // _activeUnits.Remove(unitToDestroy); // ���� _activeUnits�� ���� �����ߴٸ� ���⼭�� ����
            Destroy(unitToDestroy.gameObject);
            Debug.Log($"���� �ı�: {instanceId}");
        }
    }

    public SimulatedObject GetUnitById(int instanceId)
    {
        _allUnits.TryGetValue(instanceId, out SimulatedObject unit);
        return unit;
    }

    // ƽ ��� ������Ʈ ���� (GameLogicManager�� ���� ȣ���)
    public void UpdateUnitsForTick(float tickInterval)
    {
        // 1. ��� ���� �� Ȱ�� ���ָ� ������Ʈ (��: Idle�� �ƴϰų�, Ư���� ������ �ʿ��� ����)
        //    ���⼭�� ������ ��� ������ ��ȸ�ϸ� Dead�� �ƴ� ���ָ� ������Ʈ.
        //    �� ����ȭ�Ϸ���, ���� ���� �� ������ active list�� ����.
        List<SimulatedObject> unitsToUpdate = _allUnits.Values.Where(u => u.CurrentActionState != SimulatedObject.UnitActionState.Dead).ToList();

        foreach (SimulatedObject unit in unitsToUpdate)
        {
            unit.SimulateStep(tickInterval);
        }

        // 2. ����� ���� ó�� (SimulateStep���� Dead ���°� �� ���ֵ�)
        List<int> deadUnitIds = new List<int>();
        foreach (var pair in _allUnits)
        {
            if (pair.Value.CurrentActionState == SimulatedObject.UnitActionState.Dead && pair.Value.gameObject.activeSelf) // activeSelf�� �ӽ� ����
            {
                // TODO: ��� �ִϸ��̼�/ȿ�� �� �ı� ����. ���⼭�� ��� �ı� �غ�.
                pair.Value.gameObject.SetActive(false); // �ð������θ� ���� (���� �����ʹ� ��������)
                                                        // DestroyUnit(pair.Key); // �̷��� �ϸ� �ݺ� �� �÷��� ���� ���� �߻� ����
                deadUnitIds.Add(pair.Key);
            }
        }
        // ���� �ı��� ���� ���Ŀ�
        foreach (int id in deadUnitIds)
        {
            // DestroyUnit(id); // ���� ���� �󿡼� �ٷ� �ı�����, �ƴϸ� Ǯ������ �� ����
            // ���⼭�� �����̳� ��ü ������Ʈ�� ��ȯ�ϴ� ������ �� �� ����.
            // ������ SimulateStep���� Dead�� �Ǹ� �� �̻� ������Ʈ �� �ϵ��ϸ� �Ǿ� ����.
            // UnitManager�� ��������� DestroyUnit�� ȣ���Ͽ� _allUnits���� �����ؾ� ��.
        }
    }

    public SimulatedObject[] GetPlayerUnits(ulong localPlayerId)
    {
        return _allUnits.Values.Where(u => u.OwnerPlayerId == localPlayerId && u.CurrentActionState != SimulatedObject.UnitActionState.Dead).ToArray();
    }
    // ��� ����ִ� ���� ����� ��ȯ (SimulatedObject���� ��� �����ϵ���)
    public IEnumerable<SimulatedObject> GetAllAliveUnits()
    {
        return _allUnits.Values.Where(u => u.IsAlive());
    }

    // Ư�� �÷��̾��� ��� �� ���� ����� ��ȯ
    public IEnumerable<SimulatedObject> GetEnemyUnits(ulong currentPlayerId)
    {
        return _allUnits.Values.Where(u => u.IsAlive() && u.OwnerPlayerId != currentPlayerId);
    }

}