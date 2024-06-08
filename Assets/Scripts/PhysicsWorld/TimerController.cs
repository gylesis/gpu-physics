using System;
using TMPro;
using UnityEngine;

namespace PhysicsWorld
{
    public class TimerController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _timerText;

        private DateTime _startTime;
        private PhysicsWordController _physicsWordController;

        public bool IsOnHold { get; set; } 
        
        private void Awake()
        {
            _physicsWordController = FindObjectOfType<PhysicsWordController>();
            _physicsWordController.RestartGame += OnGameRestarted;
            _startTime = DateTime.Now;
        }

        private void OnDestroy()
        {
            _physicsWordController.RestartGame -= OnGameRestarted;
        }

        private void OnGameRestarted()
        {
            _startTime = DateTime.Now;
            IsOnHold = false;
        }

        private void Update()
        {
            if(IsOnHold) return;
            
            DateTime now = DateTime.Now;

            TimeSpan timeSpan = now - _startTime;

            string formattedTime = string.Format("{0:D2}:{1:D2}:{2:D3}",
                timeSpan.Minutes,
                timeSpan.Seconds,
                timeSpan.Milliseconds);

            _timerText.text = formattedTime;
        }
    }
}