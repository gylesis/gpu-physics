﻿using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

namespace PhysicsWorld
{
    public class PhysicsWordController : MonoBehaviour
    {
        [SerializeField] private PlayerController _playerController;

        [SerializeField] private Mesh _drawnMesh;
        [SerializeField] private float _meshVertsOffest = 2;

        [SerializeField] private bool _useGPU;
        [SerializeField] private bool _useCPUMultithreading;
    
        [SerializeField] private Material _sphereMaterial;
        [SerializeField] private Mesh _mesh;

        [Range(0f, 1f)] [SerializeField] private float _collisionVelocityConsumptionFactor = 0.5f;
        [SerializeField] private float _physicsSimulationDelta = 0.005f;

        [SerializeField] private Vector3 _gravity = new Vector3(0, -9.81f, 0);
        [SerializeField] private float _gravityScale = 1f;
        [SerializeField] private BoxCollider _bounds;

        [SerializeField] private float _sphereRadius = 0.5f;
        [SerializeField] private float _forcePower = 5;
        [SerializeField] private float _rocketDetectRadius = 2;

        float3 _explosionCenter;

        public bool IsRocketExploded { get; private set; }

        public event Action RestartGame;

        private NativeArray<Matrix4x4> _matrices;
        private RenderParams _rp;

        public GPUSpherePhysObj[] GPUSpherePhysObjects => _GPUSpherePhysObjects;
        public NativeArray<CPUSpherePhysObj> CPUPhysicsObjects => _CPUPhysicsObjects;

        public bool UseGPU => _useGPU;
        public float ExplosionRadius => _explosionRadius;
        public float3 BoundsCenter => _bounds.center;
        public float3 Bounds => _bounds.bounds.extents;
        public float3 TotalGravity => _gravity * _gravityScale;

        [Header("CPU")] [SerializeField] private int _spheresCount = 100;

        private NativeArray<CPUSpherePhysObj> _CPUPhysicsObjects;

        [Header("GPU")] [SerializeField] private ComputeShader _computeShader;

        private uint[] _rocketArgs = new uint[]
        {
            0, // rocket state
        };

        private ComputeBuffer _spheresBuffer;
        private ComputeBuffer _rocketArgsBuffer;

        private int _kernel;
        private GPUSpherePhysObj[] _GPUSpherePhysObjects;

        bool _applyExplosion = false;

        [SerializeField] private float _explosionForcePower = 40;
        [SerializeField] private float _explosionRadius = 5;

        private float _explosionActiveTimer = 0;
        private Vector3[] _meshVertices;
        private SphereJob _sphereJob;
        private Action _onRocketExploded;
        private float3 _rocketPos;
        private JobHandle _jobHandle;

        private uint _threadGroupSizeX = 512;
        private uint _threadGroupSizeY = 1;
        
        private void Awake()
        {
            _meshVertices = _drawnMesh.vertices;
            _spheresCount = _meshVertices.Length;

            _matrices = new NativeArray<Matrix4x4>(_spheresCount, Allocator.Persistent);
            _rp = new RenderParams(_sphereMaterial);
            
            if (_useGPU)
            {
                SetupGPU();
            }
            else
            {
                SetupCPU();
            }
            
            RestartGame?.Invoke();
        }

        private void SetupCPU()
        {
            _CPUPhysicsObjects = new NativeArray<CPUSpherePhysObj>(_spheresCount, Allocator.Persistent);

             _sphereJob = new SphereJob();
            
            for (int i = 0; i < _spheresCount; i++)
            {
                Vector3 spawnPos = _meshVertices[i] * _meshVertsOffest;
                spawnPos.z *= -1;

                Vector3 scale = Vector3.one * (_sphereRadius * 2);

                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, scale);

                CPUSpherePhysObj physObj = new CPUSpherePhysObj();

                physObj.Position = spawnPos;
                physObj.Velocity = Random.insideUnitSphere * _forcePower;
                physObj.IsStatic = true;

                _CPUPhysicsObjects[i] = physObj;
            }
            
            if (_useCPUMultithreading)
            {
                _sphereJob.bounds = Bounds;
                _sphereJob.center = BoundsCenter;
                _sphereJob.gravity = TotalGravity;
                _sphereJob.sphereRadius = _sphereRadius;
                _sphereJob.InputPhysObjects = new NativeArray<CPUSpherePhysObj>(_CPUPhysicsObjects, Allocator.Persistent);
            }
        }

        private void SetupGPU(bool init = true)
        {
            _kernel = _computeShader.FindKernel("PhysicsCalc");

            if (init)
            {
                _GPUSpherePhysObjects = new GPUSpherePhysObj[_spheresCount];
            }

            for (int i = 0; i < _spheresCount; i++)
            {
                Vector3 spawnPos = _meshVertices[i] * _meshVertsOffest;
                spawnPos.z *= -1;
                Vector3 scale = Vector3.one * (_sphereRadius * 2);

                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, scale);

                GPUSpherePhysObj physObj = new GPUSpherePhysObj();

                physObj.position = spawnPos;
                //physObj.velocity = Random.insideUnitSphere * _forcePower;
                physObj.isStatic = 1;

                _GPUSpherePhysObjects[i] = physObj;
            }

            UpdateShader();
            DispatchShader();
        }

        private void UpdateShader()
        {
            _spheresBuffer?.Dispose();
            _rocketArgsBuffer?.Dispose();

            int objectSize = Marshal.SizeOf(typeof(GPUSpherePhysObj));
            _spheresBuffer = new ComputeBuffer(_spheresCount, objectSize);
            _rocketArgsBuffer = new ComputeBuffer(_rocketArgs.Length, Marshal.SizeOf(typeof(uint)));

            _computeShader.SetFloat("explosionRadius", _explosionRadius);
            _computeShader.SetFloat("explosionForce", _explosionForcePower);
            _computeShader.SetVector("center", new Vector4(BoundsCenter.x, BoundsCenter.y, BoundsCenter.z, 0));
            _computeShader.SetVector("bounds", new Vector4(Bounds.x, Bounds.y, Bounds.z, 0));
            _computeShader.SetVector("gravity", new Vector4(TotalGravity.x, TotalGravity.y, TotalGravity.z, 0));
            _computeShader.SetFloat("objectsCount", _spheresCount);
            _computeShader.SetFloat("sphereRadius", _sphereRadius);
            _computeShader.SetFloat("rocketDetectRadius", _rocketDetectRadius);

            _spheresBuffer.SetData(_GPUSpherePhysObjects);

            _rocketArgsBuffer.SetData(_rocketArgs);

            _computeShader.SetBuffer(_kernel, "physObjects", _spheresBuffer);
            _computeShader.SetBuffer(_kernel, "rocketArgs", _rocketArgsBuffer);
        }

        public void RebuildModel()
        {   
            RestartGame?.Invoke();
            SetupGPU(false);        
        }

        private void Update()
        {
            if (_useGPU)
            {
                Profiler.BeginSample("Read matrices");

                for (var i = 0; i < _GPUSpherePhysObjects.Length; i++)
                {
                    GPUSpherePhysObj physObj = _GPUSpherePhysObjects[i];

                    if (float.IsNaN(physObj.position.x) || float.IsNaN(physObj.position.y) ||
                        float.IsNaN(physObj.position.z)) continue;

                    Matrix4x4 mat = _matrices[i];

                    mat.m03 = physObj.position.x;
                    mat.m13 = physObj.position.y;
                    mat.m23 = physObj.position.z;

                    _matrices[i] = mat;
                }

                Profiler.EndSample();
            }
            else
            {
                Profiler.BeginSample("Read matrices");

                if (_useCPUMultithreading)
                {
                    if (_jobHandle.IsCompleted)
                    {
                        _CPUPhysicsObjects = _sphereJob.InputPhysObjects;
                    }
                }
                
                
                for (var i = 0; i < _CPUPhysicsObjects.Length; i++)
                {
                    CPUSpherePhysObj cpuSpherePhysObj = _CPUPhysicsObjects[i];

                    Matrix4x4 mat = _matrices[i];

                    if (float.IsNaN(cpuSpherePhysObj.Position.x) 
                        || float.IsNaN(cpuSpherePhysObj.Position.y)
                        || float.IsNaN(cpuSpherePhysObj.Position.z) )
                    {
                        continue;
                    }
                        
                    mat.m03 = cpuSpherePhysObj.Position.x;
                    mat.m13 = cpuSpherePhysObj.Position.y;
                    mat.m23 = cpuSpherePhysObj.Position.z;

                    _matrices[i] = mat;
                }
                

                Profiler.EndSample();
            }

            UpdateMesh();
        }

        private void FixedUpdate()
        {
            if (_useGPU)
            {
                _computeShader.SetFloat("dt", _physicsSimulationDelta);
                _computeShader.SetVector("rocketPosition", new Vector4(_rocketPos.x,_rocketPos.y,_rocketPos.z,0));

                DispatchShader();

                _spheresBuffer.GetData(_GPUSpherePhysObjects);
                _rocketArgsBuffer.GetData(_rocketArgs);

                CheckRocketArgs();
            }
            else
            {
                if (_useCPUMultithreading)
                {
                    _sphereJob.dt = _physicsSimulationDelta;
                    _sphereJob.rocketPosition = _rocketPos;
                    _sphereJob.rocketDetectRadius = _rocketDetectRadius;
                    _jobHandle = _sphereJob.Schedule(_spheresCount,  256);
                    _jobHandle.Complete();
                }
                else
                {
                    MoveObjects(_physicsSimulationDelta);
                }
            }
        }

        private void MoveObjects(float dt)
        {
            for (var i = 0; i < _CPUPhysicsObjects.Length; i++)
            {
                CPUSpherePhysObj physObj = _CPUPhysicsObjects[i];

                if (physObj.IsStatic == false)
                {
                    physObj.Velocity += TotalGravity * dt;
                    physObj.Position += physObj.Velocity * dt;
                }

                ApplyConstraints(ref physObj);

                for (int j = 0; j < _CPUPhysicsObjects.Length; j++)
                {
                    if (i == j) continue;

                    CPUSpherePhysObj otherPhysObj = _CPUPhysicsObjects[j];

                    if (otherPhysObj.IsStatic) continue;

                    Resolve(ref physObj, ref otherPhysObj);
                }
                
                CheckForExplosion(ref physObj);
                
                _CPUPhysicsObjects[i] = physObj;
            }

            void ApplyConstraints(ref CPUSpherePhysObj physicsObject)
            {
                float consumptionFactor = _collisionVelocityConsumptionFactor;

                // Проверка и корректировка по X
                if (physicsObject.Position.x < BoundsCenter.x - Bounds.x)
                {
                    physicsObject.Position.x = BoundsCenter.x - Bounds.x;
                    physicsObject.Velocity.x *= -consumptionFactor;
                }
                else if (physicsObject.Position.x > BoundsCenter.x + Bounds.x)
                {
                    physicsObject.Position.x = BoundsCenter.x + Bounds.x;
                    physicsObject.Velocity.x *= -consumptionFactor;
                }

                // Проверка и корректировка по Y
                if (physicsObject.Position.y < BoundsCenter.y - Bounds.y)
                {
                    physicsObject.Position.y = BoundsCenter.y - Bounds.y;
                    physicsObject.Velocity.y *= -consumptionFactor;
                }
                else if (physicsObject.Position.y > BoundsCenter.y + Bounds.y)
                {
                    physicsObject.Position.y = BoundsCenter.y + Bounds.y;
                    physicsObject.Velocity.y *= -consumptionFactor;
                }

                // Проверка и корректировка по Z
                if (physicsObject.Position.z < BoundsCenter.z - Bounds.z)
                {
                    physicsObject.Position.z = BoundsCenter.z - Bounds.z;
                    physicsObject.Velocity.z *= -consumptionFactor;
                }
                else if (physicsObject.Position.z > BoundsCenter.z + Bounds.z)
                {
                    physicsObject.Position.z = BoundsCenter.z + Bounds.z;
                    physicsObject.Velocity.z *= -consumptionFactor;
                }
            }
        }

        private void CheckRocketArgs()
        {
            for (var i = 0; i < _rocketArgs.Length; i++)
            {
                uint rocketArg = _rocketArgs[i];

                switch (i)
                {
                    case 0:

                        if (rocketArg == 2) // exploded
                        {
                            _onRocketExploded?.Invoke();
                            _rocketArgs[i] = 0;

                            _rocketArgsBuffer.SetData(_rocketArgs);
                        }


                        break;

                    case 1: // is rocket 

                        if (rocketArg == 1)
                        {
                            // exploded
                        }

                        break;
                }
            }
        }


        public void LaunchRocket(Action onRocketExploded)
        {
            _onRocketExploded = onRocketExploded;

            _rocketArgs[0] = 1;

            if (_useGPU)
            {
                _rocketArgsBuffer.SetData(_rocketArgs);
            }
        }

        public void SetRocketPos(Vector3 rocketPos)
        {
            _rocketPos = rocketPos;
        }

        private void CheckForExplosion(ref CPUSpherePhysObj physObj)
        {
            if(physObj.IsStatic == false) return;
    
            float3 dir = physObj.Position - _rocketPos;
            float dist = math.length(dir);

            if (dist < _rocketDetectRadius)
            {
                physObj.IsStatic = false;

                dir =  math.normalize(dir);
                physObj.Velocity += dir * _explosionForcePower * (1.0f - dist / _explosionRadius);
            }
        }

        private void DispatchShader()
        {
            int dispatchGroup = Mathf.CeilToInt((int)(_spheresCount / _threadGroupSizeX));
            _computeShader.Dispatch(_kernel, dispatchGroup, 1, 1);
        }

        private void UpdateMesh()
        {
            Graphics.RenderMeshInstanced(_rp, _mesh, 0, _matrices);
        }

        private void Resolve(ref CPUSpherePhysObj firstPhysObj, ref CPUSpherePhysObj secondPhysObj)
        {
            Vector3 dir = firstPhysObj.Position - secondPhysObj.Position;
            float dist = dir.magnitude;

            float radius = _sphereRadius + _sphereRadius;

            if (dist < radius)
            {
                float3 normDir = dir.normalized;
                float penetrationDepth = radius - dist;
                float3 correction = normDir * (penetrationDepth / 2f);

                if (firstPhysObj.IsStatic == false)
                {
                    firstPhysObj.Position += correction;
                    firstPhysObj.Velocity -= normDir * math.dot(firstPhysObj.Velocity, normDir);
                }

                if (secondPhysObj.IsStatic == false)
                {
                    secondPhysObj.Position -= correction;
                    secondPhysObj.Velocity -= normDir * math.dot(secondPhysObj.Velocity, normDir);
                }
            }
        }

        private void OnDestroy()
        {
            _spheresBuffer?.Dispose();
            _matrices.Dispose();
            _rocketArgsBuffer?.Dispose();
            _CPUPhysicsObjects.Dispose();
        }
    }

    public struct CPUSpherePhysObj
    {
        public float3 Position;
        public float3 Velocity;
        public bool IsStatic;
    }

    public struct GPUSpherePhysObj
    {
        public float3 position;
        public float3 velocity;
        public uint isStatic;
    }
}