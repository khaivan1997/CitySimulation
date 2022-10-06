using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using MathUltilities;
using Unity.Entities;


public class DynamicControllerSystem : MonoBehaviour, DynamicVisitorBase
{
    public static DynamicControllerSystem instance;

    public static List<PedestrianController> pedestrians;
    public static List<VehicleController> vehicles;

    public List<PedestrianController> pedestriansInspector;
    public List<VehicleController> vehiclesInspector;

    public static float stuckfixPeriod_pedestrian = 30f;

    public static float stuckfixPeriod_vehicle = 30f;


    JobHandle pedestrianJob;
    bool havePedestrianJob;
    //UI
    public Text numActivePedestrians_Text;
    public Text numActiveVehicles_Text;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        else
        {
            instance = this;
        }

        //Physics.autoSimulation = false;
        pedestrians = new List<PedestrianController>(0);
        vehicles = new List<VehicleController>(0);
        this.pedestriansInspector = pedestrians;
        this.vehiclesInspector = vehicles;
    }

    private void Start()
    {
        havePedestrianJob = false;
    }

    private void OnDestroy()
    {
        pedestrians = null;
        vehicles = null;
        CancelInvoke();
    }

    public static void Add(in VehicleController controller)
    {
         vehicles.Add(controller);
    }

    public static void Remove(in DynamicControllerBase controller)
    {
        if (controller is PedestrianController con)
        {
            pedestrians.Remove(con);

        }
        else if (controller is VehicleController con1)
        {
            vehicles.Remove(con1);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        UpdateVehicles();
    }


    void UpdateVehicles()
    {
        int count = 0;
        int length = vehicles.Count;
        for (int i = 0; i < length; i++)
        {
            if (vehicles[i].poolElement.isActive)
            {
                vehicles[i].FixedUpdateCall();
                count++;
            }
        }
        numActiveVehicles_Text.text = count + " active vehicles";
    }

    void FixStuckPedestrian()
    {
        int length = pedestrians.Count;
        for (int i = 0; i < length; i++)
        {
            pedestrians[i].fix.FixStuck();
        }
    }

    void FixStuckVehicle()
    {
        int length = vehicles.Count;
        for (int i = 0; i < length; i++)
        {
            vehicles[i].fix.FixStuck();
        }
    }

    public void VisitPedestrian(PedestrianController c, int index)
    {
        
    }

    public void VisitVehicle(VehicleController c, int index)
    {

    }
}

