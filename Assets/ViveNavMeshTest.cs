using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class ViveNavMeshTest : MonoBehaviour {

    public ViveNavMesh TestMesh;
    private float dist = -1;
	
	void Update () {
        if (TestMesh != null)
            dist = TestMesh.Raycast(new Ray(transform.position, transform.forward));
        else
            dist = -1;
	}

    void OnDrawGizmos()
    {
        float drawdist = dist == -1 ? 1000 : dist;
        Vector3 hit = transform.position + transform.forward * drawdist;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, hit);
        Gizmos.color = Color.red;
        if (dist != -1)
            Gizmos.DrawSphere(hit, 0.2f);
        Gizmos.color = Color.white;
    }
}
