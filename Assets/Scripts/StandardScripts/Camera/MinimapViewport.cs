using System.Collections;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class MinimapViewport : MonoBehaviour
{
    public Camera mainCam;
    public Camera miniMapCam;
    public LayerMask groundMask;
    public int height;
    public LineRenderer line;

    Bounds minimapBound;
    public Bounds MiniMapBound {
        get => minimapBound;
    }
    NativeArray<RaycastCommand> commands;
    [HideInInspector]
    public Vector3[] vertices;
    // Start is called before the first frame update
    void Start()
    {
        var verticalExtent = miniMapCam.orthographicSize * 2f;
        var horizontalExtent = verticalExtent * miniMapCam.aspect;
        var miniMapCamPosition = miniMapCam.transform.position;
        minimapBound = new Bounds(miniMapCamPosition, new Vector3(horizontalExtent, 0, verticalExtent));
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        commands = new NativeArray<RaycastCommand>(4, Allocator.Persistent);
        vertices = new Vector3[4];
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == miniMapCam)
        {
            OnPostRender();
        }
    }

    void OnPostRender()
    {
        CalculateViewPort();
        //DrawWithGL();
        DrawWithLineRenderer();
    }

    /// <summary>
    /// <para>Calculate the array vertices of bounds, the order is as follow:
    ///     topLeftCorner, topRightCorner, bottomLeftCorner, bottomRightCorner</para>
    ///     <para> The rotation and far plane distance of the main camera are also considered</para>
    ///   The results can be obtained as public variables vertices 
    ///   
    /// </summary>
    void CalculateViewPort()
    {
        Ray topLeftRay = mainCam.ScreenPointToRay(new Vector3(0, Screen.height));
        Ray topRightRay = mainCam.ScreenPointToRay(new Vector3(Screen.width, Screen.height));
        Ray bottomLeftRay = mainCam.ScreenPointToRay(new Vector3(0, 0));
        Ray bottomRightRay = mainCam.ScreenPointToRay(new Vector3(Screen.width, 0));
        var maxDistance = mainCam.farClipPlane;
        commands[0] = new RaycastCommand(topLeftRay.origin, topLeftRay.direction, maxDistance, groundMask);
        commands[1] = new RaycastCommand(topRightRay.origin, topRightRay.direction, maxDistance, groundMask);
        commands[2] = new RaycastCommand(bottomLeftRay.origin, bottomLeftRay.direction, maxDistance, groundMask);
        commands[3] = new RaycastCommand(bottomRightRay.origin, bottomRightRay.direction, maxDistance, groundMask);

        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(4, Allocator.TempJob);

        JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));
        handle.Complete();

        for (int i = 0; i < 4; i++)
        {
            if (results[i].collider != null)
            {
                var point = results[i].point;
                point.y = 0;
                vertices[i] = minimapBound.ClosestPoint(point);
            }
            else
            {
                if (commands[i].distance != Mathf.Infinity)
                {
                    var point = commands[i].from + commands[i].distance * commands[i].direction.normalized;
                    point.y = 0;
                    vertices[i] = minimapBound.ClosestPoint(point);
                }
                else
                {
                    //Map unreached view port to minimap bounds
                    if (i == 0)
                        vertices[i] = minimapBound.ClosestPoint(mainCam.transform.rotation * (minimapBound.center + new Vector3(-minimapBound.extents.x, 0, minimapBound.extents.z)));
                    else if (i == 1)
                        vertices[i] = minimapBound.ClosestPoint(mainCam.transform.rotation * minimapBound.max);
                    else if (i == 2)
                        vertices[i] = minimapBound.ClosestPoint(mainCam.transform.rotation * minimapBound.min);
                    else
                        vertices[i] = minimapBound.ClosestPoint(mainCam.transform.rotation * (minimapBound.center + new Vector3(minimapBound.extents.x, 0, -minimapBound.extents.z)));
                }
            }
        }
        results.Dispose();
    }

    void DrawWithLineRenderer()
    {
        for (int i = 0; i < 4; i++)
        {
            vertices[i].y = height;
        }
        Vector3[] points =
        {
            vertices[0],
            vertices[1],
            vertices[3],
            vertices[2],
            vertices[0],
        };
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    void DrawWithGL()
    {
        for (int i = 0; i < 4; i++)
        {
            vertices[i] = miniMapCam.WorldToViewportPoint(vertices[i]);
            vertices[i].z = -1f;
        }

        GL.PushMatrix();
        {
            GL.LoadOrtho();
            GL.Begin(GL.LINES);
            {
                GL.Vertex(vertices[0]);
                GL.Vertex(vertices[1]);
                GL.Vertex(vertices[1]);
                GL.Vertex(vertices[3]);
                GL.Vertex(vertices[3]);
                GL.Vertex(vertices[2]);
                GL.Vertex(vertices[2]);
                GL.Vertex(vertices[0]);
            }
            GL.End();
        }
        GL.PopMatrix();
    }

    void OnDestroy()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        commands.Dispose();
    }
}
