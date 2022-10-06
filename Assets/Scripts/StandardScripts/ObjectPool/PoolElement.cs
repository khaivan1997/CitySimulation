using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DynamicControllerBase))] 
public class PoolElement : MonoBehaviour
{
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public GameObject Spawner;
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public ObjectPool pool;
    
    [HideInInspector]
    public DynamicControllerBase controller;
    HardStuckFix fix;
    Renderer[] renderers;

#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public bool isActive;
    private void Awake()
    {
        controller = GetComponent<DynamicControllerBase>();
        fix = GetComponent<HardStuckFix>();
        fix.OnDisableCall();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void SetObjectActive(bool active)
    {
        if (!active)
        {
            controller.OnDisableCall();
            
            fix.OnDisableCall();
        }
        else
            fix.OnEnableCall();
        isActive = active;

        if (controller is PedestrianController) { }
        else
            switchRender(active);
        
    }

    public void release()
    {
        if (GameController.instance.dynamicsController.selectedObject == gameObject)
            return;
        controller.OnDisableCall();
        SetObjectActive(false);
        if (pool != null)
            pool.Release(this.gameObject);
    }

    private void switchRender(bool active = true)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = active;
        
    }

    public void setUp(in ObjectPool pool, in GameObject Spawner, in Quaternion lookRotation, Vector3 start, in Vector3 destination, in int startIndex = 0, in int endIndex = 0)
    {
        start.y += controller.properties.height/2f;
        this.pool = pool;
        this.Spawner = Spawner;
        this.transform.rotation = lookRotation;
        this.transform.position = start;
        SetObjectActive(true);
        controller.setDestination(destination, startIndex, endIndex);
        fix.OnEnableCall();
    }

}
