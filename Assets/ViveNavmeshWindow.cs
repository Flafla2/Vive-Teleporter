using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ViveNavmeshWindow : EditorWindow {

    [MenuItem("Window/Vive Navmesh Generator")]
    static void Init()
    {
        ViveNavmeshWindow window = EditorWindow.GetWindow(typeof(ViveNavmeshWindow)) as ViveNavmeshWindow;
        window.Show();
    }

    private LineRenderer line1;
    private LineRenderer line2;
    private LineRenderer line3;
    private LineRenderer line4;
    private MeshFilter filter;

    void OnGUI()
    {
        filter = EditorGUILayout.ObjectField(filter, typeof(MeshFilter), true) as MeshFilter;
        if(GUILayout.Button("Test NavMesh to Mesh"))
        {
            Mesh m = ConvertNavmeshToMesh(NavMesh.CalculateTriangulation(), 0);
            
            filter.mesh = m;
        }
        line1 = EditorGUILayout.ObjectField(line1, typeof(LineRenderer), true) as LineRenderer;
        line2 = EditorGUILayout.ObjectField(line2, typeof(LineRenderer), true) as LineRenderer;
        line3 = EditorGUILayout.ObjectField(line3, typeof(LineRenderer), true) as LineRenderer;
        line4 = EditorGUILayout.ObjectField(line4, typeof(LineRenderer), true) as LineRenderer;
        if(GUILayout.Button("Test Border Edge Finder"))
        {
            Mesh m = ConvertNavmeshToMesh(NavMesh.CalculateTriangulation(), 0);
            Vector3[][] border = FindBorderEdges(m);
            line1.SetVertexCount(border[0].Length);
            line1.SetPositions(border[0]);
            line2.SetVertexCount(border[1].Length);
            line2.SetPositions(border[1]);
            line3.SetVertexCount(border[2].Length);
            line3.SetPositions(border[2]);
            line4.SetVertexCount(border[3].Length);
            line4.SetPositions(border[3]);
        }
    }

    // Converts a NavMesh (or a NavMesh area) into a standard Unity mesh.  This is later used
    // to render the mesh on-screen using Unity's standard rendering tools.
    // 
    // navMesh: Precalculated Nav Mesh Triangulation
    // area: area to consider in calculation
    private static Mesh ConvertNavmeshToMesh(NavMeshTriangulation navMesh, int area)
    {
        Mesh ret = new Mesh();

        Vector3[] vertices = new Vector3[navMesh.vertices.Length];
        for (int x = 0; x < vertices.Length; x++)
            vertices[x] = navMesh.vertices[x];

        int[] triangles = new int[navMesh.indices.Length];
        for (int x = 0; x < triangles.Length; x++)
            triangles[x] = navMesh.indices[x];

        ret.name = "Navmesh";
        ret.vertices = vertices;
        ret.triangles = triangles;

        ret.RecalculateNormals();
        ret.RecalculateBounds();

        return ret;
    }

    // Given some mesh m, calculates a number of polylines that border the mesh.  This may return more than
    // one polyline if, for example, the mesh has holes in it or if the mesh is separated in two pieces.
    //
    // m: input mesh
    // returns: array of cyclic polylines
    private static Vector3[][] FindBorderEdges(Mesh m)
    {
        // First, get together all the edges in the mesh and find out
        // how many times each edge is used.  Edges that are only used
        // once are border edges.
        Dictionary<Edge, int> edges = new Dictionary<Edge, int>();
        for (int x = 0; x < m.triangles.Length / 3; x++)
        {
            int p1 = m.triangles[x * 3];
            int p2 = m.triangles[x * 3 + 1];
            int p3 = m.triangles[x * 3 + 2];

            Edge[] e = new Edge[3];
            e[0] = new Edge(p1, p2);
            e[1] = new Edge(p2, p3);
            e[2] = new Edge(p3, p1);

            foreach (Edge d in e) {
                int curval;
                edges.TryGetValue(d, out curval); // 0 if nonexistant
                edges[d] = curval + 1;
            }
        }

        // Next, consolidate all of the border edges into one List<Edge>
        List<Edge> border = new List<Edge>();
        foreach (KeyValuePair<Edge, int> p in edges)
        {
            if (p.Value == 1)
                border.Add(p.Key);
        }

        // Perform the following routine:
        // 1. Pick any unvisited edge segment [v_start,v_next] and add these vertices to the polygon loop.
        // 2. Find the unvisited edge segment [v_i,v_j] that has either v_i = v_next or v_j = v_next and add the 
        //    other vertex (the one not equal to v_next) to the polygon loop. Reset v_next as this newly added vertex, 
        //    mark the edge as visited and continue from 2.
        // 3. Traversal is done when we get back to v_start.
        // Source: http://stackoverflow.com/questions/14108553/get-border-edges-of-mesh-in-winding-order
        bool[] visited = new bool[border.Count];
        bool finished = false;
        int cur_index = 0;

        List<Vector3[]> ret = new List<Vector3[]>();

        while(!finished)
        {
            int[] raw = FindPolylineFromEdges(cur_index, visited, border);
            Vector3[] fmt = new Vector3[raw.Length];
            for (int x = 0; x < raw.Length; x++)
                fmt[x] = m.vertices[raw[x]];
            ret.Add(fmt);

            finished = true;
            for (int x=0;x<visited.Length;x++) {
                if (!visited[x])
                {
                    cur_index = x;
                    finished = false;
                    break;
                }
            }
        }

        return ret.ToArray();
    }

    // Given a list of edges, finds a polyline connected to the edge at index start.
    // Guaranteed to run in O(n) time.
    // 
    // start: starting index of edge
    // visited: tally of visited edges (perhaps from previous calls)
    // edges: list of edges
    private static int[] FindPolylineFromEdges(int start, bool[] visited, List<Edge> edges)
    {
        List<int> loop = new List<int>(edges.Count);
        loop.Add(edges[start].min);
        loop.Add(edges[start].max);
        visited[start] = true;

        while (loop[loop.Count - 1] != edges[start].min)
        {
            int cur = loop[loop.Count - 1];
            bool found = false;
            for (int x = 0; x < visited.Length; x++)
            {
                // If we have visited this edge before, or both vertices on this edge
                // aren't connected to cur, then skip the edge
                if (!visited[x] && (edges[x].min == cur || edges[x].max == cur))
                {
                    // The next vertex in the loop
                    int next = edges[x].min == cur ? edges[x].max : edges[x].min;
                    loop.Add(next);

                    visited[x] = true;
                    found = true;
                    break;
                }
            }
            if (!found) // acyclic, so break.
                break;
        }

        int[] ret = new int[loop.Count];
        loop.CopyTo(ret);
        return ret;
    }

    private struct Edge
    {
        public int min
        {
            get; private set;
        }
        public int max
        {
            get; private set;
        }

        public Edge(int p1, int p2)
        {
            // This is done so that if p1 and p2 are switched, the edge has the same hash
            min = p1 < p2 ? p1 : p2;
            max = p1 > p2 ? p1 : p2;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Edge))
                return false;

            Edge e = (Edge)obj;
            return e.min == min && e.max == max;
        }

        public override int GetHashCode()
        {
            // Small note: this breaks when you have more than 65535 vertices.
            return (min << 16) + max;
        }
    }

}
