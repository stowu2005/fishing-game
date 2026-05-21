using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour {
    [Header("Movement")]
    private float moveSpeed = 6f;
    private float moveDamping = 10f;

    [Header("Mouse Look")]
    [Tooltip("Lower = slower/more precise. Try 0.05 – 0.2.")]
    public float mouseSensitivityX = 0.08f;
    public float mouseSensitivityY = 0.08f;

    [Tooltip("1 = instant (no smoothing), 0.01 = very laggy. Try 0.1 – 0.25.")]
    [Range(0.01f, 1f)]
    public float mouseSmoothing = 0.15f;

    public float maxLookUp = 80f;
    public float maxLookDown = 80f;

    [Header("References")]
    public Transform cam;

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody rb;
    private float verticalLookAngle;

    private Keyboard keyboard;
    private Mouse mouse;

    // Smoothed delta accumulator
    private Vector2 smoothedDelta;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        keyboard = Keyboard.current;
        mouse = Mouse.current;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cam == null) {
            Camera childCam = GetComponentInChildren<Camera>();
            if (childCam != null) cam = childCam.transform;
        }
    }

    private void OnEnable() {
        keyboard = Keyboard.current;
        mouse = Mouse.current;
    }

    private void Update() {
        HandleMouseLook();
        HandleCursorToggle();
    }

    private void FixedUpdate() {
        HandleMovement();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void HandleMovement() {
        if (keyboard == null) return;

        float inputX = 0f, inputZ = 0f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) inputX += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) inputX -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) inputZ += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) inputZ -= 1f;

        Vector3 localDir = new Vector3(inputX, 0f, inputZ);
        if (localDir.sqrMagnitude > 1f) localDir.Normalize();

        Vector3 worldDir = transform.TransformDirection(localDir);
        Vector3 targetVelocity = worldDir * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            targetVelocity,
            moveDamping * Time.fixedDeltaTime
        );
    }

    // ── Mouse Look ────────────────────────────────────────────────────────────

    private void HandleMouseLook() {
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        // Raw pixel delta this frame
        Vector2 rawDelta = mouse.delta.ReadValue();

        // Exponential smoothing — blends toward raw input each frame.
        // smoothedDelta approaches rawDelta faster when mouseSmoothing is high.
        smoothedDelta = Vector2.Lerp(smoothedDelta, rawDelta, mouseSmoothing);

        // Apply sensitivity
        float mouseX = smoothedDelta.x * mouseSensitivityX;
        float mouseY = smoothedDelta.y * mouseSensitivityY;

        // Horizontal → rotate player body
        transform.Rotate(Vector3.up * mouseX);

        // Vertical → tilt camera only
        verticalLookAngle -= mouseY;
        verticalLookAngle = Mathf.Clamp(verticalLookAngle, -maxLookUp, maxLookDown);

        if (cam != null)
            cam.localRotation = Quaternion.Euler(verticalLookAngle, 0f, 0f);
    }

    // ── Cursor Management ─────────────────────────────────────────────────────

    private void HandleCursorToggle() {
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}