
using System.Collections.Generic;
using System;
using UnityEngine;
using GPUInstancer;
public class ObjectPool : MonoBehaviour 
{
    [Serializable]
    public class ObjectPrefab
    {
        public String tag;
        public SingleLayerMask layer;
        public GameObject prefab;
        public int initialSize;
    }

    //public GPUInstancer.CrowdAnimations.GPUICrowdManager crowdManager;

    //public 
    public List<ObjectPrefab> ObjectList;

    public Dictionary<String, Queue<GameObject>> pools;

    public LayerMask poolMask;

    void Start()
    {
        pools = new Dictionary<String, Queue<GameObject>>();
        poolMask = 0;
        var gpuInstacnerManager = GameController.instance.crowdManager;
        for (int index = 0; index < ObjectList.Count; index++)
        {
            var ene = ObjectList[index];
            Queue<GameObject> Pool = new Queue<GameObject>();
            List<GPUInstancerPrefab> gpuInstancerPrefabs = gpuInstancerPrefabs = new List<GPUInstancerPrefab>(ene.initialSize);
            for (int i = 0; i < ene.initialSize; i++)
            {
                GameObject x = Instantiate(ene.prefab) as GameObject;
                x.GetComponent<PoolElement>().SetObjectActive(false);
                x.tag = ene.tag;
                Pool.Enqueue(x);
                if (ene.layer == LayerMask.NameToLayer("pedestrian"))
                    gpuInstancerPrefabs.Add(x.GetComponent<GPUInstancerPrefab>());
            }
            pools.Add(ene.tag, Pool);
            poolMask |= ((LayerMask)ene.layer);
            if (ene.layer == LayerMask.NameToLayer("pedestrian"))
                GPUInstancerAPI.RegisterPrefabInstanceList(gpuInstacnerManager, gpuInstancerPrefabs);
        }
        //Debug.Log("layers:"+LayerMask.LayerToName(MyUtilities.convertMaskToLayers(poolMask)[0]) + " and "+ LayerMask.LayerToName(MyUtilities.convertMaskToLayers(poolMask)[1]));
        //cdTime = 1f / frequency;
        GPUInstancerAPI.InitializeGPUInstancer(gpuInstacnerManager, true);
    }


    public void Release(GameObject myObject)
    {
        pools[myObject.tag].Enqueue(myObject.gameObject);
    }
    public GameObject spawnObject(String tag)
    {
        GameObject x = null;
        if (!pools.ContainsKey(tag))
        {
            Debug.Log("Tag " + tag + " does not exist");
            return x;
        }

        if (pools[tag].Count == 0)
        {
            return null;
            /*foreach (ObjectPrefab prefab in ObjectList)
            {
                if (prefab.tag.CompareTo(tag) == 0)
                {
                    x = Instantiate(prefab.prefab) as GameObject;
                    x.tag = tag;
                    x.transform.SetParent(this.transform.GetChild(0));
                    break;
                }
            }*/
        }
        else
        {
            x = pools[tag].Dequeue();
        }

        return x;
    }
}
