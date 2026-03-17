using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

public class PhyscisTest : MonoBehaviour
{
    public PhysicsBodyDefinition bodyDefinition = PhysicsBodyDefinition.defaultDefinition;
    public PhysicsShapeDefinition shapeDefinition = PhysicsShapeDefinition.defaultDefinition;
    public CapsuleGeometry capsuleGeometry;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Update()
    {
        PhysicsWorld world = PhysicsWorld.defaultWorld;
        PhysicsBody myObject = world.CreateBody(bodyDefinition);
        myObject.CreateShape(capsuleGeometry, shapeDefinition);
    }


}
