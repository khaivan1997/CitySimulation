using Unity.Jobs.LowLevel.Unsafe;
using System.Collections.Generic;
using UnityEngine;
using GPUInstancer.CrowdAnimations;
using Unity.Entities;
using Unity.Physics.Systems;

//This class is SingleTon

public class GameController : MonoBehaviour
{
    public static int coresCount = 10;
    public static GameController instance;

    //controller
    public DynamicsController dynamicsController;
    public GPUICrowdManager crowdManager;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
        coresCount = SystemInfo.processorCount - 1;
        JobsUtility.JobWorkerCount = coresCount;
        Debug.Log("job worker count: " + JobsUtility.JobWorkerCount + ",max: " + JobsUtility.JobWorkerMaximumCount);

        World.DefaultGameObjectInjectionWorld.GetExistingSystem<StepPhysicsWorld>().Enabled = false;
    }

    // Start is called before the first frame update

}
