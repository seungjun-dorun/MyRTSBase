using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWorkerUnitData", menuName = "RTS/Unit Data/Worker Unit")]
public class WorkerUnitData : UnitData // UnitData ���
{
    [Header("Worker Specific Stats")]
    public int maxCarryCapacity = 50;         // �ִ� �ڿ� ��ݷ�
    public int ticksPerGatherAction = 20;     // �ڿ� ä�� �� ���� �ɸ��� ƽ
    public float buildSpeedMultiplier = 1.0f; // �Ǽ� �ӵ� ���� (���� ����)


    // � �ڿ��� ä���� �� �ִ��� (���� ����, ���� ���� �ڿ� ��)
    // public List<string> gatherableResourceTypes;

    // � �ǹ��� ���� �� �ִ���
    public List<string> buildableBuildingDataIDs;

    // �ʱ�ȭ �� ���� �ڵ� ����
    private void OnEnable()
    {
        role = UnitType.Worker; // �� �����ʹ� �׻� Worker ����
    }
}