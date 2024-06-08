using UnityEngine;
using UnityEngine.UI;

namespace PhysicsWorld
{
    public class UI : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;
        [SerializeField] private Transform _winMenu;
        
        private PhysicsWordController _physicsWordController;
        private ModelProgress _modelProgress;
        private TimerController _timerController;

        private void Awake()
        {
            _physicsWordController = FindObjectOfType<PhysicsWordController>();
            _modelProgress = FindObjectOfType<ModelProgress>();
            _timerController = FindObjectOfType<TimerController>();

            _modelProgress.ProgressEvaluated += OnProgressEvaluated;

            _restartButton.onClick.AddListener((OnRestartButton));
        }

        private void OnProgressEvaluated(float progress)
        {
            if (progress >= 0.95f)
            {
                ShowWinMenu(true);
                _timerController.IsOnHold = true;
            }
            
        }

        private void OnRestartButton()
        {
            _timerController.IsOnHold = false;
            _physicsWordController.RebuildModel();
            ShowWinMenu(false);
        }

        public void ShowWinMenu(bool isOn)
        {
            _winMenu.gameObject.SetActive(isOn);
        }
    }
}