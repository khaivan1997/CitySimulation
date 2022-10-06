using UnityEditor;
using UnityEngine;

public class TrafficLight:MonoBehaviour
{
    [System.Serializable]
    public struct Light
    {
        public Renderer displayObject;
    }
    public Light[] lights;
    public enum LightIndex
    {
        NONE = -1,
        RED = 0,
        YELLOW = 1,
        GREEN = 2,
    }

    private LightIndex currentIndex = LightIndex.NONE;
    
    // Start is called before the first frame update

    public void setLightIndex (LightIndex index)
    {
        for (int i = 0; i< lights.Length; i++)
        {
            Material mat;
#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
                mat = lights[i].displayObject.material;
#if UNITY_EDITOR
            else
                mat = lights[i].displayObject.sharedMaterial;             
#endif
            if (i == (int)index)
                mat.SetFloat("_GlowPower", 1f);
            else mat.SetFloat("_GlowPower", -10f);
        }
        currentIndex = index;
    }

    public LightIndex getCurrentLightIndex()
    {
        return currentIndex;
    }

}
