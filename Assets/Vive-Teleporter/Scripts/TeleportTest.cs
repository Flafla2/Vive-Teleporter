using UnityEngine;

/// \brief A test script for "The Lab" style teleportation if you don't have a Vive.  Keep in mind that this 
///        doesn't have fade in/out, whereas TeleportVive (a version of this specifically made for the Vive) does.
/// \sa TeleportVive
[AddComponentMenu("Vive Teleporter/Test/Teleporter Test (No SteamVR)")]
public class TeleportTest : MonoBehaviour {

    public Camera LookCamera;
    public ParabolicPointer Pointer;
    public Transform Controller;

    public float MovementSpeed = 1;
    public float LookSensitivity = 10;
    public float PointSensitivity = 10;

    public GUIStyle InstructionsStyle;

    private float pointer_pitch = 0;
    private float pointer_yaw = 0;
	
	void Update () {
        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized * MovementSpeed;
        move = transform.TransformDirection(move);
        transform.Translate(move * Time.deltaTime);

        if(Input.GetButton("Switch Control"))
        {
            transform.Rotate(0, Input.GetAxis("Mouse X") * LookSensitivity, 0);
            LookCamera.transform.Rotate(-Input.GetAxis("Mouse Y") * LookSensitivity, 0, 0);
        } else
        {
            pointer_pitch += -Input.GetAxis("Mouse Y") * PointSensitivity;
            pointer_yaw += Input.GetAxis("Mouse X") * PointSensitivity;
            Controller.localRotation = Quaternion.Euler(pointer_pitch, pointer_yaw, 0);
        }

        if(Input.GetButtonDown("Click") && Pointer.PointOnNavMesh)
            transform.position = Pointer.SelectedPoint;
	}

    void OnGUI()
    {
        GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 60, 300, 50), "Hold ALT to turn camera, Click to teleport, Mouse to rotate controller/camera", InstructionsStyle);
    }
}
