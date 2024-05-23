using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace DefaultNamespace
{
    public class HardMoving : MonoBehaviour
    {
        [SerializeField] private int _numbersAmount = 100;
        [SerializeField] private TestBall _ballPrefab;

        [SerializeField] private BoxCollider _collider;

        [SerializeField] private bool _useCPUMultithreading;
        [SerializeField] private bool _useGPU;

        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;
        [SerializeField] private float _timeScale = 0.5f;
        [SerializeField] private int _getDataFrameCooldown = 10;

        private List<TestBall> _balls = new List<TestBall>();

        private NativeArray<Vector3> _position;
        private NativeArray<Vector3> _velocities;
        
        private NativeArray<Matrix4x4> _matrices;

        private Vector3 BoundsSize => _collider.transform.localScale;

        private TransformAccessArray _transformAccessArray;
        private ComputeBuffer _posesComputeBuffer;
        private ComputeBuffer _directionsComputeBuffer;
        private ComputeBuffer _forcesComputeBuffer; 

        private int _kernel;

        //private NativeArray<SphereObject> _spheres;
        
        private RenderParams _rp;

        private static readonly int Directions = Shader.PropertyToID("_directions");
        private static readonly int Positions = Shader.PropertyToID("_positions");
        private static readonly int Forces = Shader.PropertyToID("_forces");

        private static readonly int Bounds = Shader.PropertyToID("_bounds");
        private static readonly int DeltaTime = Shader.PropertyToID("_deltaTime");

        private uint _threadGroupSizeX = 256;

        private NativeArray<float3> _positions;

        //private Vector3[] _positions;
      
        // private NativeArray<Matrix4x4> _matrices;

        private Vector3[] _directions;
        private float[] _forces;

        private AsyncGPUReadbackRequest _gpuRequest;

        private async void Awake()
        {
            _matrices = new NativeArray<Matrix4x4>(_numbersAmount, Allocator.Persistent);
            _velocities = new NativeArray<Vector3>(_numbersAmount, Allocator.Persistent);   
            _rp = new RenderParams(_material);
            
            
            if (_useGPU == false)
            {
                CreateBallsCPU();
            }
            else
            {
                CreateBallsGPU();
            }
        }

        private void CreateBallsGPU()
        {
            _directions = new Vector3[_numbersAmount];
            _positions = new NativeArray<float3>(_numbersAmount, Allocator.Temp);
            //_positions = new Vector3[_numbersAmount] ;
            _forces = new float[_numbersAmount];

            for (int i = 0; i < _numbersAmount; i++)
            {
                Vector3 spawnPos = Random.insideUnitSphere;

                _positions[i] = spawnPos;
                _directions[i] = Random.insideUnitSphere.normalized;
                _forces[i] = Random.Range(0.01f, 0.5f);

                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, Vector3.one * 0.2f);

                // TestBall ball = Instantiate(_ballPrefab, spawnPos, Quaternion.identity);

                //_balls.Add(ball);
            }

            _kernel = _computeShader.FindKernel("Move");

            //_posesComputeBuffer = new ComputeBuffer(_numbersAmount, 64);
            _posesComputeBuffer = new ComputeBuffer(_numbersAmount, 12);
            _directionsComputeBuffer = new ComputeBuffer(_numbersAmount, 12);
            _forcesComputeBuffer = new ComputeBuffer(_numbersAmount, 4);

            _posesComputeBuffer.SetData(_positions);
            //_posesComputeBuffer.SetData(_matrices);
            _directionsComputeBuffer.SetData(_directions);

            _computeShader.SetBuffer(_kernel, Positions, _posesComputeBuffer);
            //_computeShader.SetBuffer(_kernel, "_matrices", _posesComputeBuffer);
            _computeShader.SetBuffer(_kernel, Directions, _directionsComputeBuffer);

            _computeShader.SetVector(Bounds, BoundsSize);

            _forcesComputeBuffer.SetData(_forces);
            _computeShader.SetBuffer(_kernel, Forces, _forcesComputeBuffer);

            _computeShader.GetKernelThreadGroupSizes(_kernel, out _threadGroupSizeX, out _, out _);

            GPURequest();
        }

        private void CreateBallsCPU()
        {
            _position = new NativeArray<Vector3>(_numbersAmount, Allocator.Persistent);

            for (int i = 0; i < _numbersAmount; i++)
            {
                Vector3 spawnPos = Random.insideUnitSphere;

                // TestBall ball = Instantiate(_ballPrefab, spawnPos, Quaternion.identity);

                //_balls.Add(ball);
                _position[i] = spawnPos;
                _velocities[i] = Random.insideUnitSphere.normalized;
                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, Vector3.one * 0.2f);
            }

            _transformAccessArray = new TransformAccessArray(_balls.Select(x => x.transform).ToArray());
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                for (int i = 0; i < _numbersAmount; i++)
                {
                    Vector3 direction = Random.insideUnitSphere;

                    if (_useGPU == false)
                    {
                        Vector3 velocity = direction;
                        _velocities[i] = velocity;
                    }

                    _directions[i] = direction;
                }

                // _computeShader.SetFloat(Forces, _velocityModifier);
                _directionsComputeBuffer.SetData(_directions);
                //_computeBuffer.SetData(_forces);
            }

            if (_useGPU)
            {
                GPUUpdate();
            }
            else
            {
                CPUUpdate();
            }


            UpdateMesh();
        }

        private void GPURequest()
        {
            _gpuRequest = AsyncGPUReadback.Request(_posesComputeBuffer);
        }

        private void GPUUpdate()
        {
            AsyncGPUReadbackRequest request = _gpuRequest;

            _computeShader.SetFloat(DeltaTime, Time.deltaTime * _timeScale);
            int dispatchGroup = (int)(_numbersAmount / _threadGroupSizeX);

            _computeShader.Dispatch(_kernel, dispatchGroup, 1, 1);

            if (request.done && request.hasError == false)
            {
                _positions = request.GetData<float3>();
                //_matrices = request.GetData<Matrix4x4>();

                Profiler.BeginSample("Apply pos");
                
                /*for (var i = _numbersAmount - 1; i >= 0; i--)
                {
                    Vector3 position = _positions[i];

                    //_balls[i].transform.position = position;

                    Matrix4x4 mat = _matrices[i];

                    mat.m03 = position.x;
                    mat.m13 = position.y;
                    mat.m23 = position.z;

                    _matrices[i] = mat;
                }*/
                
                var job = new AssignJob()
                {
                    positions = _positions,
                    matrices =  _matrices.Reinterpret<float4x4>()
                };

                JobHandle jobHandle = job.Schedule(_numbersAmount, _numbersAmount / 256);
                //JobHandle jobHandle = job.Schedule(_transformAccessArray);

                jobHandle.Complete();

                
               // _matrices = job.matrices;

                Profiler.EndSample();
        
                GPURequest();
            }
            else if (request.hasError)
            {
                GPURequest();
            }
        }

        private void UpdateMesh()
        {
            Graphics.RenderMeshInstanced(_rp, _mesh, 0, _matrices);
        }

        private void CPUUpdate()
        {
            if (_useCPUMultithreading)
            {
                MoveWithThreads();
            }
            else
            {
                MoveUsually();
            }
        }


        private void MoveUsually()
        {
            for (var i = 0; i < _numbersAmount; i++)
            {
                Vector3 pos = _position[i];

                pos += _velocities[i] * (_timeScale * Time.deltaTime);

                if (pos.x > BoundsSize.x || pos.x < -BoundsSize.x || pos.y > BoundsSize.y || pos.y < -BoundsSize.y)
                {
                    pos = Vector3.zero;
                }

                _position[i] = pos;
                _matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.2f);
            }
        }

        private void MoveWithThreads()
        {
            var job = new MoveJob()
            {
                deltaTime = Time.deltaTime * _timeScale,
                position = _position,
                velocity = _velocities,
                matrices = _matrices.Reinterpret<float4x4>(),
                BoundsSize = BoundsSize
            };

            JobHandle jobHandle = job.Schedule(_numbersAmount, _numbersAmount / 256);
            //JobHandle jobHandle = job.Schedule(_transformAccessArray);

            Profiler.BeginSample("Jobs");
            jobHandle.Complete();
            Profiler.EndSample();
        }

        private void OnDestroy()
        {
            _position.Dispose();
            _velocities.Dispose();
            _matrices.Dispose();
            _transformAccessArray.Dispose();
            
            _posesComputeBuffer?.Release();
            _directionsComputeBuffer?.Release();
            _forcesComputeBuffer?.Release();
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(Vector3.zero, _collider.transform.localScale);
        }
    }


    [BurstCompile]
    struct AssignJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float4x4> matrices;

        public void Execute(int i)
        {
            Vector3 position = positions[i];

            Matrix4x4 mat = matrices[i];

            mat.m03 = position.x;
            mat.m13 = position.y;
            mat.m23 = position.z;

            matrices[i] = mat;
        }
    }

    struct MoveJob : IJobParallelFor
    {
        public NativeArray<Vector3> velocity;
        public NativeArray<Vector3> position;
        public NativeArray<float4x4> matrices;

        public float deltaTime;
        public Vector3 BoundsSize;

        public void Execute(int i)
        {
            Vector3 pos = position[i];

            pos += velocity[i] * deltaTime;

            if (pos.x > BoundsSize.x || pos.x < -BoundsSize.x || pos.y > BoundsSize.y || pos.y < -BoundsSize.y)
            {
                pos = Vector3.zero;
            }

            Matrix4x4 mat = matrices[i];

            mat.m03 = pos.x;
            mat.m13 = pos.y;
            mat.m23 = pos.z;

            matrices[i] = mat;
            position[i] = pos;

            // matrices[i] = Matrix4x4.TRS(position[i], Quaternion.identity, Vector3.one * 0.2f);
        }
    }
}