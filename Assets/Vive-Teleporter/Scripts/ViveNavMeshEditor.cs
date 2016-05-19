using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

/// \brief Custom inspector for ViveNavMesh.  This handles the conversion from Unity NavMesh to Mesh and performs some
///        computational geometry to find the borders of the mesh.
[CustomEditor(typeof(ViveNavMesh))]
public class ViveNavMeshEditor : Editor {

    public override void OnInspectorGUI()
    {
        GUIStyle bold_wrap = EditorStyles.boldLabel;
        bold_wrap.wordWrap = true;
        GUILayout.Label("Navmesh Preprocessor for HTC Vive Locomotion", bold_wrap);
        GUILayout.Label("Adrian Biagioli 2016", EditorStyles.miniLabel);

        GUILayout.Label("Before Using", bold_wrap);
        GUIStyle wrap = EditorStyles.label;
        wrap.wordWrap = true;
        GUILayout.Label(
            "Change your navmesh settings (Window > Navigation) to where you want the player to be able to navigate and bake the navmesh.  "+
            "Then click \"Update Navmesh Data\" below.  You may change the navigation settings back and rebake after you have updated.\n\nRecommended Settings:\n" +
            "Agent Radius: 0.25\nAgent Height: 2\nMax Slope: 0\nStep Height: 0\nDrop Height: 0\nJump Distance: 0\n",
            wrap);

        ViveNavMesh mesh = (ViveNavMesh)target;

        if (GUILayout.Button("Update Navmesh Data"))
        {
            mesh.SelectableMesh = ConvertNavmeshToMesh(NavMesh.CalculateTriangulation(), 0);
            mesh.SelectableMeshBorder = FindBorderEdges(mesh.SelectableMesh);
        }

        EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);
        mesh.GroundMaterial = (Material)EditorGUILayout.ObjectField("Ground Material", mesh.GroundMaterial, typeof(Material), false);
        mesh.GroundAlpha = EditorGUILayout.Slider("Ground Alpha", mesh.GroundAlpha, 0, 1);
    }

    /// \brief Converts a NavMesh (or a NavMesh area) into a standard Unity mesh.  This is later used
    ///        to render the mesh on-screen using Unity's standard rendering tools.
    /// 
    /// \param navMesh Precalculated Nav Mesh Triangulation
    /// \param area area to consider in calculation
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

        RemoveMeshDuplicates(ret);

        ret.RecalculateNormals();
        ret.RecalculateBounds();

        return ret;
    }

    /// \brief VERY naive implementation of removing duplicate vertices in a mesh.  O(n^2).
    /// 
    /// This is necessary because Unity NavMeshes for some reason have a whole bunch of duplicate vertices (or vertices
    /// that are very close together).  So some processing needs to be done go get rid of these.
    /// 
    /// If this becomes an actual performance hog, consider changing this to sort the vertices first using a more
    /// optimized process O(n lg n) then removing adjacent duplicates.
    /// 
    /// \param m Mesh to remove duplicates from
    private static void RemoveMeshDuplicates(Mesh m)
    {
        Vector3[] verts = new Vector3[m.vertices.Length];
        for (int x = 0; x < verts.Length; x++)
            verts[x] = m.vertices[x];

        int[] elts = new int[m.triangles.Length];
        for (int x = 0; x < elts.Length; x++)
            elts[x] = m.triangles[x];

        int size = verts.Length;
        for (int x = 0; x < size; x++)
        {
            for (int y = x + 1; y < size; y++)
            {
                Vector3 d = verts[x] - verts[y];
                d.x = Mathf.Abs(d.x);
                d.y = Mathf.Abs(d.y);
                d.z = Mathf.Abs(d.z);
                if (x != y && d.x < 0.05f && d.y < 0.05f && d.z < 0.05f)
                {
                    verts[y] = verts[size - 1];
                    for (int z = 0; z < elts.Length; z++)
                    {
                        if (elts[z] == y)
                            elts[z] = x;

                        if (elts[z] == size - 1)
                            elts[z] = y;
                    }
                    size--;
                    y--;
                }
            }
        }
        
        Array.Resize<Vector3>(ref verts, size);
        m.Clear();
        m.vertices = verts;
        m.triangles = elts;
    }

    /// \brief Given some mesh m, calculates a number of polylines that border the mesh.  This may return more than
    ///        one polyline if, for example, the mesh has holes in it or if the mesh is separated in two pieces.
    ///
    /// \param m input mesh
    /// \returns array of cyclic polylines
    private static Vector3[][] FindBorderEdges(Mesh m)
    {
        // First, get together all the edges in the mesh and find out
        // how many times each edge is used.  Edges that are only used
        // once are border edges.

        // Key: edges (note that because of how the hashcode / equals() is set up, two equivalent edges will effectively
        //      be equal)
        // Value: How many times this edge shows up in the mesh.  Any keys with a value of 1 are border edges.
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
            if (p.Value == 1) // border edge == edge only used once.
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

    /// Given a list of edges, finds a polyline connected to the edge at index start.
    /// Guaranteed to run in O(n) time.  Assumes that each edge only has two neighbor edges.
    /// 
    /// \param start starting index of edge
    /// \param visited tally of visited edges (perhaps from previous calls)
    /// \param edges list of edges
    private static int[] FindPolylineFromEdges(int start, bool[] visited, List<Edge> edges)
    {
        List<int> loop = new List<int>(edges.Count);
        loop.Add(edges[start].min);
        loop.Add(edges[start].max);
        visited[start] = true;

        // With each iteration of this while loop, we look for an edge that connects to the previous one
        // but hasn't been processed yet (to prevent simply finding the previous edge again, and to prevent
        // a hang if faulty data is given).
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

                    // Mark the edge as visited and continue the outermost loop
                    visited[x] = true;
                    found = true;
                    break;
                }
            }
            if (!found)// acyclic, so break.
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
            max = p1 >= p2 ? p1 : p2;
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
