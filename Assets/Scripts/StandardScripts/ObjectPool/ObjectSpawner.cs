using System.Collections.Generic;
using UnityEngine;
using TurnTheGameOn.SimpleTrafficSystem;
using System;
using Random = UnityEngine.Random;

public class ObjectSpawner : MonoBehaviour
{
    [SerializeField]
    protected float _cooldownPedestrian;
    [SerializeField]
    protected float _cooldownVehicle;

    public float CooldownPedstrian{
        get => (1 / _cooldownPedestrian);
        set
        {
            int val = (int)Math.Round(value);
            _cooldownPedestrian = 1 / value;
            spawnRateText.SetText(val.ToString());
        }
     }

    public TMPro.TextMeshProUGUI spawnRateText;


    private float currentPedestrian;
    private float currentVehicle;
    public ObjectPool pool;

    public string[] pedestrianTags = { "Pedestrian"};
    public string[] vehicleTags = { "Vehicle" };

    [System.Serializable]
    public struct TupleVector
    {
        public Vector3 from;
        public Vector3 to;
    }
    public List<TupleVector> pedestrianPoints;
    [System.Serializable]
    public struct TupleTrafficWaypoint
    {
        public AITrafficWaypoint from;
        public AITrafficWaypoint to;
    }
    public List<TupleTrafficWaypoint> CarWayPoints;

    private Collider[] colliders;

    private void OnEnable()
    {
        currentPedestrian = 0;
        currentVehicle = 0;
        colliders = new Collider[3];
        spawnRateText.SetText(((int)Math.Round(CooldownPedstrian)).ToString());
    }

    public void Update()
    {
        currentPedestrian -= Time.deltaTime;
        currentVehicle -= Time.deltaTime;
        if(currentPedestrian <= 0)
        {
            spawnPedestrians();
            currentPedestrian = _cooldownPedestrian;
        }
        if(currentVehicle <= 0)
        {
            spawnVehicles();
            currentVehicle = _cooldownVehicle;
        }
    }

    public void spawnPedestrians()
    {
        if (pedestrianPoints == null )
            return;
        var tag = pedestrianTags[0];
       
        GameObject x = pool.spawnObject(tag);
        if (x != null)
        {
            ref var walkingNodes = ref WalkingPath.WalkingSystem.instance.graphNodes;
            var length = walkingNodes.Length;
            int startIndex, endIndex;
            uint start_graphArea, destination_graphArea;
            WalkingPath.MapArea start_mapArea, destination_mapArea;
            Vector3 startPoint, destinationPoint;
            int i = 0;
            do
            {
                i++;
                if (i > 15)
                    Debug.Log("overhead at spawner "+i);
                startIndex = Random.Range(1, length);
                endIndex = Random.Range(1, length);
                if (Math.Abs(endIndex - startIndex) < 100)
                    continue;
                var node = walkingNodes[startIndex];
                startPoint = node.getCenter();
                start_graphArea = node.graphArea;
                start_mapArea = node.mapArea;

                node = walkingNodes[endIndex];
                destinationPoint = node.getCenter();
                destination_graphArea = node.graphArea;
                destination_mapArea = node.mapArea;
                if (start_graphArea == destination_graphArea && !this.isObjectAround(startPoint, x) && 
                    start_mapArea != WalkingPath.MapArea.ROAD && destination_mapArea != WalkingPath.MapArea.ROAD)
                {
                    break;
                }
            } while (true);

            x.GetComponent<PoolElement>().setUp(pool, this.gameObject,
                Quaternion.LookRotation(x.transform.forward, x.transform.up),
                startPoint, destinationPoint, startIndex, endIndex);
        }
    }
    public void spawnVehicles()
    {
        if (CarWayPoints == null )
            return;
        var tag = vehicleTags[0];
        GameObject x = pool.spawnObject(tag);
        if (x != null)
        {
            int index = Random.Range(0, CarWayPoints.Count);
            ref var start = ref CarWayPoints[index].from.onReachWaypointSettings;
            if (this.isObjectAround(start.position, x))
            {
                x.GetComponent<PoolElement>().release();
                return;
            }
               
            Vector3 forward = start.parentRoute.waypointDataList[start.waypointIndexnumber]._transform.position - start.position;
            x.GetComponent<PoolElement>().setUp(pool, this.gameObject,
                Quaternion.LookRotation(forward, x.transform.up),
                 start.position, CarWayPoints[index].to.transform.position);
        }
    }

    public bool isObjectAround(in Vector3 center, GameObject toSpawn)
    {
        int num = Physics.OverlapSphereNonAlloc(center, toSpawn.GetComponent<DynamicControllerBase>().properties.radius*2, colliders, pool.poolMask, QueryTriggerInteraction.UseGlobal);
        for(int i = 0; i < num; i++)
        {
            if (colliders[i].gameObject != toSpawn && colliders[i].tag == toSpawn.tag)
                return true;
        }
        return false;
    }
        
    //private void spawnElement(string tag)
    //{
        
  
    //    int index = UnityEngine.Random.Range(0, destinations.Count);
    //    GameObject x = pool.spawnObject(tag);
    //    if( x != null)
    //    {
            
    //        x.GetComponent<PoolElement>().setUp(pool, this.gameObject, Quaternion.LookRotation(spawnPos.forward, x.transform.up),spawnPos.position, destinations[index]);
    //        remainingCooldown = cooldown;
    //        if (this.tag.CompareTo("Vehicle") == 0)
    //        {
    //            Debug.Log("Spawn" + this.name);
    //        }
    //    }
    //}
    //private void OnDrawGizmos()
    //{
    //    Transform spawnPos = gameObject.transform.GetChild(0);
    //    Gizmos.DrawSphere(spawnPos.position, maxDistance);
    //}

    //private void OnTriggerEnter(Collider other)
    //{
    //    PoolElement e = other.GetComponent<PoolElement>();
    //    if(e.Spawner != this && other.tag.CompareTo(this.tag) ==0)
    //    {
    //        e.release();
    //    }
    //}
}
