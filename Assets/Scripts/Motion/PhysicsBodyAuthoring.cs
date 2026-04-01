using Unity.Entities;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Motion
{
    class PhysicsBodyAuthoring : MonoBehaviour
    {
        public PhysicsBodyDefinition bodyDefinition;
        public PhysicsShapeDefinition shapeDefinition;
        public CircleGeometry circleGeometry;

        void Reset()
        {
            bodyDefinition = PhysicsBodyDefinition.defaultDefinition;
            shapeDefinition = PhysicsShapeDefinition.defaultDefinition;
            circleGeometry = CircleGeometry.Create(0.5f); // 기본 반지름 0.5f로 설정
        }

        class PhysicsBodyAuthoringBaker : Baker<PhysicsBodyAuthoring>
        {
            public override void Bake(PhysicsBodyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PhysicsBodyInformation()
                {
                    BodyDefinition = authoring.bodyDefinition,
                    ShapeDefinition = authoring.shapeDefinition,
                    CircleGeometry = authoring.circleGeometry
                });
            }
        }
    }

    public partial struct PhysicsBodyInformation : IComponentData
    {
        public PhysicsBodyDefinition BodyDefinition;
        public PhysicsShapeDefinition ShapeDefinition;
        public CircleGeometry CircleGeometry;
    }

    public partial struct PhysicsBodyHandle : IComponentData
    {
        public PhysicsBody Body;
    }

}
