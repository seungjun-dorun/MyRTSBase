using UnityEngine;
using System.Collections.Generic;
using RTSGame.Commands;
using Unity.VisualScripting; // CommandSystem ���ӽ����̽�

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Core References")]
    public DeterministicTimeManager timeManager;
    public GameLogicManager gameLogicManager;
    public HexGridSystem hexGridSystem;
    public Camera mainCamera; // ���� �÷��̿� ���� ���� ī�޶�
    public UnitManager unitManager; // ���� ���� �ý��� (���� ���� �� ��� ó���� ���)

    [Header("Input Settings")]
    public LayerMask groundLayerMask; // ���콺 Ŭ������ ���� ��ġ�� ��� ���� ���̾� ����ũ
    public LayerMask unitLayerMask;   // ���� ����/Ÿ������ ���� ���̾� ����ũ
    public float dragSelectMinDistance = 10f; // �巡�� �������� ������ �ּ� ���콺 �̵� �Ÿ� (�ȼ�)

    [Header("State")]
    public ulong localPlayerId = 0; // �̱� �÷��̾��̹Ƿ� ���� 0 �Ǵ� 1
    public int inputDelayTicks = 2; // �Է� ���� �ùķ��̼� (�̱ۿ����� 0���� �ص� ����)

    // ���� ���� ����
    private List<SimulatedObject> _selectedUnits = new List<SimulatedObject>();
    private Vector2 _selectionBoxStartPos;
    private bool _isDraggingSelectionBox = false;

    // �ǹ� �Ǽ� �� Ư�� �Է� ��� ���� (����)
    private bool _isPlacingBuildingMode = false;
    private BuildingData _buildingDataToPlace; // �Ǽ��� �ǹ��� ������ (ScriptableObject)
    private GameObject _buildingPlacementPreview; // �̸����� ������Ʈ

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
            Debug.LogError("PlayerInputHandler: �ʼ� ���� �� �ϳ� �̻��� �Ҵ���� �ʾҽ��ϴ�!");
            enabled = false;
        }
    }

    void Update()
    {
        if (_isPlacingBuildingMode)
        {
            UpdateBuildingPlacementPreview();
            if (Input.GetMouseButtonDown(0)) // ���� Ŭ������ ��ġ Ȯ��
            {
                PlaceBuildingCommand();
            }
            if (Input.GetMouseButtonDown(1) || Input.GetKey(KeyCode.Escape)) // ������ Ŭ�� �Ǵ� ESC�� ���
            {
                CancelBuildingPlacement();
            }
        }
        else
        {
            HandleUnitSelection();

            if (_selectedUnits.Count > 0) // ���õ� ������ ���� ���� ��� ó�� ���� ����
            {
                if (HasSelectedWorkerUnits())
                {
                    _DoHandleUnitCommands = HandleWorkerCommands();
                }

                if (HasSelectedBuildingUnits())
                {
                    _DoHandleUnitCommands = false; // �ǹ� ���� �� ���� ��� ó�� ��Ȱ��ȭ
                    HandleBuildingCommands();
                }

                if(_DoHandleUnitCommands)
                {
                    HandleUnitCommands();
                }
            }
        }
    }
    // ���õ� ���� �� �ϲ��� �ִ��� Ȯ���ϴ� ���� �޼���
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

    // ���õ� ���� �� �ǹ��� �ִ��� Ȯ���ϴ� ���� �޼��� (Building Ŭ������ SimulatedObject�� ����Ѵٰ� ����)
    bool HasSelectedBuildingUnits()
    {
        foreach (SimulatedObject unit in _selectedUnits)
        {
            if (unit is Building) // Building Ŭ������ ���ǵǾ� �ִٰ� ����
            {
                return true;
            }
        }
        return false;
    }
    #region Unit Selection
    // �巡�� ���� �ڽ� �׸��� (OnGUI ���)
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
        // ���� ���콺 ��ư Ŭ��/�巡�׷� ���� ����
        if (Input.GetMouseButtonDown(0)) // ���� ��ư ������ ����
        {
            _selectionBoxStartPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0)) // ���� ��ư ������ �ִ� ���� (�巡�� ���ɼ�)
        {
            if (!_isDraggingSelectionBox && Vector2.Distance(_selectionBoxStartPos, Input.mousePosition) > dragSelectMinDistance)
            {
                _isDraggingSelectionBox = true; // �巡�� ����
            }
        }
        else if (Input.GetMouseButtonUp(0)) // ���� ��ư ���� ����
        {
            if (_isDraggingSelectionBox)
            {
                // �巡�� ���� �Ϸ�
                SelectUnitsInDragBox();
            }
            else
            {
                // �ܼ� Ŭ�� ����
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
            if (clickedUnit != null && clickedUnit.OwnerPlayerId == localPlayerId) // �ڽ��� ���ָ� ����
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    ClearSelectedUnits(); // Shift ���� Ŭ�� �� ���� ���� ����
                }
                ToggleUnitSelection(clickedUnit);
            }
            else // ������ �ƴϰų� �� ���� Ŭ�� ��
            {
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    ClearSelectedUnits();
                }
            }
        }
        else // �ƹ��͵� Ŭ������ �ʾ��� �� (���� ��)
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

        // ��� �� ������ ��ȸ�ϸ� �巡�� �ڽ� �ȿ� �ִ��� Ȯ��
        SimulatedObject[] allPlayerUnits = unitManager.GetPlayerUnits(localPlayerId);
        foreach (SimulatedObject unit in allPlayerUnits)
        {
            if (unit.OwnerPlayerId == localPlayerId)
            {
                Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPos.z > 0 && selectionRect.Contains(screenPos)) // ȭ�� �տ� �ְ�, �ڽ� �ȿ� �ִٸ�
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
        // TODO: ���õ� ���ֵ鿡�� �ð��� �ǵ�� (���̶���Ʈ, UI ǥ�� ��)
        Debug.Log("Selected Units: " + _selectedUnits.Count);
        // foreach (var unit in _selectedUnits) { Debug.Log(" - " + unit.InstanceID); }
    }

    // ȭ�� ��ǥ ���� �簢�� �׸��� �Լ���
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

        // ������ ���콺 ��ư Ŭ������ ���
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

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayerMask)) // unitLayerMask�� ���ְ� �ǹ��� ����
            {
                SimulatedObject targetEntity = hit.collider.GetComponentInParent<SimulatedObject>();
                if (targetEntity != null)
                {
                    if (targetEntity.OwnerPlayerId != localPlayerId) // ������ �Ǵ� �߸� ��ƼƼ
                    {
                        AttackUnitCommand attackCmd = new AttackUnitCommand(localPlayerId, selectedUnitInstanceIDs, targetEntity.InstanceID, executionTick);
                        gameLogicManager.ProcessLocalCommand(attackCmd);
                        return;
                    }
                    else // �Ʊ� ��ƼƼ (��: Ư�� �ǹ��� �ڿ� �ݳ�, ���� ���� �� Ư�� ��ȣ�ۿ�)
                    {
                        // �Ʊ� ��ƼƼ ��Ŭ�� �� Ư�� ��� ó�� ���� (��: �ϲ��� �ǹ� ����)
                        // ������ �ƹ��͵� �� �� (�Ǵ� �̵�)
                    }
                }
            }


            // 2. ���� Ÿ���� (�̵� �Ǵ� ���ö�)
            Plane xzPlane = new Plane(Vector3.up, new Vector3(0, hexGridSystem.defaultYPosition, 0));
            float distance;
            if (xzPlane.Raycast(ray, out distance))
            {
                Vector3 clickWorldPosition = ray.GetPoint(distance);
                Vector3Int targetCubeCoord = hexGridSystem.WorldToCube(clickWorldPosition);

                if (hexGridSystem.IsValidHex(targetCubeCoord) && hexGridSystem.GetTileAt(targetCubeCoord).isWalkable)
                {
                    if (Input.GetKey(KeyCode.A)) // A Ű (���ö� Ű�� ����) ������ Ŭ�� ��
                    {
                        AttackPositionCommand attackPosCmd = new AttackPositionCommand(localPlayerId, selectedUnitInstanceIDs, targetCubeCoord, executionTick);
                        gameLogicManager.ProcessLocalCommand(attackPosCmd);
                        Debug.Log("Attack Position Command Issued");
                    }
                    else // �ܼ� �̵�
                    {
                        MoveCommand moveCmd = new MoveCommand(localPlayerId, selectedUnitInstanceIDs, targetCubeCoord, executionTick);
                        gameLogicManager.ProcessLocalCommand(moveCmd);
                        // Debug.Log("Move Command (to coord) Issued to " + targetCubeCoord);
                    }
                }
            }
        }

        // S Ű (Stop)
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

        // H Ű (Hold)
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
        
        // �ڿ� ä�� ��� (��Ŭ��) - HandleUnitCommands���� �̹� ���� ������ �����Ƿ� ���� �Ǵ� �и� �ʿ�
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, unitLayerMask)) // �ڿ��� unitLayerMask�� ���� ����
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
                            return false; // �ϲ� ��� ó�� �Ϸ�
                        }
                    }
                }
            }
        // �ڿ� ��尡 ���ų� ���õ� �ϲ��� ���� ���
        }
        // TODO: ��Ÿ �ϲ� ���� ��� (��: �ǹ� �Ǽ� ���� Ű 'B' ��)
        return true;
    }

    bool HandleBuildingCommands()
    {
        // �ǹ� ���� �� ������ ���� (��Ŭ��) - HandleUnitCommands���� �̹� ���� ������ �����Ƿ� ���� �Ǵ� �и� �ʿ�
        if (Input.GetMouseButtonDown(1))
        {
            Building selectedBuilding = _selectedUnits[0] as Building;
            if (selectedBuilding.CanProduceUnits) // ���� �ǹ��̶�� ������ ����
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
                        return false; // �ǹ� ��� ó�� �Ϸ�
                    }
                }
            }
            // ���� HandleUnitCommands�� �ִ� ������ ���� ������ �̰����� �ű�ų�,
            // ���⼭�� �ǹ� ���� UI�� ��ȣ�ۿ��ϴ� ���� ���� �߰��� �� �ֽ��ϴ�.
        }
        return true;
        // TODO: ���� ���� ��� (����Ű �Ǵ� UI ����)
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
    // TODO : �̿ϼ��� �ڵ�. Ȯ�� �ʿ�.

    // UI ��ư ��� ȣ��
    public void StartBuildingPlacementMode(BuildingData buildingData)
    {
        if (buildingData == null || buildingData.unitPrefab == null)
        {
            Debug.LogError("��ȿ���� ���� BuildingData �Ǵ� �������Դϴ�.");
            return;
        }
        if (_isPlacingBuildingMode) CancelBuildingPlacement();

        _isPlacingBuildingMode = true;
        _buildingDataToPlace = buildingData;

        _buildingPlacementPreview = Instantiate(buildingData.unitPrefab);
        // �̸����� ������Ʈ�� �ݶ��̴� �� ���ʿ��� ��ũ��Ʈ ��Ȱ��ȭ
        foreach (var col in _buildingPlacementPreview.GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var script in _buildingPlacementPreview.GetComponentsInChildren<MonoBehaviour>())
        {
            if (script != _buildingPlacementPreview.transform) script.enabled = false; // �ڱ� �ڽ�(Transform) ����
        }
        // TODO: �̸������ ������ ��Ƽ���� ����
        Debug.Log($"�ǹ� ��ġ ��� ����: {_buildingDataToPlace.unitName}");
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
                // SetPreviewMaterialColor(canPlace); // �Ǽ� ���� ���ο� ���� ���� ����
            }
        }
    }

    void PlaceBuildingCommand()
    {
        if (!_isPlacingBuildingMode || _buildingPlacementPreview == null || _buildingDataToPlace == null) return;

        Vector3Int buildCubeCoord = hexGridSystem.WorldToCube(_buildingPlacementPreview.transform.position);

        // bool canPlace = gameLogicManager.GetComponent<BuildingManager>().CanPlaceBuildingAt(_buildingDataToPlace, buildCubeCoord, localPlayerId);
        // if (!canPlace) { Debug.Log("�ش� ��ġ�� �ǹ��� ���� �� �����ϴ�."); return; }

        ulong executionTick = timeManager.CurrentTick + (ulong)inputDelayTicks;
        // �ϲ� ���� ���� �߰� �ʿ�: ���� ���õ� �ϲ� �� �ϳ��� ����ϰų�, ���� ����� ���� �ϲ� �ڵ� �Ҵ�
        List<int> workerIds = GetSelectedWorkerInstanceIDs(); // ���õ� �ϲ� �������� (���� �ʿ�)
        if (workerIds.Count == 0) { Debug.Log("�Ǽ��� �ϲ��� ���õ��� �ʾҽ��ϴ�."); /* �Ǵ� �ڵ� �Ҵ� */ CancelBuildingPlacement(); return; }


        BuildBuildingCommand buildCmd = new BuildBuildingCommand(localPlayerId, workerIds, _buildingDataToPlace.unitName, buildCubeCoord, executionTick); // entityName�� ID�� ���
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
            if (entity is WorkerUnit) // WorkerUnit Ÿ������ Ȯ��
            {
                workerIDs.Add(entity.InstanceID);
            }
        }
        return workerIDs;
    }

    #endregion
}