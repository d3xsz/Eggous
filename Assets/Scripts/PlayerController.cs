using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float moveSpeed = 6f;
    [HideInInspector] public float envSpeed = 1f, envJump = 1f; // su vb. cevre etkisi (LakeWater)
    [HideInInspector] public float jetUntil = -1f;   // Civciv Roketi / hayalet ucusu
    public float jetSpeed = 7f;
    [HideInInspector] public float speedMultiplier = 1f; // sersemleyince < 1 (PlayerCombat)
    public float jumpHeight = 1.4f;
    public float gravity = -20f;

    [Header("Bakis")]
    public Transform cameraPivot;
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 85f;

    // ---- Ag (multiplayer): yerel oyuncu erisimi (NetPlayer atar) ----
    public static PlayerController Local;
    public static PlayerController LocalOrFind()
    {
        if (Local != null) return Local;
        PlayerController first = null;
        foreach (var p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (p.enabled) return p;   // uzak kopyalarda PlayerController kapali
            if (first == null) first = p;
        }
        return first;
    }
    public static GameObject LocalObject() { var p = LocalOrFind(); return p != null ? p.gameObject : null; }

    CharacterController cc;
    float yVelocity;
    float pitch;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    [HideInInspector] public Vector3 launch; float launchTime; // ziplatici: havadayken korunan yatay hiz
    public void Launch(Vector3 horizontal, float up) { launch = horizontal; launchTime = Time.time; yVelocity = up; }

    public void AddVertical(float v) { yVelocity = Mathf.Max(yVelocity, v); }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (SettingsMenu.IsOpen) return;   // ESC menusu (SettingsMenu.cs) acikken girdi yok

        if (Cursor.lockState == CursorLockMode.Locked && speedMultiplier > 0.01f)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0f, d.x, 0f);
            pitch = Mathf.Clamp(pitch - d.y, -maxLookAngle, maxLookAngle);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = (transform.right * x + transform.forward * z);
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (cc.isGrounded && yVelocity < 0f) yVelocity = -2f;
        if (cc.isGrounded && Time.time - launchTime > 0.2f) launch = Vector3.zero;
        if (cc.isGrounded && speedMultiplier > 0.01f && kb.spaceKey.wasPressedThisFrame)
            yVelocity = Mathf.Sqrt(jumpHeight * envJump * -2f * gravity);
        if (Time.time < jetUntil && kb.spaceKey.isPressed) yVelocity = jetSpeed;
        else yVelocity += gravity * Time.deltaTime;

        Vector3 vel = move * moveSpeed * speedMultiplier * envSpeed + Vector3.up * yVelocity + new Vector3(launch.x, 0f, launch.z);
        cc.Move(vel * Time.deltaTime);
    }
}
