using UnityEngine;

namespace PhysicsWorld
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _speed = 8;
        [SerializeField] private Transform _explosionSphere;
        
        private PhysicsWordController _physicsWordController;
        private CameraController _cameraController;

        private void Awake()
        {
            _physicsWordController = FindObjectOfType<PhysicsWordController>();
            _cameraController = FindObjectOfType<CameraController>();
        }

        public void SetExplosionSphereRadius(float radius)
        {
            _explosionSphere.localScale = Vector3.one * (radius * 2);
        }
        
        private void Update()
        {
            SetExplosionSphereRadius(_physicsWordController.ExplosionRadius);

            float x = Input.GetAxis("Horizontal");
            float y = Input.GetAxis("Vertical");

            Vector3 move = y * _cameraController.ForwardDirectionProjected + x * _cameraController.RightDirection;

            if (Input.GetKey(KeyCode.LeftShift))
            {
                move.y = -1;
            }
            else if (Input.GetKey(KeyCode.Space))
            {
                move.y = 1;
            }
            
            transform.position += move * (Time.deltaTime * _speed);
        }
    }
}