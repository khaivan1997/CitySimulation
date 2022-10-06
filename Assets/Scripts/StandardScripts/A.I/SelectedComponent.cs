using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SelectedComponent : MonoBehaviour
{
    public bool isActive
    {
        get
        {
            return gameObject.activeSelf;
        }
    }
    public void Selected()
    {
        this.gameObject.SetActive(true);
    }
    public void Deselected()
    {
        this.gameObject.SetActive(false);
    }
}
