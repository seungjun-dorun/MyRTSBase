using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWorkerUnitData", menuName = "RTS/Unit Data/Worker Unit")]
public class WorkerUnitData : UnitData // UnitData 상속
{
    [Header("Worker Specific Stats")]
    public int maxCarryCapacity = 50;         // 최대 자원 운반량
    public int ticksPerGatherAction = 20;     // 자원 채취 한 번에 걸리는 틱
    public float buildSpeedMultiplier = 1.0f; // 건설 속도 배율 (선택 사항)


    // 어떤 자원을 채취할 수 있는지 (선택 사항, 여러 종류 자원 시)
    // public List<string> gatherableResourceTypes;

    // 어떤 건물을 지을 수 있는지
    public List<string> buildableBuildingDataIDs;

    // 초기화 시 역할 자동 설정
    private void OnEnable()
    {
        role = UnitType.Worker; // 이 데이터는 항상 Worker 역할
    }
}