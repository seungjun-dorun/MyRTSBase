using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;             // 키보드/마우스 가장자리 이동 속도
    public float panBorderThickness = 10f;   // 화면 가장자리 감지 두께 (픽셀)
    public Vector2 panLimitX = new Vector2(-50f, 50f); // X축 이동 제한 (최소, 최대)
    public Vector2 panLimitZ = new Vector2(-50f, 50f); // Z축 이동 제한 (최소, 최대)

    [Header("Zoom Settings")]
    public float scrollSpeed = 20f;          // 마우스 휠 줌 속도
    public float minY = 10f;                 // 최소 Y 높이 (줌 아웃 제한)
    public float maxY = 80f;                 // 최대 Y 높이 (줌 인 제한)

    // Update is called once per frame
    void Update()
    {
        HandleKeyboardPanning();
        HandleMouseEdgePanning();
        HandleMouseZoom();
    }

    void HandleKeyboardPanning()
    {
        Vector3 pos = transform.position;

        // 수평 이동 (A/D 또는 왼쪽/오른쪽 화살표)
        if ( Input.GetKey(KeyCode.LeftArrow))
        {
            pos.x -= panSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            pos.x += panSpeed * Time.deltaTime;
        }

        // 수직 이동 (W/S 또는 위/아래 화살표) - RTS에서는 보통 Z축 이동
        if (Input.GetKey(KeyCode.UpArrow))
        {
            pos.z += panSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            pos.z -= panSpeed * Time.deltaTime;
        }

        // 이동 제한 적용
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;
    }

    void HandleMouseEdgePanning()
    {
        Vector3 pos = transform.position;
        Vector2 mousePos = Input.mousePosition;

        // 화면 위쪽 가장자리
        if (mousePos.y >= Screen.height - panBorderThickness)
        {
            pos.z += panSpeed * Time.deltaTime;
        }
        // 화면 아래쪽 가장자리
        if (mousePos.y <= panBorderThickness)
        {
            pos.z -= panSpeed * Time.deltaTime;
        }
        // 화면 오른쪽 가장자리
        if (mousePos.x >= Screen.width - panBorderThickness)
        {
            pos.x += panSpeed * Time.deltaTime;
        }
        // 화면 왼쪽 가장자리
        if (mousePos.x <= panBorderThickness)
        {
            pos.x -= panSpeed * Time.deltaTime;
        }

        // 이동 제한 적용
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;
    }

    void HandleMouseZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel"); // 마우스 휠 입력 (-1, 0, 1)

        Vector3 pos = transform.position;
        pos.y -= scroll * scrollSpeed * 100f * Time.deltaTime; // 100f는 감도 조절용 상수
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // (선택 사항) 줌 레벨에 따라 카메라 각도 변경
        // 예: 더 많이 줌 아웃할수록 카메라가 더 수직으로 내려다보도록
        // float currentZoomRatio = (pos.y - minY) / (maxY - minY); // 0 (최대 줌인) ~ 1 (최대 줌아웃)
        // float targetXRotation = Mathf.Lerp(45f, 75f, currentZoomRatio); // 예: 45도(줌인) ~ 75도(줌아웃)
        // transform.rotation = Quaternion.Euler(targetXRotation, transform.eulerAngles.y, transform.eulerAngles.z);

        transform.position = pos;
    }

    // (선택 사항) 특정 위치로 카메라 즉시 이동 (예: 미니맵 클릭 시)
    public void MoveToPosition(Vector3 targetPosition)
    {
        Vector3 newPos = targetPosition;
        // Y 높이는 현재 카메라 Y 높이 유지 또는 고정값 사용
        newPos.y = transform.position.y;

        // 이동 제한 적용
        newPos.x = Mathf.Clamp(newPos.x, panLimitX.x, panLimitX.y);
        newPos.z = Mathf.Clamp(newPos.z, panLimitZ.x, panLimitZ.y);

        transform.position = newPos;
    }
}