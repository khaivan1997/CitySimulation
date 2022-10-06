using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
public class IntersectionEditorUtilities : MonoBehaviour
{
    public void ResetAllMaterials()
    {
        var intersections = GetComponentsInChildren<IntersectionManager>();
        for (int i = 0; i < intersections.Length; i++)
            intersections[i].ResetAllMaterials();
    }

}
#endif
