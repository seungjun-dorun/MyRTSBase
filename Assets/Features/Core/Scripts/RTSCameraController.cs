using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;             // Ű����/���콺 �����ڸ� �̵� �ӵ�
    public float panBorderThickness = 10f;   // ȭ�� �����ڸ� ���� �β� (�ȼ�)
    public Vector2 panLimitX = new Vector2(-50f, 50f); // X�� �̵� ���� (�ּ�, �ִ�)
    public Vector2 panLimitZ = new Vector2(-50f, 50f); // Z�� �̵� ���� (�ּ�, �ִ�)

    [Header("Zoom Settings")]
    public float scrollSpeed = 20f;          // ���콺 �� �� �ӵ�
    public float minY = 10f;                 // �ּ� Y ���� (�� �ƿ� ����)
    public float maxY = 80f;                 // �ִ� Y ���� (�� �� ����)

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

        // ���� �̵� (A/D �Ǵ� ����/������ ȭ��ǥ)
        if ( Input.GetKey(KeyCode.LeftArrow))
        {
            pos.x -= panSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            pos.x += panSpeed * Time.deltaTime;
        }

        // ���� �̵� (W/S �Ǵ� ��/�Ʒ� ȭ��ǥ) - RTS������ ���� Z�� �̵�
        if (Input.GetKey(KeyCode.UpArrow))
        {
            pos.z += panSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            pos.z -= panSpeed * Time.deltaTime;
        }

        // �̵� ���� ����
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;
    }

    void HandleMouseEdgePanning()
    {
        Vector3 pos = transform.position;
        Vector2 mousePos = Input.mousePosition;

        // ȭ�� ���� �����ڸ�
        if (mousePos.y >= Screen.height - panBorderThickness)
        {
            pos.z += panSpeed * Time.deltaTime;
        }
        // ȭ�� �Ʒ��� �����ڸ�
        if (mousePos.y <= panBorderThickness)
        {
            pos.z -= panSpeed * Time.deltaTime;
        }
        // ȭ�� ������ �����ڸ�
        if (mousePos.x >= Screen.width - panBorderThickness)
        {
            pos.x += panSpeed * Time.deltaTime;
        }
        // ȭ�� ���� �����ڸ�
        if (mousePos.x <= panBorderThickness)
        {
            pos.x -= panSpeed * Time.deltaTime;
        }

        // �̵� ���� ����
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;
    }

    void HandleMouseZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel"); // ���콺 �� �Է� (-1, 0, 1)

        Vector3 pos = transform.position;
        pos.y -= scroll * scrollSpeed * 100f * Time.deltaTime; // 100f�� ���� ������ ���
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // (���� ����) �� ������ ���� ī�޶� ���� ����
        // ��: �� ���� �� �ƿ��Ҽ��� ī�޶� �� �������� �����ٺ�����
        // float currentZoomRatio = (pos.y - minY) / (maxY - minY); // 0 (�ִ� ����) ~ 1 (�ִ� �ܾƿ�)
        // float targetXRotation = Mathf.Lerp(45f, 75f, currentZoomRatio); // ��: 45��(����) ~ 75��(�ܾƿ�)
        // transform.rotation = Quaternion.Euler(targetXRotation, transform.eulerAngles.y, transform.eulerAngles.z);

        transform.position = pos;
    }

    // (���� ����) Ư�� ��ġ�� ī�޶� ��� �̵� (��: �̴ϸ� Ŭ�� ��)
    public void MoveToPosition(Vector3 targetPosition)
    {
        Vector3 newPos = targetPosition;
        // Y ���̴� ���� ī�޶� Y ���� ���� �Ǵ� ������ ���
        newPos.y = transform.position.y;

        // �̵� ���� ����
        newPos.x = Mathf.Clamp(newPos.x, panLimitX.x, panLimitX.y);
        newPos.z = Mathf.Clamp(newPos.z, panLimitZ.x, panLimitZ.y);

        transform.position = newPos;
    }
}