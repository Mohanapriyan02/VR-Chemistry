using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform cam;

    void Start()
    {
        // Cache camera reference (important for performance)
        cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Make the UI face the camera
        transform.LookAt(transform.position + cam.forward);
    }
}