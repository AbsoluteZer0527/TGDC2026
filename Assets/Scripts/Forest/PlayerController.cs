using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraPivot;          // Main Camera, child of Player
    public float moveSpeed = 6f;
    public float mouseSensitivity = 0.1f;
    public float maxPitch = 60f;
    public float MoveSpeedMultiplier { get; set; } = 1f;

    public bool CanAct =>
        !CurtainTransition.IsPlaying && Cursor.lockState == CursorLockMode.Locked &&
        !(GameManager.I != null && GameManager.I.DayOver);

    CharacterController cc;
    float vy, pitch;

    void Awake()     => cc = GetComponent<CharacterController>();
    void OnEnable()  => SetCursorLocked(true);
    void OnDisable()
    {
        SetCursorLocked(false);   // frees the mouse when you go back to the kitchen
        SetRunning(false);
    }

    bool running;
    void SetRunning(bool on)
    {
        if (on == running) return;
        running = on;
        if (on) AudioManager.Instance?.PlayLoopingSFX("running");
        else AudioManager.Instance?.StopLoopingSFX("running");
    }

    void Update()
    {
        var k = Keyboard.current; var m = Mouse.current;
        if (k == null || m == null) return;

        if (PauseMenu.IsOpen || (GameManager.I != null && GameManager.I.DayOver)) { SetCursorLocked(false); SetRunning(false); return; }
        if (k.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        if (m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
        if (!CanAct) { SetRunning(false); return; }

        // Look
        Vector2 look = m.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);

        // Move relative to facing
        float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
        float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
        Vector3 move = (transform.right * x + transform.forward * z).normalized * (moveSpeed * MoveSpeedMultiplier);
        vy = cc.isGrounded ? -2f : vy + Physics.gravity.y * Time.deltaTime;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
        SetRunning(cc.isGrounded && (x != 0f || z != 0f));
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
