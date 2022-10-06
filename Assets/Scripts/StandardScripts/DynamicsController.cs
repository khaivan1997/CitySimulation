using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Physics.Systems;
using Unity.Physics;
using UnityEngine.EventSystems;
using Unity.Entities;
using RaycastHit_Normal = UnityEngine.RaycastHit;
using RaycastHit_ECS = Unity.Physics.RaycastHit;
using Dynamics;

[RequireComponent(typeof(MinimapViewport))]
public class DynamicsController : MonoBehaviour
{
    Camera cam;
    public LayerMask WalkingSurfaces;
    public LayerMask Dynamics;
    public GameObject selectedObject;
    List<int> walkingSurfaces;
    List<int> dynamics;
    BuildPhysicsWorld buildPhysicsWorld;
    void Start()
    {
        walkingSurfaces = MyUtilities.convertMaskToLayers(WalkingSurfaces);
        dynamics = MyUtilities.convertMaskToLayers(Dynamics);
        buildPhysicsWorld = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
        cam = GetComponent<MinimapViewport>().mainCam;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            RaycastHit_Normal hitInfo = new RaycastHit_Normal();
            RaycastHit_ECS hitInfo_ECS = new RaycastHit_ECS();
            UnityEngine.Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastInput ray_ECS = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * cam.farClipPlane,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u,
                    CollidesWith = (uint)(int)Dynamics,
                    GroupIndex = 0,
                }
            };
            Debug.Log($"ray ecs , {ray_ECS.Start} {ray_ECS.End} , normal {ray.direction}");
            Debug.DrawRay(ray_ECS.Start, ray_ECS.End - ray_ECS.Start, Color.green, 5f);
            if (buildPhysicsWorld.PhysicsWorld.CollisionWorld.CastRay(ray_ECS, out hitInfo_ECS))
            {
                if (selectedObject != null)
                {
                    selectedObject.GetComponentInChildren<SelectedComponent>(true).Deselected();
                    if (selectedObject.GetComponent<DynamicControllerBase>().entity == hitInfo_ECS.Entity)
                    {
                        selectedObject = null;
                        return;
                    }
                }
                var entityManager = buildPhysicsWorld.EntityManager;
                if (!entityManager.HasComponent<Inactive>(hitInfo_ECS.Entity))
                {
#if UNITY_EDITOR
                    Debug.Log($"hit { entityManager.GetName(hitInfo_ECS.Entity)}");
#endif
                };
            }
            else if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity, WalkingSurfaces | Dynamics))
            {
                var newSlectedObject = hitInfo.transform.gameObject;
                if (dynamics.Contains(newSlectedObject.layer))
                {
                    if (selectedObject != null)
                        selectedObject.GetComponentInChildren<SelectedComponent>(true).Deselected();
                    if (selectedObject == newSlectedObject)
                        selectedObject = null;
                    else
                    {
                        selectedObject = newSlectedObject;
                        var selectedComponent = selectedObject.GetComponentInChildren<SelectedComponent>(true);
                        if (selectedObject == null) Debug.LogError("Dynamics should have selected Component attached");
                        selectedComponent.Selected();
                    }
                }
                else
                {
                    if (selectedObject != null)
                    {
                        selectedObject.GetComponent<DynamicControllerBase>().setDestination(hitInfo.point);
                    }
                }
            }
        }
    }
}
