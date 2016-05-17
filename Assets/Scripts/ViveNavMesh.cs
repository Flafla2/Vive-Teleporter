using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// \brief A version of Unity's baked navmesh that is converted to a (serializable) component.  This allows the navmesh 
///        used for Vive navigation to be separated form the AI Navmesh.  ViveNavMesh also handles the rendering of the 
///        NavMesh grid in-game.
[RequireComponent(typeof(BorderRenderer))]
[ExecuteInEditMode]
public class ViveNavMesh : MonoBehaviour, ISerializationCallbackReceiver
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
    [SerializeField]
    private Material _GroundMaterial;

    /// \brief The alpha value of the ground
    /// \sa GroundMaterial
    public float GroundAlpha = 1.0f;
    private float LastGroundAlpha = 1.0f;
    private int AlphaShaderID = -1;

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
    /// describe the borders of SelectableMesh.
    public Vector3[][] SelectableMeshBorder
    {
        get { return _SelectableMeshBorder; }
        set { _SelectableMeshBorder = value; Border.Points = _SelectableMeshBorder; }
    }
    private Vector3[][] _SelectableMeshBorder;
    // Use this to actually serialize SelectableMeshBorder (you can't serialize multidimensional arrays apparently)
    [SerializeField] [HideInInspector]
    private SerializableMultiDim _Serialized;

    private BorderRenderer Border;

    private Dictionary<Camera, CommandBuffer> cameras = new Dictionary<Camera, CommandBuffer>();

    void Start () {
        Border = GetComponent<BorderRenderer>();
        Border.Points = SelectableMeshBorder;

        AlphaShaderID = Shader.PropertyToID("_Alpha");
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

        var cam = Camera.current;
        if (!cam || cam.cameraType == CameraType.Preview)
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

    /// \brief Casts a ray against the contents of this mesh (in world space)
    /// 
    /// \param ray The ray to cast against the navmesh, in world space
    /// 
    /// \return -1 if no hit, or the distance along the ray of the hit
    public float Raycast(Ray ray)
    {
        if (SelectableMesh == null)
            return -1;

        for(int x=0;x< SelectableMesh.triangles.Length/3;x++)
        {
            Vector3 p1 = SelectableMesh.vertices[SelectableMesh.triangles[x*3  ]];
            Vector3 p2 = SelectableMesh.vertices[SelectableMesh.triangles[x*3+1]];
            Vector3 p3 = SelectableMesh.vertices[SelectableMesh.triangles[x*3+2]];

            float i = Intersect(p1, p2, p3, ray);
            if (i > 0)
                return i;
        }
        return -1;
    }

    /// \brief  Checks if the specified ray hits the triangle descibed by p1, p2 and p3.
    ///         Möller–Trumbore ray-triangle intersection algorithm implementation.
    ///         
    /// \param p1 Point 1 of triangle
    /// \param p2 Point 2 of triangle
    /// \param p3 Point 3 of triangle
    ///
    /// Adapted From: http://answers.unity3d.com/questions/861719/a-fast-triangle-triangle-intersection-algorithm-fo.html
    private static float Intersect(Vector3 p1, Vector3 p2, Vector3 p3, Ray ray)
    {
        // Vectors from p1 to p2/p3 (edges)
        Vector3 e1 = p2 - p1;
        Vector3 e2 = p3 - p1;

        Vector3 p, q, t;
        float det, invDet, u, v;        

        // calculating determinant 
        p = Vector3.Cross(ray.direction, e2);
        det = Vector3.Dot(e1, p);

        //if determinant is near zero, ray lies in plane of triangle otherwise not
        if (det > -Mathf.Epsilon && det < Mathf.Epsilon) { return -1; }
        invDet = 1.0f / det;

        //calculate distance from p1 to ray origin
        t = ray.origin - p1;

        //Calculate u parameter
        u = Vector3.Dot(t, p) * invDet;

        //Check for ray hit
        if (u < 0 || u > 1) { return -1; }

        //Prepare to test v parameter
        q = Vector3.Cross(t, e1);

        //Calculate v parameter
        v = Vector3.Dot(ray.direction, q) * invDet;

        //Check for ray hit
        if (v < 0 || u + v > 1) { return -1; }

        float dist = Vector3.Dot(e2, q) * invDet;
        if (dist <= Mathf.Epsilon)
            return -1;

        return dist;
    }

    public void OnBeforeSerialize()
    {
        _Serialized = new SerializableMultiDim(_SelectableMeshBorder);
    }

    public void OnAfterDeserialize()
    {
        _SelectableMeshBorder = _Serialized.ToMultiDimArray();
    }

    [System.Serializable]
    private class SerializableMultiDim
    {
        public int[] startIndex;
        public int[] lengths;
        public Vector3[] arr;

        public SerializableMultiDim (Vector3[][] src) {
            if(src == null)
            {
                startIndex = new int[0];
                lengths = new int[0];
                arr = new Vector3[0];
                return;
            }

            startIndex = new int[src.Length];
            lengths = new int[src.Length];
            int cur = 0;
            for (int x = 0; x < src.Length; x++)
            {
                startIndex[x] = cur;
                lengths[x] = src[x].Length;
                cur += src[x].Length;
            }

            arr = new Vector3[cur];
            for (int x = 0; x < src.Length; x++)
                for (int i = 0; i < src[x].Length; i++)
                    arr[startIndex[x] + i] = src[x][i];
        }

        public Vector3[][] ToMultiDimArray()
        {
            Vector3[][] ret = new Vector3[startIndex.Length][];
            for(int x=0;x<ret.Length;x++)
            {
                ret[x] = new Vector3[lengths[x]];
                for (int i = 0; i < ret[x].Length; i++)
                    ret[x][i] = arr[startIndex[x] + i];
            }
            return ret;
        }
    }
}
