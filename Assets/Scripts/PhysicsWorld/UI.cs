using UnityEngine;
using UnityEngine.UI;

namespace PhysicsWorld
{
    public class UI : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;
        private PhysicsWordController _physicsWordController;

        private void Awake()
        {
            _physicsWordController = FindObjectOfType<PhysicsWordController>();

            _restartButton.onClick.AddListener((OnRestartButton));
        }

        private void OnRestartButton()
        {
            _physicsWordController.RebuildModel();
        }
    }
}