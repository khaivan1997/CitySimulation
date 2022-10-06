using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PoolElement))]
[RequireComponent(typeof(DynamicControllerBase))]
public class HardStuckFix : MonoBehaviour
{
    public float stuckThreshold;
    //fix hard stuck
#if UNITY_EDITOR
    [EditorReadOnly]
#endif
    public Vector3 previousPos;
    // Start is called before the first frame update
    private PoolElement poolElement;
    private DynamicControllerBase controller;

    public bool isActive;

    private void Awake()
    { 
        poolElement = GetComponent<PoolElement>();
        controller = GetComponent<DynamicControllerBase>();
        isActive = false;
    }

    public void OnEnableCall()
    {
        previousPos = transform.position;     
        wasWaitingForNewPath = false;
        isActive = true;
    }

    public void OnDisableCall()
    {
        wasWaitingForNewPath = false;
        isActive = false;
    }

    bool wasWaitingForNewPath = false;
    public void FixStuck()
    {
        if (!isActive)
            return;

        if (controller.haveJob)
        {
            wasWaitingForNewPath = true;
            return;
        }

        if (wasWaitingForNewPath)
        {
            wasWaitingForNewPath = false;
            return;
        }
            
        Vector3 currentPos = transform.position;
        float distance = Vector3.Distance(currentPos, previousPos);
        if (!controller.forceStop && (distance <= stuckThreshold || Mathf.Approximately(distance, stuckThreshold)))
            poolElement.release();
        previousPos = currentPos;
    }
}
