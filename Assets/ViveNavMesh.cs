using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;

[RequireComponent(typeof(BorderRenderer))]
[ExecuteInEditMode]
public class ViveNavMesh : MonoBehaviour, ISerializationCallbackReceiver
{
    public Material GroundMaterial;

    public Mesh SelectableMesh
    {
        get { return _SelectableMesh; }
        set { _SelectableMesh = value; }
    }
    [SerializeField] [HideInInspector]
    private Mesh _SelectableMesh;

    public Vector3[][] SelectableMeshBorder
    {
        get { return _SelectableMeshBorder; }
        set { _SelectableMeshBorder = value; Border.Points = _SelectableMeshBorder; }
    }
    private Vector3[][] _SelectableMeshBorder;
    [SerializeField] [HideInInspector]
    private SerializableMultiDim _Serialized;

    private BorderRenderer Border;

	void Start () {
        Border = GetComponent<BorderRenderer>();
        Border.Points = SelectableMeshBorder;

        UpdateCommandBuffer();
    }

    private void UpdateCommandBuffer()
    {
        if (GroundMaterial != null && _SelectableMesh != null)
        {
            CommandBuffer buf = new CommandBuffer();
            buf.DrawMesh(_SelectableMesh, Matrix4x4.identity, GroundMaterial, 0);
            Camera.current.AddCommandBuffer(CameraEvent.AfterForwardOpaque, buf);
        }
    }

    void Update()
    {
        //if (GroundMaterial != null && _SelectableMesh != null)
        //    Graphics.DrawMesh(_SelectableMesh, Matrix4x4.identity, GroundMaterial, 0, null, 0, null, false, false);
    }

    void OnValidate()
    {
        Border = GetComponent<BorderRenderer>();
        Border.Points = SelectableMeshBorder;

        UpdateCommandBuffer();
    }

    // Casts a ray against the contents of this mesh (in world space)
    // 
    // Returns -1 if no hit, or the distance along the ray of the hit
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

    // Checks if the specified ray hits the triangle descibed by p1, p2 and p3.
    // Möller–Trumbore ray-triangle intersection algorithm implementation.
    //
    // Adapted From: http://answers.unity3d.com/questions/861719/a-fast-triangle-triangle-intersection-algorithm-fo.html
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
