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
            int notStaticObjects = _physicsWordController.GPUSpherePhysObjects.Count(x => x.isStatic == 0);
            int totalObjects = _physicsWordController.GPUSpherePhysObjects.Length;

            float progress = (float) notStaticObjects / totalObjects;

            _progress.fillAmount = progress;
        }
    }
}