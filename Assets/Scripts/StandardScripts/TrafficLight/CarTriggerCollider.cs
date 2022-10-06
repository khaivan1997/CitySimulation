using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarTriggerCollider : MonoBehaviour
{
    IntersectionRoad r;
    // Start is called before the first frame update
    void Start()
    {
        Vector3 size = GetComponentInParent<BoxCollider>().size;
        Vector3 thisScale = Vector3.forward;
        thisScale.x = size.x;
        thisScale.y = size.z;
        transform.localScale = thisScale;
        r = GetComponentInParent<IntersectionRoad>();
    }

    // Update is called once per frame
    private void OnTriggerEnter(Collider other)
    {
        r.OnTriggerVehicle(other);
    }
}
