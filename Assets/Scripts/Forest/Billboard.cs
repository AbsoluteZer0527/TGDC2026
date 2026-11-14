using UnityEngine;

public class Billboard : MonoBehaviour
{
    Camera cam;

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;
        Vector3 f = cam.transform.forward;
        f.y = 0;                                    // ignore looking up/down
        if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f);
    }
}