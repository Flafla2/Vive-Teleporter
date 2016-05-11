using UnityEngine;
using System.Collections.Generic;

public class ParabolicPointer : MonoBehaviour {

    public ViveNavMesh NavMesh;
    [Header("Parabola Trajectory")]
    public Vector3 InitialVelocity = Vector3.forward * 10f;
    public Vector3 Acceleration = Vector3.up * -9.8f;
    [Header("Parabola Mesh Properties")]
    public int PointCount = 10;
    public float PointSpacing = 0.5f;
    public float GroundHeight = 0;
    public float GraphicThickness = 0.2f;
    public Material GraphicMaterial;
    [Header("Selection Pad Properties")]
    public Mesh SelectionPadMesh;
    public Material SelectionPadFadeMaterial;
    public Material SelectionPadCircleMaterial;
    public Material SelectionPadBottomMaterial;

    public Vector3 SelectedPoint { get; private set; }
    public bool PointOnNavMesh { get; private set; }

    private Mesh ParabolaMesh;

    private static float ParabolicCurve(float p0, float v0, float a, float t)
    {
        return p0 + v0 * t + 0.5f * a * t * t;
    }

    private static float ParabolicCurveDeriv(float v0, float a, float t)
    {
        return v0 + a * t;
    }

    private static Vector3 ParabolicCurve(Vector3 p0, Vector3 v0, Vector3 a, float t)
    {
        Vector3 ret = new Vector3();
        for (int x = 0; x < 3; x++)
            ret[x] = ParabolicCurve(p0[x], v0[x], a[x], t);
        return ret;
    }

    private static Vector3 ParabolicCurveDeriv(Vector3 v0, Vector3 a, float t)
    {
        Vector3 ret = new Vector3();
        for (int x = 0; x < 3; x++)
            ret[x] = ParabolicCurveDeriv(v0[x], a[x], t);
        return ret;
    }

    private static bool CalculateParabolicCurve(Vector3 p0, Vector3 v0, Vector3 a, float dist, int points, float gnd, List<Vector3> outPts)
    {
        outPts.Clear();
        outPts.Add(p0);

        Vector3 last = p0;
        float t = 0;

        for(int i=0; i< points; i++)
        {
            t += dist / ParabolicCurveDeriv(v0, a, t).magnitude;
            Vector3 next = ParabolicCurve(p0, v0, a, t);
            if (next.y < gnd)
            {
                outPts.Add(Vector3.Lerp(last, next, (gnd - last.y) / (next.y - last.y)));
                return true;
            }
            else
                outPts.Add(next);

            last = next;
        }

        return false;
    }

    private static Vector3 ProjectVectorOntoPlane(Vector3 planeNormal, Vector3 point) {
        Vector3 d = Vector3.Project(point, planeNormal.normalized);
        return point - d;
    }

    private void GenerateMesh(ref Mesh m, List<Vector3> points, Vector3 fwd, float uvoffset)
    {
        Vector3[] verts = new Vector3[points.Count * 2];
        Vector2[] uv = new Vector2[points.Count * 2];

        Vector3 right = Vector3.Cross(fwd, Vector3.up).normalized;

        for (int x = 0; x < points.Count; x++)
        {
            verts[2 * x] = points[x] - right * GraphicThickness / 2;
            verts[2 * x + 1] = points[x] + right * GraphicThickness / 2;

            float uvoffset_mod = uvoffset;
            if(x == points.Count - 1 && x > 1) {
                float dist_last = (points[x-2] - points[x-1]).magnitude;
                float dist_cur = (points[x] - points[x-1]).magnitude;
                uvoffset_mod += 1 - dist_cur / dist_last;
            }

            uv[2 * x] = new Vector2(0, x - uvoffset_mod);
            uv[2 * x + 1] = new Vector2(1, x - uvoffset_mod);
        }

        //for(int x=0;x<verts.Length;x++)
        //    verts[x] = transform.InverseTransformPoint(verts[x]);

        int[] indices = new int[2 * 3 * (verts.Length - 2)];
        for (int x = 0; x < verts.Length / 2 - 1; x++)
        {
            int p1 = 2 * x;
            int p2 = 2 * x + 1;
            int p3 = 2 * x + 2;
            int p4 = 2 * x + 3;

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

        m.Clear();
        m.vertices = verts;
        m.uv = uv;
        m.triangles = indices;
        m.RecalculateBounds();
        m.RecalculateNormals();
    }

    void Start() {
        ParabolaPoints = new List<Vector3>(PointCount);

        ParabolaMesh = new Mesh();
        ParabolaMesh.MarkDynamic();
        ParabolaMesh.name = "Parabolic Pointer";
        ParabolaMesh.vertices = new Vector3[0];
        ParabolaMesh.triangles = new int[0];
    }

    private List<Vector3> ParabolaPoints;

    void Update()
    {
        Vector3 velocity = transform.TransformDirection(InitialVelocity);
        Vector3 velocity_normalized;
        ClampInitialVelocity(ref velocity, out velocity_normalized);

        bool didHit = CalculateParabolicCurve(
            transform.position,
            velocity,
            Acceleration, PointSpacing, PointCount,
            GroundHeight,
            ParabolaPoints);

        SelectedPoint = ParabolaPoints[ParabolaPoints.Count-1];

        PointOnNavMesh = true;
        if(NavMesh != null)
        {
            Vector3 rayorigin = SelectedPoint;
            rayorigin.y = GroundHeight + 1;
            float cast = NavMesh.Raycast(new Ray(rayorigin, Vector3.down));

            PointOnNavMesh = cast > 0;
            if(PointOnNavMesh)
                SelectedPoint = rayorigin + Vector3.down * cast;
        }

        bool ShouldDrawMarker = PointOnNavMesh && SelectionPadMesh != null
            && SelectionPadFadeMaterial != null && SelectionPadBottomMaterial != null && SelectionPadCircleMaterial != null;

        if (ShouldDrawMarker)
        {
            Graphics.DrawMesh(SelectionPadMesh, Matrix4x4.TRS(SelectedPoint + Vector3.up * 0.05f, Quaternion.identity, Vector3.one * 0.2f), SelectionPadFadeMaterial, 0, null, 3);
            Graphics.DrawMesh(SelectionPadMesh, Matrix4x4.TRS(SelectedPoint + Vector3.up * 0.05f, Quaternion.identity, Vector3.one * 0.2f), SelectionPadCircleMaterial, 0, null, 1);
            Graphics.DrawMesh(SelectionPadMesh, Matrix4x4.TRS(SelectedPoint + Vector3.up * 0.05f, Quaternion.identity, Vector3.one * 0.2f), SelectionPadBottomMaterial, 0, null, 2);
        }

        GenerateMesh(ref ParabolaMesh, ParabolaPoints, velocity, Time.time % 1);

        Graphics.DrawMesh(ParabolaMesh, Matrix4x4.identity, GraphicMaterial, 0);

        if (ShouldDrawMarker)
            Graphics.DrawMesh(SelectionPadMesh, Matrix4x4.TRS(SelectedPoint + Vector3.up * 0.05f, Quaternion.identity, Vector3.one * 0.2f), SelectionPadFadeMaterial, 0, null, 0);
    }

    // Clamps the given velocity vector so that it can't be more than 45 degrees above the vertical.
    // This is done so that it is easier to leverage the maximum distance (at the 45 degree angle) of
    // parabolic motion.
    private void ClampInitialVelocity(ref Vector3 velocity, out Vector3 velocity_normalized) {
        Vector3 velocity_fwd = ProjectVectorOntoPlane(Vector3.up, velocity);
        float angle = Vector3.Angle(velocity_fwd, velocity);
        if(angle > 45) {
            velocity = Vector3.Slerp(velocity_fwd, velocity, 45f / angle);
            velocity /= velocity.magnitude;
            velocity_normalized = velocity;
            velocity *= InitialVelocity.magnitude;
        } else
            velocity_normalized = velocity.normalized;
    }

#if UNITY_EDITOR
    private List<Vector3> ParabolaPoints_Gizmo;

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
            return;

        if (ParabolaPoints_Gizmo == null)
            ParabolaPoints_Gizmo = new List<Vector3>(PointCount);

        Vector3 velocity = transform.TransformDirection(InitialVelocity);
        Vector3 velocity_normalized;
        ClampInitialVelocity(ref velocity, out velocity_normalized);

        bool didHit = CalculateParabolicCurve(
            transform.position, 
            velocity, 
            Acceleration, PointSpacing, PointCount, 
            GroundHeight,
            ParabolaPoints_Gizmo);

        Gizmos.color = Color.blue;
        for (int x = 0; x < ParabolaPoints_Gizmo.Count - 1; x++)
            Gizmos.DrawLine(ParabolaPoints_Gizmo[x], ParabolaPoints_Gizmo[x + 1]);
        Gizmos.color = Color.green;

        if(didHit)
            Gizmos.DrawSphere(ParabolaPoints_Gizmo[ParabolaPoints_Gizmo.Count-1], 0.2f);
    }
#endif
}
