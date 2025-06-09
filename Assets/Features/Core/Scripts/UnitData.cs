using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ResourceCost
{
    public string resourceName;
    public int amount;
}

[CreateAssetMenu(fileName = "NewUnitData_Int", menuName = "RTS/Unit Data (Int)")]

public class UnitData : ScriptableObject // ���� UnitData�� �̸��� ���ٸ� �ϳ��� �����ϰų�, namespace ���
{
    public string unitName = "DefaultUnit";
    public GameObject unitPrefab; // ������ �ð��� ǥ���� ���� ������
    public Vector3Int sizeInTiles = Vector3Int.one; // �����ϴ� Ÿ�� ũ�� (���������� ���� ��� ����)

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Movement")]
    // 1 �׸��� Ÿ���� �̵��ϴ� �� �ʿ��� �� �̵� ����Ʈ. ��: 1000
    public int pointsPerTile = 1000;
    // 1 ƽ�� ��� �̵� ����Ʈ. ��: 200 (5ƽ�� 1Ÿ�� �̵�)
    public int movePointsPerTick = 200;


    [Header("Attack")]
    public int attackDamage = 10;
    public int attackRangeInTiles = 1; // 0: ����, 1: 1ĭ ������ �� ���� ����
    public int attackCooldownInTicks = 20; // ��: 20ƽ = 1�� (���� 1�ʿ� 20ƽ�̶��)
    public int defense = 0;

    [Header("Vision")]
    public int visionRangeInTiles = 5;


    public List<ResourceCost> creationCost; // ���� ��� (���� ����, �ǹ� �Ǽ� ����)
    public int creationTimeInTicks = 100;  // ���� �ð�

    // ���� Ÿ�� �ĺ��� ���� ����
    public enum UnitType { Generic, Worker, Combat_Melee, Combat_Ranged, Building, Combat_Magic /* ... */ }
    public UnitType role = UnitType.Generic;


}