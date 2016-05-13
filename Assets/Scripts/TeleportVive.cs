using UnityEngine;
using Valve.VR;

[RequireComponent(typeof(Camera), typeof(BorderRenderer))]
public class TeleportVive : MonoBehaviour {

    public ParabolicPointer Pointer;
    public Transform OriginTransform;
    public Transform CameraTransform;

    public float TeleportFadeDuration = 0.2f;
    public float HapticClickAngleStep = 10;

    public ViveNavMesh Navmesh;
    private BorderRenderer NavmeshBorder;
    private BorderRenderer RoomBorder;

    [SerializeField]
    private Animator NavmeshAnimator;
    private int EnabledAnimatorID;

    public Material FadeMaterial;
    private int MaterialFadeID;

    public SteamVR_TrackedObject[] Controllers;
    private SteamVR_TrackedObject ActiveController;

    private float LastClickAngle = 0;

    private bool Teleporting = false;
    private bool FadingIn = false;
    private float TeleportTimeMarker = -1;
    private Vector3 TeleportDestination;

    private Mesh PlaneMesh;
    private Camera cam;

    void Start()
    {
        // Disable the pointer graphic (until the user holds down on the touchpad)
        Pointer.enabled = false;

        // Standard plane mesh used for "fade out" graphic when you teleport
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

        // Set some standard variables
        cam = GetComponent<Camera>();

        MaterialFadeID = Shader.PropertyToID("_Fade");
        EnabledAnimatorID = Animator.StringToHash("Enabled");

        NavmeshBorder = Navmesh.GetComponent<BorderRenderer>();
        RoomBorder = GetComponent<BorderRenderer>();
        RoomBorder.enabled = false;

        // Sample the vive chaperone bounds
        float w = 0;
        float h = 0;

        if (GetSoftBounds(ref w, ref h))
        {
            w /= 2;
            h /= 2;
            RoomBorder.Points = new Vector3[][]
            {
                new Vector3[] {
                    new Vector3(w, 0, h),
                    new Vector3(w, 0, -h),
                    new Vector3(-w, 0, -h),
                    new Vector3(-w, 0, h),
                    new Vector3(w, 0, h)
                }
            };
        }
            

    }

    public static bool GetSoftBounds(ref float width, ref float height)
    {
        var initOpenVR = (!SteamVR.active && !SteamVR.usingNativeSupport);
        if (initOpenVR)
        {
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);
        }

        var chaperone = OpenVR.Chaperone;
        bool success = (chaperone != null) && chaperone.GetPlayAreaSize(ref width, ref height);
        if (!success)
            Debug.LogWarning("Failed to get Calibrated Play Area bounds!  Make sure you have tracking first, and that your space is calibrated.");

        if (initOpenVR)
            OpenVR.Shutdown();

        return success;
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
                    Vector3 offset = OriginTransform.position - CameraTransform.position;
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
                RoomBorder.enabled = false;
                if (NavmeshAnimator != null)
                    NavmeshAnimator.SetBool(EnabledAnimatorID, false);

                Pointer.transform.parent = null;
                Pointer.transform.position = Vector3.zero;
                Pointer.transform.rotation = Quaternion.identity;
                Pointer.transform.localScale = Vector3.one;
            } else
            {
                Vector3 offset = CameraTransform.position - OriginTransform.position;
                offset.y = 0;

                RoomBorder.Transpose = Matrix4x4.TRS(Pointer.SelectedPoint - offset, Quaternion.identity, Vector3.one);

                // Haptic feedback click every [HaptickClickAngleStep] degrees
                float angleClickDiff = Pointer.CurrentParabolaAngle - LastClickAngle;
                if(Mathf.Abs(angleClickDiff) > HapticClickAngleStep)
                {
                    LastClickAngle = Pointer.CurrentParabolaAngle;
                    device.TriggerHapticPulse();
                }
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
                    RoomBorder.enabled = true;
                    if(NavmeshAnimator != null)
                        NavmeshAnimator.SetBool(EnabledAnimatorID, true);

                    Pointer.ForceUpdateCurrentAngle();
                    LastClickAngle = Pointer.CurrentParabolaAngle;
                }
            }
        }
	}
}
