using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(MinimapViewport))]
public class ViewController : MonoBehaviour
{
    MinimapViewport minimapViewport;
    float minimumFarClipPlane;
    private UniversalAdditionalCameraData camData;
    [Tooltip("base is 500, so max is 500 + this")]
    public float increasingFarClipPlane = 9500f;
    public GameObject[] batchingRoots;
    public TMPro.TextMeshProUGUI cameraText;

    public RawImage minimap;
    private void Start()
    {
        minimapViewport = GetComponent<MinimapViewport>();
        minimumFarClipPlane = minimapViewport.mainCam.farClipPlane;
        camData = minimapViewport.mainCam.GetUniversalAdditionalCameraData();
        StaticBatchingUtility.Combine(batchingRoots, this.gameObject);
        cameraText.SetText(minimapViewport.mainCam.farClipPlane.ToString());
    }
    // Update is called once per frame
    public bool isHiddenDynamicsVisible
    {
        get
        {
            return minimapViewport.mainCam.useOcclusionCulling;
        }
        set
        {
            camData.SetRenderer(value ? 0 : 1);
        }
    }

    public void SetFarClipPlane (float factor)
    {
        float value = minimumFarClipPlane + factor * increasingFarClipPlane;
        minimapViewport.mainCam.farClipPlane = value;
        cameraText.SetText(value.ToString());
    }

    public void OnMinimapClick(BaseEventData data)
    {
        var pointerData = (PointerEventData)data;
        Vector2 localClick;
        var textureRectTransform = minimap.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textureRectTransform, pointerData.position, null, out localClick);
        Vector2 viewportClick = new Vector2(localClick.x / textureRectTransform.rect.xMax, localClick.y / (textureRectTransform.rect.yMin * -1));
        localClick.y = (textureRectTransform.rect.yMin * -1) - (localClick.y * -1);
        Debug.Log($"OnPointerClick {Input.mousePosition} {pointerData.position} {localClick}");
    }
}
