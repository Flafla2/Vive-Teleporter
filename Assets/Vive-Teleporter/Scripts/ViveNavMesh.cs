using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// \brief A version of Unity's baked navmesh that is converted to a (serializable) component.  This allows the navmesh 
///        used for Vive navigation to be separated form the AI Navmesh.  ViveNavMesh also handles the rendering of the 
///        NavMesh grid in-game.
[AddComponentMenu("Vive Teleporter/Vive Nav Mesh")]
[RequireComponent(typeof(BorderRenderer))]
[ExecuteInEditMode]
public class ViveNavMesh : MonoBehaviour
{
    /// Material used for the floor mesh when the user is selecting a point to teleport to
    public Material GroundMaterial
    {
        get { return _GroundMaterial; }
        set
        {
            Material old = _GroundMaterial;
            _GroundMaterial = value;
            if(_GroundMaterial != null)
                _GroundMaterial.SetFloat(AlphaShaderID, GroundAlpha);
            if (old != _GroundMaterial)
                Cleanup();
        }
    }
    private Material _GroundMaterial;
    [FormerlySerializedAs("_GroundMaterial")]
    [SerializeField]
    private Material _GroundMaterialSource;

    /// \brief The alpha (transparency) value of the rendered ground mesh)
    /// \sa GroundMaterial
    [Range(0,1)]
    public float GroundAlpha = 1.0f;
    private float LastGroundAlpha = 1.0f;
    private int AlphaShaderID = -1;

    public int LayerMask
    {
        get { return _LayerMask;  }
        set {  _LayerMask = value; }
    }
    [SerializeField]
    private int _LayerMask = 0;
    public bool IgnoreLayerMask
    {
        get { return _IgnoreLayerMask; }
        set { _IgnoreLayerMask = value; }
    }
    [SerializeField]
    private bool _IgnoreLayerMask = true;

    public int QueryTriggerInteraction
    {
        get { return _QueryTriggerInteraction; }
        set { _QueryTriggerInteraction = value; }
    }
    [SerializeField]
    private int _QueryTriggerInteraction = 0;

    /// A Mesh that represents the "Selectable" area of the world.  This is converted from Unity's NavMesh in ViveNavMeshEditor
    public Mesh SelectableMesh
    {
        get { return _SelectableMesh; }
        set { _SelectableMesh = value; Cleanup(); } // Cleanup because we need to change the mesh inside command buffers
    }
    [SerializeField] [HideInInspector]
    private Mesh _SelectableMesh;

    /// \brief The border points of SelectableMesh.  This is automatically generated in ViveNavMeshEditor.
    /// 
    /// This is an array of Vector3 arrays, where each Vector3 array is the points in a polyline.  These polylines combined
    /// describe the borders of SelectableMesh.  We have to use BorderPointSets instead of a jagged Vector3[][] array because
    /// Unity can't serialize jagged arrays for some reason.
    public BorderPointSet[] SelectableMeshBorder
    {
        get { return _SelectableMeshBorder; }
        set { _SelectableMeshBorder = value; Border.Points = _SelectableMeshBorder; }
    }
    [SerializeField] [HideInInspector]
    private BorderPointSet[] _SelectableMeshBorder;

    [SerializeField] [HideInInspector]
    private int _NavAreaMask = ~0; // Initialize to all

    public bool IgnoreSlopedSurfaces { get { return _IgnoreSlopedSurfaces; } }
    [SerializeField]
    private bool _IgnoreSlopedSurfaces = true;

    public float SampleRadius
    {
        get { return _SampleRadius; }
    }
    [SerializeField]
    private float _SampleRadius = 0.25f;

    public NavmeshDewarpingMethod DewarpingMethod { get { return _DewarpingMethod; } }
    [SerializeField]
    private NavmeshDewarpingMethod _DewarpingMethod = NavmeshDewarpingMethod.None;

    private BorderRenderer Border;

    private Dictionary<Camera, CommandBuffer> cameras = new Dictionary<Camera, CommandBuffer>();

    void Start () {
        if (SelectableMesh == null)
            SelectableMesh = new Mesh();
        if (_SelectableMeshBorder == null)
            _SelectableMeshBorder = new BorderPointSet[0];

        Border = GetComponent<BorderRenderer>();
        Border.Points = SelectableMeshBorder;

        AlphaShaderID = Shader.PropertyToID("_Alpha");

        if(_GroundMaterialSource != null)
            GroundMaterial = new Material(_GroundMaterialSource);

        if (GroundAlpha != LastGroundAlpha && GroundMaterial != null)
        {
            GroundMaterial.SetFloat(AlphaShaderID, GroundAlpha);
            LastGroundAlpha = GroundAlpha;
        }

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void Update ()
    {
        // We have to detect changes this way instead of using properties because
        // we want to be able to animate the alpha value with a Unity animator.
        if (GroundAlpha != LastGroundAlpha && GroundMaterial != null)
        {
            GroundMaterial.SetFloat(AlphaShaderID, GroundAlpha);
            LastGroundAlpha = GroundAlpha;
        }
    }

    private void Cleanup()
    {
        foreach (var cam in cameras)
        {
            if (cam.Key)
            {
                cam.Key.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, cam.Value);
            }
        }
        cameras.Clear();
    }

    public void OnEnable()
    {
        Cleanup();
    }

    public void OnDisable()
    {
        Cleanup();
    }

    void OnRenderObject()
    {
        // We have to use command buffers instead of Graphics.DrawMesh because of strange depth issues that I am experiencing
        // with Graphics.Drawmesh (perhaps Graphics.DrawMesh is called before all opaque objects are rendered?)
        var act = gameObject.activeInHierarchy && enabled;
        if (!act)
        {
            Cleanup();
            return;
        }

        // If _SelectableMesh == null there is a crash in Unity 5.4 beta (apparently you can't pass null to CommandBuffer::DrawMesh now).
        if (!_SelectableMesh || !GroundMaterial)
            return;

        var cam = Camera.current;
        if (!cam || cam.cameraType == CameraType.Preview || ((1 << gameObject.layer) & Camera.current.cullingMask) == 0)
            return;

        CommandBuffer buf = null;
        if (cameras.ContainsKey(cam))
            return;

        buf = new CommandBuffer();
        // Note: Mesh is drawn slightly pushed upwards to avoid z-fighting issues
        buf.DrawMesh(_SelectableMesh, Matrix4x4.TRS(Vector3.up * 0.005f, Quaternion.identity, Vector3.one), GroundMaterial, 0);
        cameras[cam] = buf;
        cam.AddCommandBuffer(CameraEvent.AfterForwardOpaque, buf);
    }

    void OnValidate()
    {
        Border = GetComponent<BorderRenderer>();
        Border.Points = SelectableMeshBorder;

        if(AlphaShaderID == -1)
            AlphaShaderID = Shader.PropertyToID("_Alpha");
    }

    /// \brief Casts a ray against the Navmesh and attempts to calculate the ray's worldspace intersection with it.
    /// 
    /// This uses Physics raycasts to perform the raycast calculation, so the teleport surface must have a collider
    /// on it.
    /// 
    /// \param p1 First (origin) point of ray
    /// \param p2 Last (end) point of ray
    /// \param pointOnNavmesh If the raycast hit something on the navmesh.
    /// \param hitPoint If hit, the point of the hit.  Otherwise zero.
    /// \param normal If hit, the normal of the hit surface.  Otherwise (0, 1, 0)
    /// 
    /// \return If the raycast hit something.
    public bool Linecast(Vector3 p1, Vector3 p2, out bool pointOnNavmesh, out Vector3 hitPoint, out Vector3 normal)
    {
        RaycastHit hit;
        Vector3 dir = p2 - p1;
        float dist = dir.magnitude;
        dir /= dist;
        if(Physics.Raycast(p1, dir, out hit, dist, _IgnoreLayerMask ? ~_LayerMask : _LayerMask, (QueryTriggerInteraction) _QueryTriggerInteraction))
        {
            normal = hit.normal;
            if (Vector3.Dot(Vector3.up, hit.normal) < 0.99f && _IgnoreSlopedSurfaces)
            {
                pointOnNavmesh = false;
                hitPoint = hit.point;
                
                return true;
            }

            hitPoint = hit.point;
            NavMeshHit navHit;
            pointOnNavmesh = NavMesh.SamplePosition(hitPoint, out navHit, _SampleRadius, _NavAreaMask);

            return true;
        }
        pointOnNavmesh = false;
        hitPoint = Vector3.zero;
        normal = Vector3.up;
        return false;
    }
}

[System.Serializable]
public class BorderPointSet
{
    public Vector3[] Points;

    public BorderPointSet(Vector3[] Points)
    {
        this.Points = Points;
    }
}

[System.Serializable]
public enum NavmeshDewarpingMethod
{
    None, RoundToVoxelSize, RaycastDownward
}