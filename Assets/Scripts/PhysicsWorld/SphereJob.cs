using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PhysicsWorld
{
    public struct SphereJob : IJobParallelFor
    {
        public NativeArray<CPUSpherePhysObj> InputPhysObjects;

        public float dt;
        public float3 bounds;
        public float3 center;
        public float3 gravity;
        public float sphereRadius;

        public float3 rocketPosition;
        public float rocketDetectRadius;
        
        public void Execute(int i)
        {
            CPUSpherePhysObj firstPhysObj = InputPhysObjects[i];

            Move(ref firstPhysObj);
            ApplyBounds(ref firstPhysObj);

            for (int j = i; j < 255; j++)
            {
                if (i == j) continue;

                CPUSpherePhysObj otherPhysObj = InputPhysObjects[j];

                if (otherPhysObj.IsStatic) continue;

                ResolveCollisions(ref firstPhysObj, ref otherPhysObj);

                InputPhysObjects[j] = firstPhysObj;
            }

            ResolveRocketExplosion(ref firstPhysObj);

            InputPhysObjects[i] = firstPhysObj;
        }

        public float explosionRadius;
        public float explosionForce;

        private void Move(ref CPUSpherePhysObj physObj)
        {
            if (physObj.IsStatic == false)
            {
                physObj.Velocity += gravity * dt;
                physObj.Position += physObj.Velocity * dt;
            }
        }

        void ApplyBounds(ref CPUSpherePhysObj obj)
        {
            float consumptionFactor = 0.5f;

            // Проверка и корректировка по X
            if (obj.Position.x < center.x - bounds.x)
            {
                obj.Position.x = center.x - bounds.x;
                obj.Velocity.x *= -consumptionFactor;
            }
            else if (obj.Position.x > center.x + bounds.x)
            {
                obj.Position.x = center.x + bounds.x;
                obj.Velocity.x *= -consumptionFactor;
            }

            // Проверка и корректировка по Y
            if (obj.Position.y < center.y - bounds.y)
            {
                obj.Position.y = center.y - bounds.y;
                obj.Velocity.y *= -consumptionFactor;
            }
            else if (obj.Position.y > center.y + bounds.y)
            {
                obj.Position.y = center.y + bounds.y;
                obj.Velocity.y *= -consumptionFactor;
            }

            // Проверка и корректировка по Z
            if (obj.Position.z < center.z - bounds.z)
            {
                obj.Position.z = center.z - bounds.z;
                obj.Velocity.z *= -consumptionFactor;
            }
            else if (obj.Position.z > center.z + bounds.z)
            {
                obj.Position.z = center.z + bounds.z;
                obj.Velocity.z *= -consumptionFactor;
            }
        }

        private void ResolveCollisions(ref CPUSpherePhysObj firstPhysObj, ref CPUSpherePhysObj secondPhysObj)
        {
            Vector3 dir = firstPhysObj.Position - secondPhysObj.Position;
            float dist = dir.magnitude;

            float radius = sphereRadius + sphereRadius;

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

        private void ResolveRocketExplosion(ref CPUSpherePhysObj physObj)
        {
            if(physObj.IsStatic == false) return;
    
            float3 dir = physObj.Position - rocketPosition;
            float dist = math.length(dir);

            if (dist < rocketDetectRadius)
            {
                physObj.IsStatic = false;

                /*dir =  math.normalize(dir);
                physObj.Velocity += dir * explosionForce * (1.0f - dist / explosionRadius);*/
            }
        }
    }
}