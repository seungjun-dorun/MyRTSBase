using UnityEngine;
using System.Collections.Generic;
using RTSGame.Commands;
using Unity.VisualScripting; // CommandSystem 네임스페이스

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Core References")]
    public DeterministicTimeManager timeManager;
    public GameLogicManager gameLogicManager;
    public HexGridSystem hexGridSystem;
    public Camera mainCamera; // 게임 플레이에 사용될 메인 카메라
    public UnitManager unitManager; // 유닛 관리 시스템 (유닛 선택 및 명령 처리에 사용)

    [Header("Input Settings")]
    public LayerMask groundLayerMask; // 마우스 클릭으로 지형 위치를 얻기 위한 레이어 마스크
    public LayerMask unitLayerMask;   // 유닛 선택/타겟팅을 위한 레이어 마스크
    public float dragSelectMinDistance = 10f; // 드래그 선택으로 인정할 최소 마우스 이동 거리 (픽셀)

    [Header("State")]
    public ulong localPlayerId = 0; // 싱글 플레이어이므로 보통 0 또는 1
    public int inputDelayTicks = 2; // 입력 지연 시뮬레이션 (싱글에서는 0으로 해도 무방)

    // 유닛 선택 관련
    private List<SimulatedObject> _selectedUnits = new List<SimulatedObject>();
    private Vector2 _selectionBoxStartPos;
    private bool _isDraggingSelectionBox = false;

    // 건물 건설 등 특수 입력 모드 관련 (예시)
    private bool _isPlacingBuildingMode = false;
    private BuildingData _buildingDataToPlace; // 건설할 건물의 데이터 (ScriptableObject)
    private GameObject _buildingPlacementPreview; // 미리보기 오브젝트

    private bool _DoHandleUnitCommands = true;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (timeManager == null) timeManager = FindFirstObjectByType<DeterministicTimeManager>();
        if (gameLogicManager == null) gameLogicManager = FindFirstObjectByType<GameLogicManager>();
        if (hexGridSystem == null) hexGridSystem = FindFirstObjectByType<HexGridSystem>();
        if (unitManager == null) unitManager = FindFirstObjectByType<UnitManager>();

        if (mainCamera == null || timeManager == null || gameLogicManager == null || hexGridSystem == null || unitManager == null)
        {
            Debug.LogError("PlayerInputHandler: 필수 참조 중 하나 이상이 할당되지 않았습니다!");
            enabled = false;
        }
    }

    void Update()
    {
        if (_isPlacingBuildingMode)
        {
            UpdateBuildingPlacementPreview();
            if (Input.GetMouseButtonDown(0)) // 왼쪽 클릭으로 배치 확정
            {
                PlaceBuildingCommand();
            }
            if (Input.GetMouseButtonDown(1) || Input.GetKey(KeyCode.Escape)) // 오른쪽 클릭 또는 ESC로 취소
            {
                CancelBuildingPlacement();
            }
        }
        else
        {
            HandleUnitSelection();

            if (_selectedUnits.Count > 0) // 선택된 유닛이 있을 때만 명령 처리 로직 실행
            {
                if (HasSelectedWorkerUnits())
                {
                    _DoHandleUnitCommands = HandleWorkerCommands();
                }

                if (HasSelectedBuildingUnits())
                {
                    _DoHandleUnitCommands = false; // 건물 선택 시 유닛 명령 처리 비활성화
                    HandleBuildingCommands();
                }

                if(_DoHandleUnitCommands)
                {
                    HandleUnitCommands();
                }
            }
        }
    }
    // 선택된 유닛 중 일꾼이 있는지 확인하는 헬퍼 메서드
    bool HasSelectedWorkerUnits()
    {
        foreach (SimulatedObject unit in _selectedUnits)
        {
            if (unit is WorkerUnit)
            {
                return true;
            }
        }
        return false;
    }

    // 선택된 유닛 중 건물이 있는지 확인하는 헬퍼 메서드 (Building 클래스가 SimulatedObject를 상속한다고 가정)
    bool HasSelectedBuildingUnits()
    {
        foreach (SimulatedObject unit in _selectedUnits)
        {
            if (unit is Building) // Building 클래스가 정의되어 있다고 가정
            {
                return true;
            }
        }
        return false;
    }
    #region Unit Selection
    // 드래그 선택 박스 그리기 (OnGUI 사용)
    void OnGUI()
    {
        if (_isDraggingSelectionBox)
        {
            Rect rect = GetScreenRect(_selectionBoxStartPos, Input.mousePosition);
            DrawScreenRect(rect, new Color(0.8f, 0.8f, 0.95f, 0.25f));
            DrawScreenRectBorder(rect, 2, new Color(0.8f, 0.8f, 0.95f));
        }
    }

    void HandleUnitSelection()
    {
        // 왼쪽 마우스 버튼 클릭/드래그로 유닛 선택
        if (Input.GetMouseButtonDown(0)) // 왼쪽 버튼 누르는 순간
        {
            _selectionBoxStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0)) // 왼쪽 버튼 누르고 있는 동안 (드래그 가능성)
        {
            if (!_isDraggingSelectionBox && Vector2.Distance(_selectionBoxStartPos, Input.mousePosition) > dragSelectMinDistance)
            {
                _isDraggingSelectionBox = true; // 드래그 시작
            }
        }
        else if (Input.GetMouseButtonUp(0)) // 왼쪽 버튼 떼는 순간
        {
            if (_isDraggingSelectionBox)
            {
                // 드래그 선택 완료
                SelectUnitsInDragBox();
            }
            else
            {
                // 단순 클릭 선택
                SelectUnitAtMousePosition();
            }
            _isDraggingSelectionBox = false;
        }
    }

    void SelectUnitAtMousePosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayerMask))
        {
            SimulatedObject clickedUnit = hit.collider.GetComponent<SimulatedObject>();
            if (clickedUnit != null && clickedUnit.OwnerPlayerId == localPlayerId) // 자신의 유닛만 선택
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    ClearSelectedUnits(); // Shift 없이 클릭 시 기존 선택 해제
                }
                ToggleUnitSelection(clickedUnit);
            }
            else // 유닛이 아니거나 적 유닛 클릭 시
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    ClearSelectedUnits();
                }
            }
        }
        else // 아무것도 클릭하지 않았을 때 (지형 등)
        {
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                ClearSelectedUnits();
            }
        }
        UpdateSelectionVisuals();
    }

    void SelectUnitsInDragBox()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            ClearSelectedUnits();
        }

        Rect selectionRect = GetScreenRect(_selectionBoxStartPos, Input.mousePosition);

        // 모든 내 유닛을 순회하며 드래그 박스 안에 있는지 확인
        SimulatedObject[] allPlayerUnits = unitManager.GetPlayerUnits(localPlayerId);
        foreach (SimulatedObject unit in allPlayerUnits)
        {
            if (unit.OwnerPlayerId == localPlayerId)
            {
                Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPos.z > 0 && selectionRect.Contains(screenPos)) // 화면 앞에 있고, 박스 안에 있다면
                {
                    AddUnitToSelection(unit);
                }
            }
        }
        UpdateSelectionVisuals();
    }

    void ClearSelectedUnits()
    {
        foreach (SimulatedObject unit in _selectedUnits)
        {
            unit.Deselect();
        }
        _selectedUnits.Clear();
    }

    void ToggleUnitSelection(SimulatedObject unit)
    {
        if (_selectedUnits.Contains(unit))
        {
            _selectedUnits.Remove(unit);
            unit.Deselect();
        }
        else
        {
            _selectedUnits.Add(unit);
            unit.Select();
        }
    }

    void AddUnitToSelection(SimulatedObject unit)
    {
        if (!_selectedUnits.Contains(unit))
        {
            _selectedUnits.Add(unit);
            unit.Select();
        }
    }

    void UpdateSelectionVisuals()
    {
        // TODO: 선택된 유닛들에게 시각적 피드백 (하이라이트, UI 표시 등)
        Debug.Log("Selected Units: " + _selectedUnits.Count);
        // foreach (var unit in _selectedUnits) { Debug.Log(" - " + unit.InstanceID); }
    }

    // 화면 좌표 기준 사각형 그리기 함수들
    public static Rect GetScreenRect(Vector3 screenPosition1, Vector3 screenPosition2)
    {
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;
        var topLeft = Vector3.Min(screenPosition1, screenPosition2);
        var bottomRight = Vector3.Max(screenPosition1, screenPosition2);
        return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
    }
    public static void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
    public static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color); // Top
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color); // Bottom
        DrawScreenRect(new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - 2 * thickness), color); // Left
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - 2 * thickness), color); // Right
    }


    #endregion

    #region Unit Commands

    void HandleUnitCommands()
    {

        // 오른쪽 마우스 버튼 클릭으로 명령
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            List<int> selectedUnitInstanceIDs = new List<int>();
            foreach (SimulatedObject unit in _selectedUnits)
            {
                selectedUnitInstanceIDs.Add(unit.InstanceID);
            }

            ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayerMask)) // unitLayerMask는 유닛과 건물을 포함
            {
                SimulatedObject targetEntity = hit.collider.GetComponentInParent<SimulatedObject>();
                if (targetEntity != null)
                {
                    if (targetEntity.OwnerPlayerId != localPlayerId) // 적대적 또는 중립 엔티티
                    {
                        AttackUnitCommand attackCmd = new AttackUnitCommand(localPlayerId, selectedUnitInstanceIDs, targetEntity.InstanceID, executionTick);
                        gameLogicManager.ProcessLocalCommand(attackCmd);
                        return;
                    }
                    else // 아군 엔티티 (예: 특정 건물에 자원 반납, 유닛 수리 등 특수 상호작용)
                    {
                        // 아군 엔티티 우클릭 시 특수 명령 처리 로직 (예: 일꾼이 건물 수리)
                        // 지금은 아무것도 안 함 (또는 이동)
                    }
                }
            }


            // 2. 지형 타겟팅 (이동 또는 어택땅)
            Plane xzPlane = new Plane(Vector3.up, new Vector3(0, hexGridSystem.defaultYPosition, 0));
            float distance;
            if (xzPlane.Raycast(ray, out distance))
            {
                Vector3 clickWorldPosition = ray.GetPoint(distance);
                Vector3Int targetCubeCoord = hexGridSystem.WorldToCube(clickWorldPosition);

                if (hexGridSystem.IsValidHex(targetCubeCoord) && hexGridSystem.GetTileAt(targetCubeCoord).isWalkable)
                {
                    if (Input.GetKey(KeyCode.A)) // A 키 (어택땅 키로 가정) 누르고 클릭 시
                    {
                        AttackPositionCommand attackPosCmd = new AttackPositionCommand(localPlayerId, selectedUnitInstanceIDs, targetCubeCoord, executionTick);
                        gameLogicManager.ProcessLocalCommand(attackPosCmd);
                        Debug.Log("Attack Position Command Issued");
                    }
                    else // 단순 이동
                    {
                        MoveCommand moveCmd = new MoveCommand(localPlayerId, selectedUnitInstanceIDs, targetCubeCoord, executionTick);
                        gameLogicManager.ProcessLocalCommand(moveCmd);
                        // Debug.Log("Move Command (to coord) Issued to " + targetCubeCoord);
                    }
                }
            }
        }

        // S 키 (Stop)
        if (Input.GetKey(KeyCode.S))
        {
            List<int> selectedUnitInstanceIDs = GetSelectedUnitInstanceIDs();
            if (selectedUnitInstanceIDs.Count > 0)
            {
                ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
                StopCommand stopCmd = new StopCommand(localPlayerId, selectedUnitInstanceIDs, executionTick);
                gameLogicManager.ProcessLocalCommand(stopCmd);
            }
        }

        // H 키 (Hold)
        if (Input.GetKey(KeyCode.H))
        {
            List<int> selectedUnitInstanceIDs = GetSelectedUnitInstanceIDs();
            if (selectedUnitInstanceIDs.Count > 0)
            {
                ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
                HoldPositionCommand holdCmd = new HoldPositionCommand(localPlayerId, selectedUnitInstanceIDs, executionTick);
                gameLogicManager.ProcessLocalCommand(holdCmd);
            }
        }

    }
    bool HandleWorkerCommands()
    {
        
        // 자원 채집 명령 (우클릭) - HandleUnitCommands에서 이미 유사 로직이 있으므로 통합 또는 분리 필요
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayerMask)) // 자원도 unitLayerMask에 포함 가정
            {
                ResourceNode resourceNode = hit.collider.GetComponent<ResourceNode>();
                if (resourceNode != null && !resourceNode.IsDepleted)
                {
                    List<int> workerInstanceIDs = GetSelectedWorkerInstanceIDs();
                    if (workerInstanceIDs.Count > 0)
                    {
                        SimulatedObject resourceSimObject = resourceNode.GetComponent<SimulatedObject>();
                        if (resourceSimObject != null)
                        {
                            ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
                            GatherResourceCommand gatherCmd = new GatherResourceCommand(localPlayerId, workerInstanceIDs, resourceSimObject.InstanceID, executionTick);
                            gameLogicManager.ProcessLocalCommand(gatherCmd);
                            Debug.Log($"HandleWorkerCommands: Gather Resource Command Issued to {resourceNode.name}");
                            return false; // 일꾼 명령 처리 완료
                        }
                    }
                }
            }
        // 자원 노드가 없거나 선택된 일꾼이 없는 경우
        }
        // TODO: 기타 일꾼 전용 명령 (예: 건물 건설 시작 키 'B' 등)
        return true;
    }

    bool HandleBuildingCommands()
    {
        // 건물 선택 시 집결지 설정 (우클릭) - HandleUnitCommands에서 이미 유사 로직이 있으므로 통합 또는 분리 필요
        if (Input.GetMouseButtonDown(1))
        {
            Building selectedBuilding = _selectedUnits[0] as Building;
            if (selectedBuilding.CanProduceUnits) // 생산 건물이라면 집결지 설정
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Plane xzPlane = new Plane(Vector3.up, new Vector3(0, hexGridSystem.defaultYPosition, 0));
                float distance;
                if (xzPlane.Raycast(ray, out distance))
                {
                    Vector3 clickWorldPosition = ray.GetPoint(distance);
                    Vector3Int targetCubeCoord = hexGridSystem.WorldToCube(clickWorldPosition);

                    if (hexGridSystem.IsValidHex(targetCubeCoord))
                    {
                        ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
                        SetRallyPointCommand rallyCmd = new SetRallyPointCommand(localPlayerId, new List<int> { selectedBuilding.InstanceID }, targetCubeCoord, executionTick);
                        gameLogicManager.ProcessLocalCommand(rallyCmd);
                        Debug.Log("Set Rally Point Command Issued for Building " + selectedBuilding.InstanceID);
                        return false; // 건물 명령 처리 완료
                    }
                }
            }
            // 현재 HandleUnitCommands에 있는 집결지 설정 로직을 이곳으로 옮기거나,
            // 여기서는 건물 전용 UI와 상호작용하는 로직 등을 추가할 수 있습니다.
        }
        return true;
        // TODO: 유닛 생산 명령 (단축키 또는 UI 연동)
    }

    List<int> GetSelectedUnitInstanceIDs()
    {
        List<int> ids = new List<int>();
        foreach (var unit in _selectedUnits)
        {
            ids.Add(unit.InstanceID);
        }
        return ids;
    }

    #endregion

    #region Building Placement
    // TODO : 미완성된 코드. 확인 필요.

    // UI 버튼 등에서 호출
    public void StartBuildingPlacementMode(BuildingData buildingData)
    {
        if (buildingData == null || buildingData.unitPrefab == null)
        {
            Debug.LogError("유효하지 않은 BuildingData 또는 프리팹입니다.");
            return;
        }
        if (_isPlacingBuildingMode) CancelBuildingPlacement();

        _isPlacingBuildingMode = true;
        _buildingDataToPlace = buildingData;

        _buildingPlacementPreview = Instantiate(buildingData.unitPrefab);
        // 미리보기 오브젝트의 콜라이더 및 불필요한 스크립트 비활성화
        foreach (var col in _buildingPlacementPreview.GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var script in _buildingPlacementPreview.GetComponentsInChildren<MonoBehaviour>())
        {
            if (script != _buildingPlacementPreview.transform) script.enabled = false; // 자기 자신(Transform) 제외
        }
        // TODO: 미리보기용 반투명 머티리얼 적용
        Debug.Log($"건물 배치 모드 시작: {_buildingDataToPlace.unitName}");
    }

    void UpdateBuildingPlacementPreview()
    {
        if (_buildingPlacementPreview == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane xzPlane = new Plane(Vector3.up, new Vector3(0, hexGridSystem.defaultYPosition, 0));
        float distance;

        if (xzPlane.Raycast(ray, out distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            Vector3Int cubeCoord = hexGridSystem.WorldToCube(worldPos);
            HexTile tile;
            if (hexGridSystem.TryGetTileAt(cubeCoord, out tile))
            {
                _buildingPlacementPreview.transform.position = tile.worldPosition;
                bool canPlace = gameLogicManager.GetComponent<BuildingManager>().CanPlaceBuildingAt(_buildingDataToPlace, cubeCoord, localPlayerId);
                // SetPreviewMaterialColor(canPlace); // 건설 가능 여부에 따라 색상 변경
            }
        }
    }

    void PlaceBuildingCommand()
    {
        if (!_isPlacingBuildingMode || _buildingPlacementPreview == null || _buildingDataToPlace == null) return;

        Vector3Int buildCubeCoord = hexGridSystem.WorldToCube(_buildingPlacementPreview.transform.position);

        // bool canPlace = gameLogicManager.GetComponent<BuildingManager>().CanPlaceBuildingAt(_buildingDataToPlace, buildCubeCoord, localPlayerId);
        // if (!canPlace) { Debug.Log("해당 위치에 건물을 지을 수 없습니다."); return; }

        ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
        // 일꾼 선택 로직 추가 필요: 현재 선택된 일꾼 중 하나를 사용하거나, 가장 가까운 유휴 일꾼 자동 할당
        List<int> workerIds = GetSelectedWorkerInstanceIDs(); // 선택된 일꾼 가져오기 (구현 필요)
        if (workerIds.Count == 0) { Debug.Log("건설할 일꾼이 선택되지 않았습니다."); /* 또는 자동 할당 */ CancelBuildingPlacement(); return; }


        BuildBuildingCommand buildCmd = new BuildBuildingCommand(localPlayerId, workerIds, _buildingDataToPlace.unitName, buildCubeCoord, executionTick); // entityName을 ID로 사용
        gameLogicManager.ProcessLocalCommand(buildCmd);

        CancelBuildingPlacement();
    }

    void CancelBuildingPlacement()
    {
        _isPlacingBuildingMode = false;
        _buildingDataToPlace = null;
        if (_buildingPlacementPreview != null)
        {
            Destroy(_buildingPlacementPreview);
            _buildingPlacementPreview = null;
        }
    }

    List<int> GetSelectedWorkerInstanceIDs()
    {
        List<int> workerIDs = new List<int>();
        foreach (var entity in _selectedUnits)
        {
            if (entity is WorkerUnit) // WorkerUnit 타입인지 확인
            {
                workerIDs.Add(entity.InstanceID);
            }
        }
        return workerIDs;
    }

    #endregion
}