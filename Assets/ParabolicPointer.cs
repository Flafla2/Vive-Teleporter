using UnityEngine;
using System.Collections;

public class ParabolicPointer : MonoBehaviour {

    public Vector3 InitialVelocity = Vector3.forward * 10f;
    public Vector3 Acceleration = Vector3.up * -9.8f;
    public int PointCount = 10;
    public float PointSpacing = 0.5f;
    public float GroundPoint = 

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

    private delegate void LineDrawer(Vector3 p1, Vector3 p2);

	private static bool DrawParabolicCurve(Vector3 p0, Vector3 v0, Vector3 a, float dist, int points, Vector3 gndHit, LineDrawer draw)
    {
        Vector3 last = p0;
        float t = 0;
        for(int i=0; i<points; i++)
        {
            t += dist / ParabolicCurveDeriv(v0, a, t).magnitude;
            Vector3 next = ParabolicCurve(p0, v0, a, t);
            if (next.y < gndHit.y)
            {
                draw(last, gndHit);
                return true;
            } else
                draw(last, next);

            last = next;
        }

        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        DrawParabolicCurve(
            transform.position, 
            transform.TransformDirection(InitialVelocity), 
            Acceleration, PointSpacing, PointCount, Gizmos.DrawLine);
    }
}
