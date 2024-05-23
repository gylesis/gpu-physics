using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace DefaultNamespace.Third
{
    public class HardComputing : MonoBehaviour
    {
        [SerializeField] private ComputeShader _computeShader;
        
        [SerializeField] private int _size;
        [SerializeField] private int _interationFor = 10;
        
        [SerializeField] private bool _useGPU;
        
        private NativeArray<float3> _floatsData;
        
        private ComputeBuffer _computeBuffer;
        private int _kernel;
        private uint _threadGroupSizeX = 256;
        private AsyncGPUReadbackRequest _request;

        private void Awake()
        {
            _floatsData = new NativeArray<float3>(_size, Allocator.Persistent);

            for (int i = 0; i < _size; i++)
            {
                _floatsData[i] = new float3(1,1,1) * Random.Range(1f, 50f); 
            }

            
           // Stopwatch stopwatch = Stopwatch.StartNew()
           // ;

            if (_useGPU)
            {
                SetupGPU();
                //GPUCompute();
            }
            else
            {
                CPUCompute();
            }

           // stopwatch.Stop();
            
            //Debug.Log($"Time passed to proceed {_size} elements. Seconds: {stopwatch.Elapsed.Seconds}, Milliseconds: {stopwatch.Elapsed.Milliseconds}");
        }

        private void SetupGPU()
        {
            _kernel = _computeShader.FindKernel("Move");
            
            _computeBuffer = new ComputeBuffer(_size, 12);
            
            _computeBuffer.SetData(_floatsData);
            _computeShader.SetBuffer(_kernel, "_data", _computeBuffer);
        }
        
        private void Update()
        {
            if (_useGPU)
            {
                GPUCompute();
            }
            else
            {
                CPUCompute();
            }
        }

        private void GPUCompute()
        {
            AsyncGPUReadbackRequest request = _request;
            
            int dispatchGroup = (int)(_size / _threadGroupSizeX);

            _computeShader.Dispatch(_kernel, dispatchGroup, 1, 1);

            if (request.done && request.hasError == false)
            {
                _floatsData = request.GetData<float3>();
                
                _request = AsyncGPUReadback.Request(_computeBuffer);
            }
            else if(request.hasError)
            {
                _request = AsyncGPUReadback.Request(_computeBuffer);
            }
        }
        
        private void CPUCompute()
        {
            var job = new ComputeFloatsJob
            {
                _data = _floatsData,
                _iterations = _interationFor
            };

            JobHandle jobHandle = job.Schedule(_size, 256);
            
            jobHandle.Complete();
        }

        private void OnDestroy()
        {
            _floatsData.Dispose();
            
            _computeBuffer?.Release();
        }
    }


    struct ComputeFloatsJob : IJobParallelFor
    {
        public NativeArray<float3> _data;
        public float _iterations;
        
        public void Execute(int index)
        {
            for (int i = 0; i < _iterations; i++)
            {
                float3 data = _data[index];
                _data[index] = new float3(Mathf.Sqrt(data.x), Mathf.Sqrt(data.y), Mathf.Sqrt(data.z));
            }
        }   
    }
    
}