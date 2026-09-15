using UnityEngine;

namespace Richman.Presentation
{
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Transform overviewTarget;
        [SerializeField] private float overviewDistance = 19f;
        [SerializeField] private float focusDistance = 9.5f;
        [SerializeField] private float rotateSpeed = 5f;
        [SerializeField] private float zoomSpeed = 2f;

        private Vector3 _focusPoint;
        private float _yaw;
        private float _pitch = 53f;
        private float _distance;

        public void SetSceneReferences(Camera camera, Transform target)
        {
            sceneCamera = camera;
            overviewTarget = target;
        }

        private void Awake()
        {
            if (sceneCamera == null) sceneCamera = GetComponentInChildren<Camera>();
            _focusPoint = overviewTarget == null ? transform.position : overviewTarget.position;
            _distance = overviewDistance;
            UpdateCamera(true);
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * rotateSpeed, 28f, 78f);
            }

            _distance = Mathf.Clamp(_distance - Input.mouseScrollDelta.y * zoomSpeed, 8f, 25f);

            UpdateCamera(false);
        }

        public void FocusOverview()
        {
            _focusPoint = overviewTarget == null ? transform.position : overviewTarget.position;
            _distance = overviewDistance;
        }

        public void Focus(Transform target)
        {
            if (target == null) return;
            _focusPoint = target.position;
            _distance = focusDistance;
        }

        private void UpdateCamera(bool immediate)
        {
            if (sceneCamera == null) return;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var desiredPosition = _focusPoint + rotation * Vector3.back * _distance;
            var smoothing = immediate ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 8f);
            sceneCamera.transform.position = Vector3.Lerp(sceneCamera.transform.position, desiredPosition, smoothing);
            var targetRotation = Quaternion.LookRotation(_focusPoint - sceneCamera.transform.position, Vector3.up);
            sceneCamera.transform.rotation = Quaternion.Slerp(sceneCamera.transform.rotation, targetRotation, smoothing);
        }
    }
}
