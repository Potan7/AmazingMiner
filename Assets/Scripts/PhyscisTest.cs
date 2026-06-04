using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

public class PhyscisTest : MonoBehaviour
{
    public PhysicsBodyDefinition bodyDefinition = PhysicsBodyDefinition.defaultDefinition;
    public PhysicsShapeDefinition shapeDefinition = PhysicsShapeDefinition.defaultDefinition;
    public CapsuleGeometry capsuleGeometry;

    void Update()
    {
        PhysicsWorld world = PhysicsWorld.defaultWorld;
        PhysicsBody myObject = world.CreateBody(bodyDefinition);
        myObject.CreateShape(capsuleGeometry, shapeDefinition);
        myObject.position += new Vector2(1, 1);
    }
}
