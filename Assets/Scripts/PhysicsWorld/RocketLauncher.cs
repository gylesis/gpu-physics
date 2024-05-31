using System;
using Unity.Mathematics;
using UnityEngine;

namespace PhysicsWorld
{
    public class RocketLauncher : MonoBehaviour
    {
        [SerializeField] private Transform _rocket;
        [SerializeField] private float _rocketSpeed = 5;

        private CameraController _cameraController;
        private PhysicsWordController _physicsWordController;

        private bool _isLaunched;
        private Vector3 _direction;

        private float _timer;

        public event Action RocketExploded;
        
        private void Awake()
        {
            _cameraController = FindObjectOfType<CameraController>();
            _physicsWordController = FindObjectOfType<PhysicsWordController>();

            _rocket.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetMouseButton(0))
            {
                if (_isLaunched) return;
                
                Launch();
            }

            if (_isLaunched)
            {
                _timer += Time.deltaTime;

                if (_timer > 0.5f)
                {
                    _timer = 0;
                    _isLaunched = false;
                    OnRocketExploded();
                    return;
                }
                
                
                _rocket.transform.position += _direction * (Time.deltaTime * _rocketSpeed);

                //RocketPos = _rocket.transform.position;
                _physicsWordController.SetRocketPos(_rocket.transform.position);
            }
        }

        private void Launch()
        {
            Vector3 clickPos = _cameraController.Camera.ScreenToWorldPoint(Input.mousePosition);
            Ray ray = _cameraController.Camera.ScreenPointToRay(Input.mousePosition);
            
            _direction = ray.direction;

            _rocket.transform.position = _cameraController.CameraPos ;
            
            _rocket.transform.rotation = Quaternion.LookRotation(_direction);
            
            _rocket.gameObject.SetActive(true);
            _isLaunched = true;

            _physicsWordController.LaunchRocket((OnRocketExploded));
        }

        private void OnRocketExploded()
        {
            RocketExploded?.Invoke();
            _isLaunched = false;
            _rocket.gameObject.SetActive(false);
        }
    }
}