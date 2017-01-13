using UnityEngine;
using Valve.VR;

[AddComponentMenu("Vive Teleporter/Vive Teleporter")]
[RequireComponent(typeof(Camera), typeof(BorderRenderer))]
public class TeleportVive : MonoBehaviour {
    [Tooltip("Parabolic Pointer object to pull destination points from, and to assign to each controller.")]
    public ParabolicPointer Pointer;
    /// Origin of SteamVR tracking space
    [Tooltip("Origin of the SteamVR tracking space")]
    public Transform OriginTransform;
    /// Origin of the player's head
    [Tooltip("Transform of the player's head")]
    public Transform HeadTransform;
    
    /// How long, in seconds, the fade-in/fade-out animation should take
    [Tooltip("Duration of the \"blink\" animation (fading in and out upon teleport) in seconds.")]
    public float TeleportFadeDuration = 0.2f;
    /// Measure in degrees of how often the controller should respond with a haptic click.  Smaller value=faster clicks
    [Tooltip("The player feels a haptic pulse in the controller when they raise / lower the controller by this many degrees.  Lower value = faster pulses.")]
    public float HapticClickAngleStep = 10;

    /// BorderRenderer to render the chaperone bounds (when choosing a location to teleport to)
    private BorderRenderer RoomBorder;

    /// Animator used to fade in/out the teleport area.  This should have a boolean parameter "Enabled" where if true
    /// the selectable area is displayed on the ground.
    [SerializeField]
    [Tooltip("Animator with a boolean \"Enabled\" parameter that is set to true when the player is choosing a place to teleport.")]
    private Animator NavmeshAnimator;
    private int EnabledAnimatorID;

    /// Material used to render the fade in/fade out quad
    [Tooltip("Material used to render the fade in/fade out quad.")]
    [SerializeField]
    private Material FadeMaterial;
    private Material FadeMaterialInstance;
    private int MaterialFadeID;

    /// SteamVR controllers that should be polled.
    [Tooltip("Array of SteamVR controllers that may used to select a teleport destination.")]
    public SteamVR_TrackedObject[] Controllers;
    private SteamVR_TrackedObject ActiveController;

    /// Indicates the current use of teleportation.
    /// None: The player is not using teleportation right now
    /// Selecting: The player is currently selecting a teleport destination (holding down on touchpad)
    /// Teleporting: The player has selected a teleport destination and is currently teleporting now (fading in/out)
    public TeleportState CurrentTeleportState { get; private set; }

    private Vector3 LastClickAngle = Vector3.zero;
    private bool IsClicking = false;

    private bool FadingIn = false;
    private float TeleportTimeMarker = -1;

    private Mesh PlaneMesh;

    void Start()
    {
        // Disable the pointer graphic (until the user holds down on the touchpad)
        Pointer.enabled = false;

        // Ensure we mark the player as not teleporting
        CurrentTeleportState = TeleportState.None;

        // Standard plane mesh used for "fade out" graphic when you teleport
        // This way you don't need to supply a simple plane mesh in the inspector
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

        if(FadeMaterial != null)
            FadeMaterialInstance = new Material(FadeMaterial);
        // Set some standard variables
        MaterialFadeID = Shader.PropertyToID("_Fade");
        EnabledAnimatorID = Animator.StringToHash("Enabled");

        RoomBorder = GetComponent<BorderRenderer>();

        Vector3 p0, p1, p2, p3;
        if (GetChaperoneBounds(out p0, out p1, out p2, out p3))
        {
            // Rotate to match camera rig rotation
            var originRotationMatrix = Matrix4x4.TRS(Vector3.zero, OriginTransform.rotation, Vector3.one);

            BorderPointSet p = new BorderPointSet(new Vector3[] {
                originRotationMatrix * p0,
                originRotationMatrix * p1,
                originRotationMatrix * p2,
                originRotationMatrix * p3,
                originRotationMatrix * p0,
            });
            RoomBorder.Points = new BorderPointSet[]
            {
                p
            };
        }

        RoomBorder.enabled = false;
    }

    /// \brief Requests the chaperone boundaries of the SteamVR play area.  This doesn't work if you haven't performed
    ///        Room Setup.
    /// \param p0, p1, p2, p3 Points that make up the chaperone boundaries.
    /// 
    /// \returns If the play area retrieval was successful
    public static bool GetChaperoneBounds(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        var initOpenVR = (!SteamVR.active && !SteamVR.usingNativeSupport);
        if (initOpenVR)
        {
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);
        }

        var chaperone = OpenVR.Chaperone;
        HmdQuad_t rect = new HmdQuad_t();
        bool success = (chaperone != null) && chaperone.GetPlayAreaRect(ref rect);
        p0 = new Vector3(rect.vCorners0.v0, rect.vCorners0.v1, rect.vCorners0.v2);
        p1 = new Vector3(rect.vCorners1.v0, rect.vCorners1.v1, rect.vCorners1.v2);
        p2 = new Vector3(rect.vCorners2.v0, rect.vCorners2.v1, rect.vCorners2.v2);
        p3 = new Vector3(rect.vCorners3.v0, rect.vCorners3.v1, rect.vCorners3.v2);
        if (!success)
            Debug.LogWarning("Failed to get Calibrated Play Area bounds!  Make sure you have tracking first, and that your space is calibrated.");

        if (initOpenVR)
            OpenVR.Shutdown();

        return success;
    }

    void OnPostRender()
    {
        if(CurrentTeleportState == TeleportState.Teleporting)
        {
            // Perform the fading in/fading out animation, if we are teleporting.  This is essentially a triangle wave
            // in/out, and the user teleports when it is fully black.
            float alpha = Mathf.Clamp01((Time.time - TeleportTimeMarker) / (TeleportFadeDuration / 2));
            if (FadingIn)
                alpha = 1 - alpha;

            Matrix4x4 local = Matrix4x4.TRS(Vector3.forward * 0.3f, Quaternion.identity, Vector3.one);
            FadeMaterialInstance.SetPass(0);
            FadeMaterialInstance.SetFloat(MaterialFadeID, alpha);
            Graphics.DrawMeshNow(PlaneMesh, transform.localToWorldMatrix * local);
        }
    }

	void Update ()
    {
        // If we are currently teleporting (ie handling the fade in/out transition)...
        if(CurrentTeleportState == TeleportState.Teleporting)
        {
            // Wait until half of the teleport time has passed before the next event (note: both the switch from fade
            // out to fade in and the switch from fade in to stop the animation is half of the fade duration)
            if(Time.time - TeleportTimeMarker >= TeleportFadeDuration / 2)
            {
                if(FadingIn)
                {
                    // We have finished fading in
                    CurrentTeleportState = TeleportState.None;
                } else
                {
                    // We have finished fading out - time to teleport!
                    Vector3 offset = OriginTransform.position - HeadTransform.position;
                    offset.y = 0;
                    OriginTransform.position = Pointer.SelectedPoint + offset;
                }

                TeleportTimeMarker = Time.time;
                FadingIn = !FadingIn;
            }
        }
        // At this point, we are NOT actively teleporting.  So now we care about controller input.
        else if(CurrentTeleportState == TeleportState.Selecting)
        {
            Debug.Assert(ActiveController != null);

            // Here, there is an active controller - that is, the user is holding down on the trackpad.
            // Poll controller for pertinent button data
            int index = (int)ActiveController.index;
            var device = SteamVR_Controller.Input(index);
            bool shouldTeleport = device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad);
            bool shouldCancel = device.GetPressUp(SteamVR_Controller.ButtonMask.Grip);
            if (shouldTeleport || shouldCancel)
            {
                // If the user has decided to teleport (ie lets go of touchpad) then remove all visual indicators
                // related to selecting things and actually teleport
                // If the user has decided to cancel (ie squeezes grip button) then remove visual indicators and do nothing
                if (shouldTeleport && Pointer.PointOnNavMesh)
                {
                    // Begin teleport sequence
                    CurrentTeleportState = TeleportState.Teleporting;
                    TeleportTimeMarker = Time.time;
                }
                else
                    CurrentTeleportState = TeleportState.None;
                
                // Reset active controller, disable pointer, disable visual indicators
                ActiveController = null;
                Pointer.enabled = false;
                RoomBorder.enabled = false;
                //RoomBorder.Transpose = Matrix4x4.TRS(OriginTransform.position, Quaternion.identity, Vector3.one);
                if (NavmeshAnimator != null)
                    NavmeshAnimator.SetBool(EnabledAnimatorID, false);

                Pointer.transform.parent = null;
                Pointer.transform.position = Vector3.zero;
                Pointer.transform.rotation = Quaternion.identity;
                Pointer.transform.localScale = Vector3.one;
            } else
            {
                // The user is still deciding where to teleport and has the touchpad held down.
                // Note: rendering of the parabolic pointer / marker is done in ParabolicPointer
                Vector3 offset = HeadTransform.position - OriginTransform.position;
                offset.y = 0;

                // Render representation of where the chaperone bounds will be after teleporting
                RoomBorder.enabled = Pointer.PointOnNavMesh;
                RoomBorder.Transpose = Matrix4x4.TRS(Pointer.SelectedPoint - offset, Quaternion.identity, Vector3.one);

                // Haptic feedback click every [HaptickClickAngleStep] degrees
                if (Pointer.CurrentParabolaAngleY >= 45) // Don't click when at max degrees
                    LastClickAngle = Pointer.CurrentPointVector;

                float angleClickDiff = Vector3.Angle(LastClickAngle, Pointer.CurrentPointVector);
                if (IsClicking && Mathf.Abs(angleClickDiff) > HapticClickAngleStep)
                {
                    LastClickAngle = Pointer.CurrentPointVector;
                    if (Pointer.PointOnNavMesh)
                        device.TriggerHapticPulse();
                }

                // Trigger a stronger haptic pulse when "entering" a teleportable surface
                if (Pointer.PointOnNavMesh && !IsClicking)
                {
                    IsClicking = true;
                    device.TriggerHapticPulse(750);
                    LastClickAngle = Pointer.CurrentPointVector;
                }
                else if (!Pointer.PointOnNavMesh && IsClicking)
                    IsClicking = false;
            }
        }
        else //CurrentTeleportState == TeleportState.None
        {
            // At this point the user is not holding down on the touchpad at all or has canceled a teleport and hasn't
            // let go of the touchpad.  So we wait for the user to press the touchpad and enable visual indicators
            // if necessary.
            foreach (SteamVR_TrackedObject obj in Controllers)
            {
                int index = (int)obj.index;
                if (index == -1)
                    continue;

                var device = SteamVR_Controller.Input(index);
                if (device.GetPressDown(SteamVR_Controller.ButtonMask.Touchpad))
                {
                    // Set active controller to this controller, and enable the parabolic pointer and visual indicators
                    // that the user can use to determine where they are able to teleport.
                    ActiveController = obj;

                    Pointer.transform.parent = obj.transform;
                    Pointer.transform.localPosition = Vector3.zero;
                    Pointer.transform.localRotation = Quaternion.identity;
                    Pointer.transform.localScale = Vector3.one;
                    Pointer.enabled = true;

                    CurrentTeleportState = TeleportState.Selecting;
                    
                    if(NavmeshAnimator != null)
                        NavmeshAnimator.SetBool(EnabledAnimatorID, true);

                    Pointer.ForceUpdateCurrentAngle();
                    LastClickAngle = Pointer.CurrentPointVector;
                    IsClicking = Pointer.PointOnNavMesh;
                }
            }
        }
	}
}

/// \brief Represents the player's current use of the teleport machanic.
public enum TeleportState
{
    /// The player is not using teleportation right now
    None,
    /// The player is currently selecting a teleport destination (holding down on touchpad)
    Selecting,
    /// The player has selected a teleport destination and is currently teleporting now (fading in/out)
    Teleporting
}