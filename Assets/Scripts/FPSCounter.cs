using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public class FPSCounter : MonoBehaviour
    {
        private readonly Queue<float> _fpsValues = new Queue<float>();

        private float _averageFPS;
        
        private void LateUpdate()
        {
            if (_fpsValues.Count > 60)
            {
                _fpsValues.Dequeue();
            }

            float currentFPs = 1f / Time.deltaTime;

            _fpsValues.Enqueue(currentFPs);

            _averageFPS = 0;
            
            foreach (float fpsValue in _fpsValues)
            {
                _averageFPS += fpsValue;
            }

            _averageFPS /= _fpsValues.Count;
        }

        private void OnGUI()
        {
            string text = $"FPS:{(int)_averageFPS}";

            var guiStyle = new GUIStyle
            {
                imagePosition = ImagePosition.ImageLeft,
                fontSize = 32,
                fontStyle = FontStyle.Normal,
            };

            GUI.Label(new Rect(50,50,100,100), text, guiStyle);
        }
    }
}