using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ResourceCost
{
    public string resourceName;
    public int amount;
}

[CreateAssetMenu(fileName = "NewUnitData_Int", menuName = "RTS/Unit Data (Int)")]

public class UnitData : ScriptableObject // 기존 UnitData와 이름이 같다면 하나를 변경하거나, namespace 사용
{
    public string unitName = "DefaultUnit";
    public GameObject unitPrefab; // 유닛의 시각적 표현을 위한 프리팹
    public Vector3Int sizeInTiles = Vector3Int.one; // 차지하는 타일 크기 (육각에서는 정의 방식 주의)

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Movement")]
    // 1 그리드 타일을 이동하는 데 필요한 총 이동 포인트. 예: 1000
    public int pointsPerTile = 1000;
    // 1 틱당 얻는 이동 포인트. 예: 200 (5틱에 1타일 이동)
    public int movePointsPerTick = 200;


    [Header("Attack")]
    public int attackDamage = 10;
    public int attackRangeInTiles = 1; // 0: 근접, 1: 1칸 떨어진 적 공격 가능
    public int attackCooldownInTicks = 20; // 예: 20틱 = 1초 (만약 1초에 20틱이라면)
    public int defense = 0;

    [Header("Vision")]
    public int visionRangeInTiles = 5;


    public List<ResourceCost> creationCost; // 생성 비용 (유닛 생산, 건물 건설 공통)
    public int creationTimeInTicks = 100;  // 생성 시간

    // 유닛 타입 식별을 위한 정보
    public enum UnitType { Generic, Worker, Combat_Melee, Combat_Ranged, Building, Combat_Magic /* ... */ }
    public UnitType role = UnitType.Generic;


}