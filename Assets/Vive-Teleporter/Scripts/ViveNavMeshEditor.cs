#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

/// \brief Custom inspector for ViveNavMesh.  This handles the conversion from Unity NavMesh to Mesh and performs some
///        computational geometry to find the borders of the mesh.
[CustomEditor(typeof(ViveNavMesh))]
public class ViveNavMeshEditor : Editor {

    private SerializedProperty p_area;
    private SerializedProperty p_mesh;
    private SerializedProperty p_material;
    private SerializedProperty p_alpha;
    private SerializedProperty p_layer_mask;
    private SerializedProperty p_ignore_layer_mask;
    private SerializedProperty p_query_trigger_interaction;
    private SerializedProperty p_sample_radius;
    private SerializedProperty p_ignore_sloped_surfaces;
    private SerializedProperty p_dewarp_method;

    void OnEnable()
    {
        p_area = serializedObject.FindProperty("_NavAreaMask");
        p_mesh = serializedObject.FindProperty("_SelectableMesh");
        p_material = serializedObject.FindProperty("_GroundMaterialSource");
        p_alpha = serializedObject.FindProperty("GroundAlpha");
        p_layer_mask = serializedObject.FindProperty("_LayerMask");
        p_ignore_layer_mask = serializedObject.FindProperty("_IgnoreLayerMask");
        p_query_trigger_interaction = serializedObject.FindProperty("_QueryTriggerInteraction");
        p_sample_radius = serializedObject.FindProperty("_SampleRadius");
        p_ignore_sloped_surfaces = serializedObject.FindProperty("_IgnoreSlopedSurfaces");
        p_dewarp_method = serializedObject.FindProperty("_DewarpingMethod");
    }

    public override void OnInspectorGUI()
    {
        GUIStyle bold_wrap = EditorStyles.boldLabel;
        bold_wrap.wordWrap = true;
        GUILayout.Label("Navmesh Preprocessor for HTC Vive Locomotion", bold_wrap);
        GUILayout.Label("Adrian Biagioli 2017", EditorStyles.miniLabel);

        GUILayout.Label("Before Using", bold_wrap);
        GUIStyle wrap = EditorStyles.label;
        wrap.wordWrap = true;
        GUILayout.Label(
            "Make sure you bake a Navigation Mesh (NavMesh) in Unity before continuing (Window > Navigation).  When you "+
            "are done, click \"Update Navmesh Data\" below.  This will update the graphic of the playable area "+
            "that the player will see in-game.\n",
            wrap);

        ViveNavMesh mesh = (ViveNavMesh)target;

        serializedObject.Update();

        // Area Mask //
        string[] areaNames = GameObjectUtility.GetNavMeshAreaNames();
        int[] area_index = new int[areaNames.Length];
        int temp_mask = 0;
        for (int x = 0; x < areaNames.Length; x++)
        {
            area_index[x] = GameObjectUtility.GetNavMeshAreaFromName(areaNames[x]);
            temp_mask |= ((p_area.intValue >> area_index[x]) & 1) << x;
        }
        EditorGUI.BeginChangeCheck();
        temp_mask = EditorGUILayout.MaskField("Area Mask", temp_mask, areaNames);
        if(EditorGUI.EndChangeCheck())
        {
            p_area.intValue = 0;
            for(int x=0; x<areaNames.Length; x++)
                p_area.intValue |= (((temp_mask >> x) & 1) == 1 ? 0 : 1) << area_index[x];
            p_area.intValue = ~p_area.intValue;
        }
        serializedObject.ApplyModifiedProperties();

        // Sanity check for Null properties //
        bool HasMesh = (mesh.SelectableMesh != null && mesh.SelectableMesh.vertexCount != 0) || (mesh.SelectableMeshBorder != null && mesh.SelectableMeshBorder.Length != 0);

        // Fixes below error message popping up with prefabs.  Kind of hacky but gets the job done
        bool isPrefab = EditorUtility.IsPersistent(target);
        if (isPrefab && mesh.SelectableMesh == null)
            mesh.SelectableMesh = new Mesh();

        bool MeshNull = mesh.SelectableMesh == null;
        bool BorderNull = mesh.SelectableMeshBorder == null;

        if (MeshNull || BorderNull) {
            string str = "Internal Error: ";
            if (MeshNull)
                str += "Selectable Mesh == null.  ";
            if (BorderNull)
                str += "Border point array == null.  ";
            str += "This may lead to strange behavior or serialization.  Try updating the mesh or delete and recreate the Navmesh object.  ";
            str += "If you are able to consistently get a Vive Nav Mesh object into this state, please submit a bug report.";
            EditorGUILayout.HelpBox(str, MessageType.Error);
        }

        // Update / Clear Navmesh Data //
        if (GUILayout.Button("Update Navmesh Data"))
        {
            Undo.RecordObject(mesh, "Update Navmesh Data");

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

            Vector3[] verts = tri.vertices;
            int[] tris = tri.indices;
            int[] areas = tri.areas;

            int vert_size = verts.Length;
            int tri_size = tris.Length;
            RemoveMeshDuplicates(verts, tris, out vert_size, 0.01f);
            DewarpMesh(verts, mesh.DewarpingMethod, mesh.SampleRadius);
            CullNavmeshTriangulation(verts, tris, areas, p_area.intValue, mesh.IgnoreSlopedSurfaces, ref vert_size, ref tri_size);

            Mesh m = ConvertNavmeshToMesh(verts, tris, vert_size, tri_size);
            // Can't use SerializedProperties here because BorderPointSet doesn't derive from UnityEngine.Object
            mesh.SelectableMeshBorder = FindBorderEdges(m);

            serializedObject.Update();
            p_mesh.objectReferenceValue = m;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            mesh.SelectableMesh = mesh.SelectableMesh; // Make sure that setter is called
        }

        GUI.enabled = HasMesh;
        if(GUILayout.Button("Clear Navmesh Data"))
        {
            Undo.RecordObject(mesh, "Clear Navmesh Data");

            // Note: Unity does not serialize "null" correctly so we set everything to empty objects
            Mesh m = new Mesh();

            serializedObject.Update();
            p_mesh.objectReferenceValue = m;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            mesh.SelectableMesh = mesh.SelectableMesh; // Make sure setter is called

            mesh.SelectableMeshBorder = new BorderPointSet[0];
        }
        GUI.enabled = true;

        GUILayout.Label(HasMesh ? "Status: NavMesh Loaded" : "Status: No NavMesh Loaded");

        // Render Settings //
        EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(p_material);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mesh, "Change Ground Material");
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            mesh.GroundMaterial = new Material((Material)p_material.objectReferenceValue); // Reload material
        }

        EditorGUILayout.PropertyField(p_alpha);
        serializedObject.ApplyModifiedProperties();

        // Raycast Settings //
        EditorGUILayout.LabelField("Raycast Settings", EditorStyles.boldLabel);

        int temp_layer_mask = p_layer_mask.intValue;
        bool temp_ignore_layer_mask = p_ignore_layer_mask.boolValue;

        EditorGUI.BeginChangeCheck();
        temp_layer_mask = LayerMaskField("Layer Mask", temp_layer_mask);
        if(EditorGUI.EndChangeCheck())
        {
            p_layer_mask.intValue = temp_layer_mask;
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUI.BeginChangeCheck();
        temp_ignore_layer_mask = EditorGUILayout.Toggle("Ignore Layer Mask", temp_ignore_layer_mask);
        if(EditorGUI.EndChangeCheck())
        {
            p_ignore_layer_mask.boolValue = temp_ignore_layer_mask;
        }
        serializedObject.ApplyModifiedProperties();

        QueryTriggerInteraction temp_query_trigger_interaction = (QueryTriggerInteraction) p_query_trigger_interaction.intValue;

        EditorGUI.BeginChangeCheck();
        temp_query_trigger_interaction = (QueryTriggerInteraction) EditorGUILayout.EnumPopup("Query Trigger Interaction", (QueryTriggerInteraction) temp_query_trigger_interaction);
        if(EditorGUI.EndChangeCheck())
        {
            p_query_trigger_interaction.intValue = (int) temp_query_trigger_interaction;
        }
        serializedObject.ApplyModifiedProperties();

        // Navmesh Settings //
        EditorGUILayout.LabelField("Navmesh Settings", EditorStyles.boldLabel);
        GUILayout.Label(
            "Make sure the sample radius below is equal to your Navmesh Voxel Size (see Advanced > Voxel Size " +
            "in the navigation window).  Increase this if the selection disk is not appearing.",
            wrap);
        EditorGUILayout.PropertyField(p_sample_radius);
        EditorGUILayout.PropertyField(p_ignore_sloped_surfaces);
        EditorGUILayout.PropertyField(p_dewarp_method);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DewarpMesh(Vector3[] verts, NavmeshDewarpingMethod dw, float step)
    {
        if (dw == NavmeshDewarpingMethod.None)
            return;

        for (int x = 0; x < verts.Length; x++)
        {
            if (dw == NavmeshDewarpingMethod.RaycastDownward)
            {
                RaycastHit hit;

                // Have the raycast span over the entire navmesh voxel
                Vector3 sample = verts[x];
                double vy = Math.Round(verts[x].y / step) * step;
                sample.y = (float)vy;

                if (Physics.Raycast(sample, Vector3.down, out hit, (float)step + 0.01f))
                    verts[x] = hit.point;

            }
            else if (dw == NavmeshDewarpingMethod.RoundToVoxelSize)
            {
                // Clamp the point to the voxel grid in the Y direction
                double vy = Math.Round((verts[x].y - 0.05) / step) * step + 0.05;
                verts[x].y = (float)vy;
            }
        }
    }

    /// \brief Modifies the given NavMesh so that only the Navigation areas are present in the mesh.  This is done only 
    ///        by swapping, so that no new memory is allocated.
    /// 
    /// The data stored outside of the returned array sizes should be considered invalid and will contain garbage data.
    /// \param vertices vertices of Navmesh
    /// \param indices indices of Navmesh triangles
    /// \param areas Navmesh areas
    /// \param areaMask Area mask to include in returned mesh.  Areas outside of this mask are culled.
    /// \param vert_size New size of navMesh.vertices
    /// \param tri_size New size of navMesh.areas and one third of the size of navMesh.indices
    private static void CullNavmeshTriangulation(Vector3[] vertices, int[] indices, int[] areas, int areaMask, bool ignore_sloped_surfaces, ref int vert_size, ref int tri_size)
    {
        // Step 1: re-order triangles so that valid areas are in front.  Then determine tri_size.
        tri_size = indices.Length / 3;
        for(int i=0; i < tri_size; i++)
        {
            Vector3 p1 = vertices[indices[i * 3]];
            Vector3 p2 = vertices[indices[i * 3 + 1]];
            Vector3 p3 = vertices[indices[i * 3 + 2]];
            Plane p = new Plane(p1, p2, p3);
            bool vertical = Mathf.Abs(Vector3.Dot(p.normal, Vector3.up)) > 0.99f;

            // If the current triangle isn't flat (normal is up) or if it doesn't match
            // with the provided mask, we should cull it.
            if(((1 << areas[i]) & areaMask) == 0 || (ignore_sloped_surfaces && !vertical)) // If true this triangle should be culled.
            {
                // Swap area indices and triangle indices with the end of the array
                int t_ind = tri_size - 1;

                int t_area = areas[t_ind];
                areas[t_ind] = areas[i];
                areas[i] = t_area;

                for(int j=0;j<3;j++)
                {
                    int t_v = indices[t_ind * 3 + j];
                    indices[t_ind * 3 + j] = indices[i * 3 + j];
                    indices[i * 3 + j] = t_v;
                }

                // Then reduce the size of the array, effectively cutting off the previous triangle
                tri_size--;
                // Stay on the same index so that we can check the triangle we just swapped.
                i--;
            }
        }

        // Step 2: Cull the vertices that aren't used.
        vert_size = 0;
        for(int i=0; i < tri_size * 3; i++)
        {
            int prv = indices[i];
            if (prv >= vert_size)
            {
                int nxt = vert_size;

                // Bring the current vertex to the end of the "active" array by swapping it with what's currently there
                Vector3 t_v = vertices[prv];
                vertices[prv] = vertices[nxt];
                vertices[nxt] = t_v;

                // Now change around the values in the triangle indices to reflect the swap
                for(int j=i; j < tri_size * 3; j++)
                {
                    if (indices[j] == prv)
                        indices[j] = nxt;
                    else if (indices[j] == nxt)
                        indices[j] = prv;
                }

                // Increase the size of the vertex array to reflect the changes.
                vert_size++;
            }
        }
    }

    /// \brief Converts a NavMesh (or a NavMesh area) into a standard Unity mesh.  This is later used
    ///        to render the mesh on-screen using Unity's standard rendering tools.
    /// 
    /// \param navMesh Precalculated Nav Mesh Triangulation
    /// \param vert_size size of vertex array
    /// \param tri_size size of triangle array
    private static Mesh ConvertNavmeshToMesh(Vector3[] vraw, int[] iraw, int vert_size, int tri_size)
    {
        Mesh ret = new Mesh();

        if(vert_size >= 65535)
        {
            Debug.LogError("Playable NavMesh too big (vertex count >= 65535)!  Limit the size of the playable area using"+
                "Area Masks.  For now no preview mesh will render.");
            return ret;
        }

        Vector3[] vertices = new Vector3[vert_size];
        for (int x = 0; x < vertices.Length; x++) {
            vertices[x].x = vraw[x].x;
            vertices[x].y = vraw[x].y;
            vertices[x].z = vraw[x].z;
        }

        int[] triangles = new int[tri_size * 3];
        for (int x = 0; x < triangles.Length; x++)
            triangles[x] = iraw[x];

        ret.name = "Navmesh";

        ret.Clear();
        ret.vertices = vertices;
        ret.triangles = triangles;

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
    /// \param verts Vertex array to process
    /// \param elts Triangle indices array
    /// \param verts_size size of vertex array after processing
    /// \param threshold Threshold with which to combine vertices
    private static void RemoveMeshDuplicates(Vector3[] verts, int[] elts, out int verts_size, double threshold)
    {
        int size = verts.Length;
        for (int x = 0; x < size; x++)
        {
            for (int y = x + 1; y < size; y++)
            {
                Vector3 d = verts[x] - verts[y];

                if (x != y && Mathf.Abs(d.x) < threshold && Mathf.Abs(d.y) < threshold && Mathf.Abs(d.z) < threshold)
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

        verts_size = size;
    }

    /// \brief Given some mesh m, calculates a number of polylines that border the mesh.  This may return more than
    ///        one polyline if, for example, the mesh has holes in it or if the mesh is separated in two pieces.
    ///
    /// \param m input mesh
    /// \returns array of cyclic polylines
    private static BorderPointSet[] FindBorderEdges(Mesh m)
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

        BorderPointSet[] ret_set = new BorderPointSet[ret.Count];
        for (int x = 0; x < ret.Count; x++)
        {
            ret_set[x] = new BorderPointSet(ret[x]);
        }
        return ret_set;
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

    static List<int> layerNumbers = new List<int>();

    // From http://answers.unity3d.com/questions/42996/how-to-create-layermask-field-in-a-custom-editorwi.html
    static LayerMask LayerMaskField(string label, LayerMask layerMask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;

        layerNumbers.Clear();

        for (int i = 0; i < layers.Length; i++)
            layerNumbers.Add(LayerMask.NameToLayer(layers[i]));

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = UnityEditor.EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= (1 << layerNumbers[i]);
        }
        layerMask.value = mask;

        return layerMask;
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
#endif