
using UnityEngine;

[CreateAssetMenu(fileName ="Dynamics", menuName ="Dynamics/Base")]
public class DynamicProperties : ScriptableObject {
    public LayerMask unavoidableObstacles;
    public LayerMask avoidableObstacles;
    public float radius;
    public float speed;
    public float stopDistance;
    public float height;
}

