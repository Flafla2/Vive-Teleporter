using UnityEngine;
using System.Collections;

/// \brief A generic component that renders a border using the given polylines.  The borders are double sided and are oriented
///        upwards (ie normals are parallel to the XZ plane)
[AddComponentMenu("Vive Teleporter/Border Renderer")]
[ExecuteInEditMode]
public class BorderRenderer : MonoBehaviour {
    private Mesh[] CachedMeshes;
    /// Material used to render the border mesh.  Note: UVs are set up so that v=0->bottom and v=1->top of border
    [Tooltip("Material used to render the border mesh.  UV's are set up so that v=0->bottom and v=1->top.  u is stretched along each edge.")]
    public Material BorderMaterial;

    [System.NonSerialized]
    public Matrix4x4 Transpose = Matrix4x4.identity;

    [SerializeField] [Range(0,1)]
    [Tooltip("Alpha (transparency) of the border mesh.")]
    public float BorderAlpha = 1.0f;
    private float LastBorderAlpha = 1.0f;

    [Tooltip("Layer to render the mesh at.")]
    private int AlphaShaderID = -1;
    
    /// Polylines that will be drawn.
    public BorderPointSet[] Points {
        get
        {
            return _Points;
        }
        set
        {
            _Points = value;
            RegenerateMesh();
        }
    }
    private BorderPointSet[] _Points;

    public float BorderHeight
    {
        get
        {
            return _BorderHeight;
        }
        set
        {
            _BorderHeight = value;
            RegenerateMesh();
        }
    }
    [SerializeField]
    [Tooltip("Height of the border mesh, in meters.")]
    private float _BorderHeight = 0.2f;

    void Update()
    {
        if (CachedMeshes == null || BorderMaterial == null)
            return;

        if (LastBorderAlpha != BorderAlpha && BorderMaterial != null)
        {
            BorderMaterial.SetFloat("_Alpha", BorderAlpha);
            LastBorderAlpha = BorderAlpha;
        }

        foreach (Mesh m in CachedMeshes)
            Graphics.DrawMesh(m, Transpose, BorderMaterial, gameObject.layer, null, 0, null, false, false);
    }

    void OnValidate()
    {
        RegenerateMesh();

        if (AlphaShaderID == -1)
            AlphaShaderID = Shader.PropertyToID("_Alpha");
        if(BorderMaterial != null)
            BorderMaterial.SetFloat(AlphaShaderID, BorderAlpha);
    }

    public void RegenerateMesh()
    {
        if (Points == null)
        {
            CachedMeshes = new Mesh[0];
            return;
        }
        CachedMeshes = new Mesh[Points.Length];
        for (int x = 0; x < CachedMeshes.Length; x++)
        {
            if (Points[x] == null || Points[x].Points == null)
                CachedMeshes[x] = new Mesh();
            else
                CachedMeshes[x] = GenerateMeshForPoints(Points[x].Points);
        }
    }
	
	private Mesh GenerateMeshForPoints(Vector3[] Points)
    {
        if (Points.Length <= 1)
            return new Mesh();

        Vector3[] verts = new Vector3[Points.Length * 2];
        Vector2[] uv = new Vector2[Points.Length * 2];
        for(int x=0;x<Points.Length;x++)
        {
            verts[2 * x] = Points[x];
            verts[2 * x + 1] = Points[x] + Vector3.up * BorderHeight;

            uv[2 * x] = new Vector2(x % 2, 0);
            uv[2 * x + 1] = new Vector2(x % 2, 1);
        }

        int[] indices = new int[2 * 3 * (verts.Length - 2)];
        for(int x=0;x<verts.Length/2-1;x++)
        {
            int p1 = 2*x;
            int p2 = 2*x + 1;
            int p3 = 2*x + 2;
            int p4 = 2*x + 3;

            indices[12 * x] = p1;
            indices[12 * x + 1] = p2;
            indices[12 * x + 2] = p3;
            indices[12 * x + 3] = p3;
            indices[12 * x + 4] = p2;
            indices[12 * x + 5] = p4;

            indices[12 * x + 6] = p3;
            indices[12 * x + 7] = p2;
            indices[12 * x + 8] = p1;
            indices[12 * x + 9] = p4;
            indices[12 * x + 10] = p2;
            indices[12 * x + 11] = p3;
        }

        Mesh m = new Mesh();
        m.vertices = verts;
        m.uv = uv;
        m.triangles = indices;
        m.RecalculateBounds();
        m.RecalculateNormals();
        return m;
    }
}
