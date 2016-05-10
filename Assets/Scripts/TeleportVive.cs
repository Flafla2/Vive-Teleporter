using UnityEngine;

public class TeleportVive : MonoBehaviour {

    public ParabolicPointer Pointer;
    public Transform Controller;
    public Transform CameraTransform;

    public SteamVR_TrackedObject TrackedObj;
    	
	void FixedUpdate () {
        int index = (int)TrackedObj.index;
        if (index == -1)
            return;
        var device = SteamVR_Controller.Input(index);
        if (device.GetTouchDown(SteamVR_Controller.ButtonMask.Trigger) && Pointer.PointOnNavMesh)
        {
            Vector3 offset = CameraTransform.position - transform.position;
            offset.y = 0;
            transform.position = Pointer.SelectedPoint + offset;
        }
	}
}
