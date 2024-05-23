using UnityEngine;

namespace DefaultNamespace.Second
{
    public class Drawing : MonoBehaviour
    {
        [SerializeField] private ComputeShader _drawComputeShader;
        [SerializeField] private RenderTexture _renderTexture;
        [SerializeField] private Camera _camera;

        [SerializeField] private int _textureSizeX;
        [SerializeField] private int _textureSizeY;
        
        private Texture2D _texture2D;

        private void Awake()
        {
            _renderTexture = new RenderTexture(_textureSizeX, _textureSizeY, 32);
            _renderTexture.Create();

            _texture2D = new Texture2D(_textureSizeX, _textureSizeY);
            
            
            
            
            
        }

        private void Update()
        {
            
        }

        private void Draw()
        {
            Color color = Color.Lerp(Color.black, Color.white, Mathf.PingPong(Time.time, 1));
            
            for (int y = 0; y < _textureSizeY; y++)
            {
                for (int x = 0; x < _textureSizeX; x++)
                {
                    _texture2D.SetPixel(x, y, color);
                }   
            }
            
            _texture2D.Apply();
        }
    }
}