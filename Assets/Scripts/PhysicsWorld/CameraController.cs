using UnityEngine;

namespace PhysicsWorld
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform _cameraParent;
        [SerializeField] private float _speed = 2;

        [SerializeField] private Camera _camera;
        [SerializeField] private float _minVertical = -45;
        [SerializeField] private float _maxVertical = 45;
        
        public Vector3 CameraPos => _camera.transform.position;

        public Camera Camera => _camera;

        public Vector3 ForwardDirectionProjected => Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up) ;
        public Vector3 ForwardDirection => _camera.transform.forward ;
        public Vector3 RightDirection => Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up) ;
        
        private void Update()
        {
            bool left = Input.GetKey(KeyCode.A);
            bool right = Input.GetKey(KeyCode.D);
            bool up = Input.GetKey(KeyCode.W);
            bool down = Input.GetKey(KeyCode.S);

            float horizontal = 0;
            float vertical = 0;
    
            if (left)
            {
                horizontal = 1;
            }
            else if (right)
            {
                horizontal = -1;
            }
            else if (up)
            {
                vertical = 1;
                vertical = Mathf.Clamp(vertical, _minVertical, _maxVertical);
            }
            else if (down)
            {
                vertical = -1;
                vertical = Mathf.Clamp(vertical, _minVertical, _maxVertical);
            }

            Vector3 eulerAngles = _cameraParent.transform.eulerAngles;
            eulerAngles.y += horizontal * Time.deltaTime * _speed;
            eulerAngles.x += vertical * Time.deltaTime * _speed;
            _cameraParent.transform.rotation = Quaternion.Euler(eulerAngles);
        }
    }
}