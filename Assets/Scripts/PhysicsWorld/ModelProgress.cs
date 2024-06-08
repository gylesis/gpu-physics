using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsWorld
{
    public class ModelProgress : MonoBehaviour
    {
        [SerializeField] private Image _progress;
        
        private RocketLauncher _rocketLauncher;
        private PhysicsWordController _physicsWordController;

        public float Progress { get; private set; }

        public Action<float> ProgressEvaluated;
        
        private void Awake()
        {
            _rocketLauncher = FindObjectOfType<RocketLauncher>();
            _physicsWordController = FindObjectOfType<PhysicsWordController>();

            _rocketLauncher.RocketExploded += OnRocketExploded;
        }

        private void OnDestroy()
        {
            _rocketLauncher.RocketExploded -= OnRocketExploded;
        }

        private void OnRocketExploded()
        {
            int notStaticObjects;
            int totalObjects;
            
            if (_physicsWordController.UseGPU)
            {
               notStaticObjects = _physicsWordController.GPUSpherePhysObjects.Count(x => x.isStatic == 0);
               totalObjects = _physicsWordController.GPUSpherePhysObjects.Length;
            }
            else
            {
               notStaticObjects = _physicsWordController.CPUPhysicsObjects.Count(x => x.IsStatic == false);
               totalObjects = _physicsWordController.CPUPhysicsObjects.Length;
            }

            float progress = (float) notStaticObjects / totalObjects;

            _progress.fillAmount = progress;
            Progress = progress;
            ProgressEvaluated?.Invoke(progress);
        }
    }
}