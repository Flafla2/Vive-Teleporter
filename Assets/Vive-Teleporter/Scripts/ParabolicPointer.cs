using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Vive Teleporter/Parabolic Pointer")]
public class ParabolicPointer : MonoBehaviour {

    public ViveNavMesh NavMesh;
    [Header("Parabola Trajectory")]
    [Tooltip("Initial velocity of the parabola, in local space.")]
    public Vector3 InitialVelocity = Vector3.forward * 10f;
    [Tooltip("World-space \"acceleration\" of the parabola.  This effects the falloff of the curve.")]
    public Vector3 Acceleration = Vector3.up * -9.8f;
    [Header("Parabola Mesh Properties")]
    [Tooltip("Number of points on the parabola mesh.  Greater point counts lead to a higher poly/smoother mesh.")]
    public int PointCount = 10;
    [Tooltip("Approximate spacing between each of the points on the parabola mesh.")]
    public float PointSpacing = 0.5f;
    [Tooltip("Thickness of the parabola mesh")]
    public float GraphicThickness = 0.2f;
    [Tooltip("Material to use to render the parabola mesh")]
    public Material GraphicMaterial;
    [Header("Selection Pad Properties")]
    [SerializeField]
    [Tooltip("Prefab to use as the selection pad when the player is pointing at a valid teleportable surface.")]
    private GameObject SelectionPadPrefab;
    [SerializeField]
    [Tooltip("Prefab to use as the selection pad when the player is pointing at an invalid teleportable surface.")]
    private GameObject InvalidPadPrefab;

    
    public Vector3 SelectedPoint { get; private set; }
    public bool PointOnNavMesh { get; private set; }
    public float CurrentParabolaAngleY { get; private set; }
    public Vector3 CurrentPointVector { get; private set; }

    private GameObject SelectionPadObject;
    private GameObject InvalidPadObject;


    private Mesh ParabolaMesh;

    // Parabolic motion equation, y = p0 + v0*t + 1/2at^2
    private static float ParabolicCurve(float p0, float v0, float a, float t)
    {
        return p0 + v0 * t + 0.5f * a * t * t;
    }

    // Derivative of parabolic motion equation
    private static float ParabolicCurveDeriv(float v0, float a, float t)
    {
        return v0 + a * t;
    }

    // Parabolic motion equation applied to 3 dimensions
    private static Vector3 ParabolicCurve(Vector3 p0, Vector3 v0, Vector3 a, float t)
    {
        Vector3 ret = new Vector3();
        for (int x = 0; x < 3; x++)
            ret[x] = ParabolicCurve(p0[x], v0[x], a[x], t);
        return ret;
    }

    // Parabolic motion derivative applied to 3 dimensions
    private static Vector3 ParabolicCurveDeriv(Vector3 v0, Vector3 a, float t)
    {
        Vector3 ret = new Vector3();
        for (int x = 0; x < 3; x++)
            ret[x] = ParabolicCurveDeriv(v0[x], a[x], t);
        return ret;
    }

    // Sample a bunch of points along a parabolic curve until you hit gnd.  At that point, cut off the parabola
    // p0: starting point of parabola
    // v0: initial parabola velocity
    // a: initial acceleration
    // dist: distance between sample points
    // points: number of sample points
    // gnd: height of the ground, in meters above y=0
    // outPts: List that will be populated by new points
    // normal: normal of hit point
    private static bool CalculateParabolicCurve(Vector3 p0, Vector3 v0, Vector3 a, float dist, int points, ViveNavMesh nav, List<Vector3> outPts, out Vector3 normal)
    {
        outPts.Clear();
        outPts.Add(p0);

        Vector3 last = p0;
        float t = 0;

        for(int i=0; i< points; i++)
        {
            t += dist / ParabolicCurveDeriv(v0, a, t).magnitude;
            Vector3 next = ParabolicCurve(p0, v0, a, t);

            Vector3 castHit;
            Vector3 norm;
            bool endOnNavmesh;
            bool cast = nav.Linecast(last, next, out endOnNavmesh, out castHit, out norm);
            if (cast)
            {
                outPts.Add(castHit);
                normal = norm;
                return endOnNavmesh;
            }
            else
                outPts.Add(next);

            last = next;
        }

        normal = Vector3.up;
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

        if(SelectionPadPrefab != null)
        {
            SelectionPadObject = Instantiate<GameObject>(SelectionPadPrefab);
            SelectionPadObject.SetActive(false);
        }

        if (InvalidPadPrefab != null)
        {
            InvalidPadObject = Instantiate<GameObject>(InvalidPadPrefab);
            InvalidPadObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        if(SelectionPadObject != null)
            SelectionPadObject.SetActive(false);
        if(InvalidPadObject != null)
            InvalidPadObject.SetActive(false);
    }

    private List<Vector3> ParabolaPoints;

    void Update()
    {
        // 1. Calculate Parabola Points
        Vector3 velocity = transform.TransformDirection(InitialVelocity);
        Vector3 velocity_normalized;
        CurrentParabolaAngleY = ClampInitialVelocity(ref velocity, out velocity_normalized);
        CurrentPointVector = velocity_normalized;

        Vector3 normal;
        PointOnNavMesh = CalculateParabolicCurve(
            transform.position,
            velocity,
            Acceleration, PointSpacing, PointCount,
            NavMesh,
            ParabolaPoints,
            out normal);

        SelectedPoint = ParabolaPoints[ParabolaPoints.Count-1];

        // 2. Render Parabola graphics
        if(SelectionPadObject != null)
        {
            SelectionPadObject.SetActive(PointOnNavMesh);
            SelectionPadObject.transform.position = SelectedPoint + Vector3.one * 0.005f;
            if(PointOnNavMesh)
            {
                SelectionPadObject.transform.rotation = Quaternion.LookRotation(normal);
                SelectionPadObject.transform.Rotate(90, 0, 0);
            }
        }
        if(InvalidPadObject != null)
        {
            InvalidPadObject.SetActive(!PointOnNavMesh);
            InvalidPadObject.transform.position = SelectedPoint + Vector3.one * 0.005f;
            if (!PointOnNavMesh)
            {
                InvalidPadObject.transform.rotation = Quaternion.LookRotation(normal);
                InvalidPadObject.transform.Rotate(90, 0, 0);
            }
        }

        // Draw parabola (BEFORE the outside faces of the selection pad, to avoid depth issues)
        GenerateMesh(ref ParabolaMesh, ParabolaPoints, velocity, Time.time % 1);
        Graphics.DrawMesh(ParabolaMesh, Matrix4x4.identity, GraphicMaterial, gameObject.layer);
    }
    
    // Used when you can't depend on Update() to automatically update CurrentParabolaAngle
    // (for example, directly after enabling the component)
    public void ForceUpdateCurrentAngle()
    {
        Vector3 velocity = transform.TransformDirection(InitialVelocity);
        Vector3 d;
        CurrentParabolaAngleY = ClampInitialVelocity(ref velocity, out d);
        CurrentPointVector = d;
    }

    // Clamps the given velocity vector so that it can't be more than 45 degrees above the horizontal.
    // This is done so that it is easier to leverage the maximum distance (at the 45 degree angle) of
    // parabolic motion.
    //
    // Returns angle with reference to the XZ plane
    private float ClampInitialVelocity(ref Vector3 velocity, out Vector3 velocity_normalized) {
        // Project the initial velocity onto the XZ plane.  This gives us the "forward" direction
        Vector3 velocity_fwd = ProjectVectorOntoPlane(Vector3.up, velocity);

        // Find the angle between the XZ plane and the velocity
        float angle = Vector3.Angle(velocity_fwd, velocity);
        // Calculate positivity/negativity of the angle using the cross product
        // Below is "right" from controller's perspective (could also be left, but it doesn't matter for our purposes)
        Vector3 right = Vector3.Cross(Vector3.up, velocity_fwd);
        // If the cross product between forward and the velocity is in the same direction as right, then we are below the vertical
        if (Vector3.Dot(right, Vector3.Cross(velocity_fwd, velocity)) > 0)
            angle *= -1;

        // Clamp the angle if it is greater than 45 degrees
        if(angle > 45) {
            velocity = Vector3.Slerp(velocity_fwd, velocity, 45f / angle);
            velocity /= velocity.magnitude;
            velocity_normalized = velocity;
            velocity *= InitialVelocity.magnitude;
            angle = 45;
        } else
            velocity_normalized = velocity.normalized;

        return angle;
    }

#if UNITY_EDITOR
    private List<Vector3> ParabolaPoints_Gizmo;

    void OnDrawGizmos()
    {
        if (Application.isPlaying) // Otherwise the parabola can show in the game view
            return;

        if (ParabolaPoints_Gizmo == null)
            ParabolaPoints_Gizmo = new List<Vector3>(PointCount);

        Vector3 velocity = transform.TransformDirection(InitialVelocity);
        Vector3 velocity_normalized;
        CurrentParabolaAngleY = ClampInitialVelocity(ref velocity, out velocity_normalized);

        Vector3 normal;
        bool didHit = CalculateParabolicCurve(
            transform.position, 
            velocity, 
            Acceleration, PointSpacing, PointCount, 
            NavMesh,
            ParabolaPoints_Gizmo, out normal);

        Gizmos.color = Color.blue;
        for (int x = 0; x < ParabolaPoints_Gizmo.Count - 1; x++)
            Gizmos.DrawLine(ParabolaPoints_Gizmo[x], ParabolaPoints_Gizmo[x + 1]);
        Gizmos.color = Color.green;

        if(didHit)
            Gizmos.DrawSphere(ParabolaPoints_Gizmo[ParabolaPoints_Gizmo.Count-1], 0.2f);
    }
#endif
}
