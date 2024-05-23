using UnityEngine;

namespace DefaultNamespace
{
    public class Test : MonoBehaviour
    {
        [SerializeField] private int _count;

        [SerializeField] private int _cooldown = 10;
        
        private void Update()
        {
            if (Time.frameCount % _cooldown == 0)
            {
                int sum;
                for (int i = 0; i < _count; i++)
                {
                    sum = 2 + 3;
                }
            }
        }
    }
}