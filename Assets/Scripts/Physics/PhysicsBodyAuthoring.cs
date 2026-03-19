using Unity.Entities;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace CoreDriller.Physics
{
    class PhysicsBodyAuthoring : MonoBehaviour
    {
        public PhysicsBodyDefinition bodyDefinition;
        public PhysicsShapeDefinition shapeDefinition;
        public CircleGeometry circleGeometry;

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