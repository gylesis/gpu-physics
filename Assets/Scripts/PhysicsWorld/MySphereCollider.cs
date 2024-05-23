using System;

namespace PhysicsWorld
{
    [Serializable]
    public class MySphereCollider : MyCollider
    {
        public float Radius;

        public MySphereCollider(float radius)
        {
            Radius = radius;
        }
        
        public override ColliderType ColliderType => ColliderType.Sphere;
    }
}