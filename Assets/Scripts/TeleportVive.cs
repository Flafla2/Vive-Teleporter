using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TeleportVive : MonoBehaviour {

    public ParabolicPointer Pointer;
    public Transform OriginTransform;
    public Transform CameraTransform;

    public float TeleportFadeDuration = 0.2f;

    public Material FadeMaterial;
    private int MaterialFadeID;

    public SteamVR_TrackedObject[] Controllers;
    private SteamVR_TrackedObject ActiveController;

    private bool Teleporting = false;
    private bool FadingIn = false;
    private float TeleportTimeMarker = -1;
    private Vector3 TeleportDestination;

    private Mesh PlaneMesh;
    private Camera cam;

    void Start()
    {
        Pointer.enabled = false;

        PlaneMesh = new Mesh();
        Vector3[] verts = new Vector3[]
        {
            new Vector3(-1, -1, 0),
            new Vector3(-1, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, -1, 0)
        };
        int[] elts = new int[] { 0, 1, 2, 0, 2, 3 };
        PlaneMesh.vertices = verts;
        PlaneMesh.triangles = elts;
        PlaneMesh.RecalculateBounds();

        cam = GetComponent<Camera>();

        MaterialFadeID = Shader.PropertyToID("_Fade");
    }

    void OnPostRender()
    {
        if(Teleporting)
        {
            float alpha = Mathf.Clamp01((Time.time - TeleportTimeMarker) / (TeleportFadeDuration / 2));
            if (FadingIn)
                alpha = 1 - alpha;

            Matrix4x4 local = Matrix4x4.TRS(Vector3.forward * 0.3f, Quaternion.identity, Vector3.one);
            FadeMaterial.SetPass(0);
            FadeMaterial.SetFloat(MaterialFadeID, alpha);
            Graphics.DrawMeshNow(PlaneMesh, transform.localToWorldMatrix * local);
        }
    }

	void Update () {
        if(Teleporting)
        {
            if(Time.time - TeleportTimeMarker >= TeleportFadeDuration / 2)
            {
                if(FadingIn)
                {
                    Teleporting = false;
                } else
                {
                    Vector3 offset = CameraTransform.position - OriginTransform.position;
                    offset.y = 0;
                    OriginTransform.position = Pointer.SelectedPoint + offset;
                }

                TeleportTimeMarker = Time.time;
                FadingIn = !FadingIn;
            }

            return;
        }

        if (ActiveController != null)
        {
            int index = (int)ActiveController.index;
            var device = SteamVR_Controller.Input(index);
            if (device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                if(Pointer.PointOnNavMesh)
                {
                    Teleporting = true;
                    TeleportDestination = Pointer.SelectedPoint;
                    TeleportTimeMarker = Time.time;
                }
                
                ActiveController = null;
                Pointer.enabled = false;

                Pointer.transform.parent = null;
                Pointer.transform.position = Vector3.zero;
                Pointer.transform.rotation = Quaternion.identity;
                Pointer.transform.localScale = Vector3.one;
            }
        } else
        {
            foreach (SteamVR_TrackedObject obj in Controllers)
            {
                int index = (int)obj.index;
                if (index == -1)
                    continue;

                var device = SteamVR_Controller.Input(index);
                if (device.GetPressDown(SteamVR_Controller.ButtonMask.Touchpad))
                {
                    ActiveController = obj;

                    Pointer.transform.parent = obj.transform;
                    Pointer.transform.localPosition = Vector3.zero;
                    Pointer.transform.localRotation = Quaternion.identity;
                    Pointer.transform.localScale = Vector3.one;
                    Pointer.enabled = true;
                }
            }
        }
	}
}
