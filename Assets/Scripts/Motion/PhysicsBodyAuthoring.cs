using Unity.Entities;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using Unity.Mathematics;

namespace CoreDriller.Motion
{
    public enum ColliderShapeType
    {
        Circle,
        Capsule
    }

    class PhysicsBodyAuthoring : MonoBehaviour
    {
        [Header("Physics Definitions")]
        public PhysicsBodyDefinition bodyDefinition;
        public PhysicsShapeDefinition shapeDefinition;

        [Header("Collider Settings")]
        public ColliderShapeType ColliderType;
        public CircleGeometry circleGeometry;
        public CapsuleGeometry capsuleGeometry;

        void Reset()
        {
            bodyDefinition = PhysicsBodyDefinition.defaultDefinition;
            shapeDefinition = PhysicsShapeDefinition.defaultDefinition;

            circleGeometry = CircleGeometry.Create(0.5f); // 기본 반지름 0.5f로 설정
            capsuleGeometry = CapsuleGeometry.Create(new Vector2(0, 0.5f), new Vector2(0, -0.5f), 0.5f); // 기본 길이 1.0f, 반지름 0.5f로 설정
        }

        class PhysicsBodyAuthoringBaker : Baker<PhysicsBodyAuthoring>
        {
            public override void Bake(PhysicsBodyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                PhysicsBodyInformation comp = new PhysicsBodyInformation()
                {
                    BodyDefinition = authoring.bodyDefinition,
                    ShapeDefinition = authoring.shapeDefinition,
                    ColliderType = authoring.ColliderType,
                    InitialVelocity = float2.zero
                };
                switch (authoring.ColliderType)
                {
                    case ColliderShapeType.Circle:
                        comp.CircleGeometry = authoring.circleGeometry;
                        break;
                    case ColliderShapeType.Capsule:
                        comp.CapsuleGeometry = authoring.capsuleGeometry;
                        break;
                };

                AddComponent(entity, comp);
            }
        }
    }

    public partial struct PhysicsBodyInformation : IComponentData
    {
        public PhysicsBodyDefinition BodyDefinition;
        public PhysicsShapeDefinition ShapeDefinition;
        public ColliderShapeType ColliderType;
        public CircleGeometry CircleGeometry;
        public CapsuleGeometry CapsuleGeometry;
        public float2 InitialVelocity;
    }

    public partial struct PhysicsBodyHandle : IComponentData
    {
        public PhysicsBody Body;
    }

}
