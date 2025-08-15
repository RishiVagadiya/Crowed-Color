using Unity.Netcode;
using UnityEngine;

public class PLayer_Controller : NetworkBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        // सिर्फ अपना local player move होगा
        if (!IsOwner || Input.touchCount > 0) return;

        float move = Input.GetAxis("Vertical"); // Keyboard या Mobile joystick
        transform.Translate(Vector3.forward * move * moveSpeed * Time.deltaTime);
    }
}
