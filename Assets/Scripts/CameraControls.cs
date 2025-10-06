using UnityEngine;

public class CameraControls : MonoBehaviour
{
    private const float CameraZoomSpeed = 2f;
    private const float CameraMinDistance = 5f;
    private const float CameraMaxDistance = 100f;
    private const float CameraOrbitSpeed = 5f;
    private const float CameraArrowKeyOrbitSpeed = 60f;

    private float _cameraDistance = 20f;
    private float _pitch = 20f;
    private float _yaw;

    private void Start()
    {
        var dir = transform.position - Vector3.zero;
        _cameraDistance = dir.magnitude;
        _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        _pitch = Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg;
    }

    private void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * CameraOrbitSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * CameraOrbitSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        }

        var arrowDelta = CameraArrowKeyOrbitSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftArrow))
            _yaw -= arrowDelta;
        if (Input.GetKey(KeyCode.RightArrow))
            _yaw += arrowDelta;
        if (Input.GetKey(KeyCode.UpArrow))
            _pitch += arrowDelta;
        if (Input.GetKey(KeyCode.DownArrow))
            _pitch -= arrowDelta;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);

        var scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _cameraDistance -= scroll * CameraZoomSpeed * _cameraDistance * 0.1f;
            _cameraDistance = Mathf.Clamp(_cameraDistance, CameraMinDistance, CameraMaxDistance);
        }

        var rot = Quaternion.Euler(_pitch, _yaw, 0);
        var target = Vector3.zero;
        transform.position = target + rot * new Vector3(0, 0, -_cameraDistance);
        transform.LookAt(target);
    }
}