using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace PhysicsWorld
{
    public class PhysicsWordController : MonoBehaviour
    {
        [SerializeField] private bool _useGPU;

        [SerializeField] private Material _sphereMaterial;
        [SerializeField] private Mesh _mesh;
        private NativeArray<Matrix4x4> _matrices;
        private RenderParams _rp;

        [Header("CPU")] [SerializeField] private MoveObject _moveObjectPrefab;

        [FormerlySerializedAs("_colliders")] [SerializeField]
        private List<MoveObject> _moveObjects;

        [SerializeField] private Vector3 _gravity = new Vector3(0, -9.81f, 0);
        [SerializeField] private float _gravityScale = 1f;
        [SerializeField] private BoxCollider _bounds;
        [Range(1, 100)] [SerializeField] private float _velocityConsumption = 10;
        [SerializeField] private float _sphereRadius = 0.5f;
        [SerializeField] private float _forcePower = 5;
        [SerializeField] private int _spheresCount = 100;

        private List<PhysicsObject> _physicsObjects = new List<PhysicsObject>();

        [Header("GPU")] [SerializeField] private ComputeShader _computeShader;

        [SerializeField] private Transform _explosionObject;
        
        [SerializeField] private float _physicsSimulationDelta = 0.005f;


        private uint[] _args = { 0, 0, 0, 0, 0 };
        private ComputeBuffer _argsBuffer;

        private uint _threadGroupSizeX = 256;
        private uint _threadGroupSizeY = 1;

        private ComputeBuffer _spheresBuffer;
        private int _kernel;
        private SpherePhysObj[] _spherePhysObjects;
        private AsyncGPUReadbackRequest _gpuRequest;

        bool _applyExplosion = false;
        Vector3 _explosionCenter;

        public float _clickForce = 10.0f;
        public float _clickRadius = 2.0f;

        private float _explosionActiveTimer = 0;

        private void Awake()
        {
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
        }

        private void SetupGPU()
        {
            _kernel = _computeShader.FindKernel("PhysicsCalc");

            int objectSize = Marshal.SizeOf(typeof(SpherePhysObj));
            _spheresBuffer = new ComputeBuffer(_spheresCount, objectSize);

            _computeShader.SetVector("center", _bounds.bounds.center);
            _computeShader.SetVector("bounds", _bounds.bounds.extents);
            _computeShader.SetVector("gravity", _gravity * _gravityScale);
            _computeShader.SetFloat("objectsCount", _spheresCount);

            _spherePhysObjects = new SpherePhysObj[_spheresCount];

            for (int i = 0; i < _spheresCount; i++)
            {
                Vector3 spawnPos = _bounds.center + Random.insideUnitSphere * 2;

                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, Vector3.one * (_sphereRadius * 2));

                SpherePhysObj physObj = new SpherePhysObj();

                physObj.position = spawnPos;

                float3 direction = Random.insideUnitSphere;

                physObj.velocity = direction * _forcePower;
                physObj.isStatic = 0;
                physObj.radius = _sphereRadius;
                physObj.gridIndex = 0;
                physObj.mat = _matrices[i];

                _spherePhysObjects[i] = physObj;
            }

            _args[0] = _mesh.GetIndexCount(0);
            _args[1] = (uint)_spheresCount;
            _args[2] = _mesh.GetIndexStart(0);
            _args[3] = _mesh.GetBaseVertex(0);
            
            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(_args);
            
            _spheresBuffer.SetData(_spherePhysObjects);
            _computeShader.SetBuffer(_kernel, "physObjects", _spheresBuffer);
            _sphereMaterial.SetBuffer("physObjects", _spheresBuffer);
            
            DispatchShader();
        }

        private void SetupCPU()
        {
            for (int i = 0; i < _spheresCount; i++)
            {
                Vector3 spawnPos = _bounds.center + Random.insideUnitSphere * 2;
                //MoveObject moveObject = Instantiate(_moveObjectPrefab, spawnPos, Quaternion.identity);

                Vector3 scale = Vector3.one * (_sphereRadius * 2);
                _matrices[i] = Matrix4x4.TRS(spawnPos, Quaternion.identity, scale);

                // _moveObjects.Add(moveObject);

                SetupPhysicObject(spawnPos);
            }
        }

        private void SetupPhysicObject(Vector3 pos)
        {
            PhysicsObject physicsObject = new PhysicsObject();

            MyRigidbody rigidbody = new MyRigidbody();

            rigidbody.PositionCurrent = pos;
            rigidbody.PositionPrev = pos;
            rigidbody.Acceleration = Vector3.zero;
            rigidbody.IsStatic = false;

            physicsObject.Rigidbody = rigidbody;
            MyCollider sphereCollider = new MySphereCollider(_sphereRadius);

            physicsObject.Collider = sphereCollider;

            _physicsObjects.Add(physicsObject);
        }

        private void MoveObjects()
        {
            for (var i = 0; i < _physicsObjects.Count; i++)
            {
                PhysicsObject physicsObject = _physicsObjects[i];

                if (physicsObject.Rigidbody.IsStatic) continue;

                // physicsObject.Rigidbody.acceleration += physicsObject.Rigidbody.Force + _gravity * _gravityScale;   // Apply gravity
                physicsObject.Rigidbody.Acceleration += _gravity * _gravityScale; // Apply gravity

                ApplyConstraints(physicsObject);

                Vector3 velocity = physicsObject.Rigidbody.PositionCurrent - physicsObject.Rigidbody.PositionPrev;
                physicsObject.Rigidbody.PositionPrev = physicsObject.Rigidbody.PositionCurrent;

                physicsObject.Rigidbody.PositionCurrent += velocity +
                                                           physicsObject.Rigidbody.Acceleration * Time.fixedDeltaTime *
                                                           Time.fixedDeltaTime; // Update pos
                physicsObject.Rigidbody.Acceleration = Vector3.zero;
                //physicsObject.Rigidbody.Force = Vector3.zero;
            }

            void ApplyConstraints(PhysicsObject physicsObject)
            {
                Vector3 position = physicsObject.Rigidbody.PositionCurrent;

                if (_bounds.bounds.Contains(position) == false)
                {
                    Vector3 closestPointOnBounds =
                        _bounds.ClosestPointOnBounds(physicsObject.Rigidbody.PositionCurrent);

                    //float distance = (contactPoint - position).magnitude;

                    //Vector3 movePosition = closestPointOnBounds + (closestPointOnBounds - position).normalized * (_sphereRadius);
                    Vector3 movePosition = closestPointOnBounds;

                    physicsObject.Rigidbody.PositionCurrent = movePosition;

                    // float consumptionFactor = 1 - (_velocityConsumption / 100);

                    //physicsObject.Rigidbody.Velocity = -(physicsObject.Rigidbody.Velocity * consumptionFactor);
                }
            }


            ApplyRigidbody();

            void ApplyRigidbody()
            {
                for (var i = 0; i < _moveObjects.Count; i++)
                {
                    MoveObject moveObject = _moveObjects[i];
                    PhysicsObject physicsObject = _physicsObjects[i];

                    moveObject.transform.position = physicsObject.Rigidbody.PositionCurrent;
                }
            }
        }

        private void Update()
        {
            if (_useGPU)
            {
                Profiler.BeginSample("Read matrices");
              
                /*for (var i = 0; i < _spherePhysObjects.Length; i++)
                {
                    SpherePhysObj physObj = _spherePhysObjects[i];

                    if (float.IsNaN(physObj.position.x) || float.IsNaN(physObj.position.y) ||
                        float.IsNaN(physObj.position.z)) continue;

                    Matrix4x4 mat = _matrices[i];

                    mat.m03 = physObj.position.x;
                    mat.m13 = physObj.position.y;
                    mat.m23 = physObj.position.z;

                    _matrices[i] = mat;
                }*/

                Profiler.EndSample();
                
                if (_applyExplosion)
                {
                    _applyExplosion = false;
                    _computeShader.SetBool("applyExplosion", false);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    Vector3 hitPoint = _explosionObject.position;

                    hitPoint.y = _bounds.bounds.min.y;
                    
                    _explosionCenter = hitPoint;
                    _applyExplosion = true;
                    _computeShader.SetVector("explosionCenter", _explosionCenter);
                    _computeShader.SetFloat("explosionRadius", _clickRadius);
                    _computeShader.SetFloat("explosionForce", _clickForce);
                    _computeShader.SetBool("applyExplosion", true);
                    
                    DispatchShader();
                }
            }
            else
            {
                for (var i = 0; i < _physicsObjects.Count; i++)
                {
                    PhysicsObject physicsObject = _physicsObjects[i];

                    Matrix4x4 mat = _matrices[i];

                    mat.m03 = physicsObject.Rigidbody.PositionCurrent.x;
                    mat.m13 = physicsObject.Rigidbody.PositionCurrent.y;
                    mat.m23 = physicsObject.Rigidbody.PositionCurrent.z;

                    _matrices[i] = mat;
                }
               
            }

            UpdateMesh();
        }

        private void FixedUpdate()
        {
            if (_useGPU)
            {
                _computeShader.SetFloat("dt", _physicsSimulationDelta);

                DispatchShader();
                
                Profiler.BeginSample("Read data from buffer");
               // _spheresBuffer.GetData(_spherePhysObjects);
                Profiler.EndSample();
            }
            else
            {
                MoveObjects();
                ResolveCollisions();
            }
        }

        private void DispatchShader()
        {
            int dispatchGroup = Mathf.CeilToInt((int)(_spheresCount / _threadGroupSizeX));
            _computeShader.Dispatch(_kernel, dispatchGroup, 1, 1);
        }

        private void UpdateMesh()
        {
            //Graphics.RenderMeshInstanced(_rp, _mesh, 0, _matrices);
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _sphereMaterial, _bounds.bounds, _argsBuffer);
        }

        private void ResolveCollisions()
        {
            for (var i = 0; i < _physicsObjects.Count; i++)
            {
                PhysicsObject physicsObject1 = _physicsObjects[i];

                if (physicsObject1.CalculateCollision == false) continue;
                if (physicsObject1.Rigidbody.IsStatic) continue;

                MyCollider collider1 = physicsObject1.Collider;

                for (int j = 0; j < _physicsObjects.Count; j++)
                {
                    if (i == j) continue;

                    PhysicsObject physicsObject2 = _physicsObjects[j];

                    if (physicsObject2.CalculateCollision == false) continue;

                    MyCollider collider2 = physicsObject2.Collider;

                    if (collider1.ColliderType == ColliderType.Sphere && collider2.ColliderType == ColliderType.Sphere)
                    {
                        MySphereCollider sphere1 = (MySphereCollider)collider1;
                        MySphereCollider sphere2 = (MySphereCollider)collider2;

                        Vector3 direction = physicsObject1.Rigidbody.PositionCurrent -
                                            physicsObject2.Rigidbody.PositionCurrent;
                        float distance = direction.magnitude;
                        float totalRadius = sphere1.Radius + sphere2.Radius;

                        if (distance < totalRadius)
                        {
                            float displacement = totalRadius - distance;
                            float halfDisplacement = displacement / 2;

                            if (physicsObject2.Rigidbody.IsStatic == false)
                            {
                                physicsObject2.Rigidbody.PositionCurrent += -direction.normalized * halfDisplacement;
                            }

                            physicsObject1.Rigidbody.PositionCurrent += direction.normalized * halfDisplacement;

                            // float consumptionFactor = 1 - (_velocityConsumption / 100);
                            //  physicsObject1.Rigidbody.Velocity = -(physicsObject1.Rigidbody.Velocity * consumptionFactor);
                            //  physicsObject2.Rigidbody.Velocity = -(physicsObject2.Rigidbody.Velocity * consumptionFactor);

                            continue;
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            _spheresBuffer?.Dispose();
            _matrices.Dispose();
            _argsBuffer.Release();
        }
    }

    public class PhysicsObject
    {
        public MyRigidbody Rigidbody;
        public MyCollider Collider;
        public bool CalculateCollision => Collider != null;
    }

    public class MyRigidbody
    {
        public Vector3 PositionCurrent;
        public Vector3 PositionPrev;

        public Vector3 Acceleration;

        //public Vector3 Force;
        public bool IsStatic;
    }

    internal struct SpherePhysObj
    {
        public float4x4 mat;
        public float3 position;
        public float3 velocity;
        public float radius;
        public uint isStatic;
        public uint gridIndex;
    }
}