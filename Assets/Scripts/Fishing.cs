using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Fishing : MonoBehaviour {
    [Header("Rod Settings")]
    public Transform fishingRod;
    public GameObject bobberPrefab;
    public Transform castPoint;
    public float castForce;
    public float maxChargeAngle;
    public float maxChargeTime;
    public float maxCastAngle;

    [Header("Catch Arc")]
    [Tooltip("How long (seconds) the fish takes to arc from the water to the player.")]
    public float catchFlightTime = 1.2f;
    [Tooltip("How high above the straight-line path the arc peaks.")]
    public float catchArcHeight = 4f;

    [Header("Fish Inspection")]
    [Tooltip("How far in front of the camera the fish hovers during inspection.")]
    public float inspectDistance = 1.5f;
    [Tooltip("Fine-tune the vertical position of the fish (positive = higher).")]
    public float inspectHeightOffset = 0f;
    [Tooltip("Mouse sensitivity while orbiting the fish.")]
    public float orbitSensitivity = 0.3f;

    [Header("References")]
    [Tooltip("Assign the FirstPersonController here, or leave blank to auto-find.")]
    public FirstPersonController playerController;

    private float charge;
    private GameObject bobber;
    private GameObject caughtFishObject;
    private float orbitYaw;
    private float orbitPitch;

    private enum Status { idle, charging, castOut, fishFlying, inspecting }
    Status status;

    void Start() {
        if (playerController == null)
            playerController = GetComponentInParent<FirstPersonController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<FirstPersonController>();
    }

    void Update() {
        handleInput();
        updateRod();

        if (status == Status.inspecting)
            handleFishOrbit();
    }

    // ─────────────────────────────── Input ───────────────────────────────────

    void handleInput() {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            switch (status) {
                case Status.idle:
                    status = Status.charging;
                    charge = 0;
                    break;

                case Status.castOut:
                    bringIn();
                    break;

                case Status.inspecting:
                    endInspection();
                    break;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            if (status == Status.charging) {
                status = Status.castOut;
                cast();
            }
        }

        if (status == Status.charging) {
            charge += Time.deltaTime / maxChargeTime;
            charge = Mathf.Clamp(charge, 0, maxChargeAngle);
        }
    }

    // ─────────────────────────────── Rod Visuals ─────────────────────────────

    void updateRod() {
        switch (status) {
            case Status.charging:
                fishingRod.localRotation = Quaternion.Euler(maxChargeAngle, 0, 0);
                break;

            case Status.castOut:
                fishingRod.localRotation = Quaternion.Euler(maxCastAngle, 0, 0);
                break;

            default:
                fishingRod.localRotation = Quaternion.Euler(0, 0, 0);
                break;
        }
    }

    // ─────────────────────────────── Casting ─────────────────────────────────

    void cast() {
        bobber = Instantiate(bobberPrefab, castPoint.position, Quaternion.identity);

        FishingLine line = FindFirstObjectByType<FishingLine>();
        if (line != null) {
            line.castPoint = castPoint;
            line.bobber = bobber.transform;
        }

        Rigidbody rb = bobber.GetComponent<Rigidbody>();
        Vector3 launchDir = Camera.main.transform.forward;
        launchDir += Vector3.up * 0.1f;
        launchDir.Normalize();
        rb.AddForce(launchDir * castForce, ForceMode.Impulse);
    }

    // ─────────────────────────────── Reeling In ──────────────────────────────

    void bringIn() {
        Fish bitingFish = getBitingFish();

        if (bitingFish != null && bobber != null) {
            status = Status.fishFlying;

            if (playerController != null) {
                playerController.MovementEnabled = false;
            }

            StartCoroutine(reelInFish(bitingFish));
        } else {
            status = Status.idle;
            Destroy(bobber);
            bobber = null;
            clearFishingLine();
        }
    }

    Fish getBitingFish() {
        foreach (Fish f in FindObjectsByType<Fish>(FindObjectsSortMode.None)) {
            if (f.IsBiting) return f;
        }
        return null;
    }

    // ─────────────────────────────── Catch Arc ───────────────────────────────

    IEnumerator reelInFish(Fish fish) {
        // Stop all fish AI — we now own the transform
        fish.SetCaughtMode();

        // Snapshot the start position directly from the underwater fish's position
        Vector3 startPos = fish.transform.position;

        // Delete the bobber immediately when brought in
        if (bobber != null) {
            Destroy(bobber);
            bobber = null;
        }

        // Disconnect the fishing line immediately
        clearFishingLine();

        float elapsed = 0f;
        while (elapsed < catchFlightTime) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / catchFlightTime);
            float tEased = t * t * (3f - 2f * t);           // smoothstep

            // Recalculate end position every frame so the arc lands wherever the player looks
            Transform cam = Camera.main.transform;
            Vector3 endPos = cam.position
                                + cam.forward * inspectDistance
                                + cam.up * inspectHeightOffset;

            // Straight-line lerp with a sine-arch lift
            Vector3 pos = Vector3.Lerp(startPos, endPos, tEased);
            pos.y += Mathf.Sin(t * Mathf.PI) * catchArcHeight;

            fish.transform.position = pos;

            Vector3 dirToPlayer = (cam.position - fish.transform.position).normalized;
            if (dirToPlayer != Vector3.zero) {
                fish.transform.rotation = Quaternion.LookRotation(dirToPlayer);
            }

            yield return null;
        }

        // Lock look mechanics so mouse input handles inspection rotations instead of camera movement
        if (playerController != null)
            playerController.LookEnabled = false;

        enterInspection(fish.gameObject);
    }

    // ─────────────────────────────── Inspection ──────────────────────────────

    void enterInspection(GameObject fish) {
        status = Status.inspecting;
        caughtFishObject = fish;

        Transform cam = Camera.main.transform;
        caughtFishObject.transform.position =
            cam.position + cam.forward * inspectDistance + cam.up * inspectHeightOffset;

        orbitYaw = 0f;
        orbitPitch = 0f;
        caughtFishObject.transform.rotation = cam.rotation * Quaternion.Euler(0f, -90f, 0f);

        Fish fishScript = fish.GetComponent<Fish>();
        if (fishScript != null && fishScript.CaughtType != null) {
            if (FishingUIManager.Instance != null) {
                // Passes the fish name and its scale factor (X component) 
                FishingUIManager.Instance.OnFishCaught(fishScript.CaughtType.name, fish.transform.localScale.x);
            }
        }
    }

    void handleFishOrbit() {
        if (caughtFishObject == null || Mouse.current == null) return;

        Transform cam = Camera.main.transform;


        // Drive custom model inspect orbit rotations
        Vector2 delta = Mouse.current.delta.ReadValue();
        orbitYaw += delta.x * orbitSensitivity;
        orbitPitch -= delta.y * orbitSensitivity;

        caughtFishObject.transform.rotation = cam.rotation * Quaternion.Euler(orbitPitch, orbitYaw - 90f, 0f);
    }

    void endInspection() {
        if (caughtFishObject != null) {
            Destroy(caughtFishObject);
            caughtFishObject = null;
        }

        if (playerController != null) {
            playerController.MovementEnabled = true;
            playerController.LookEnabled = true;
        }

        status = Status.idle;
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    void clearFishingLine() {
        FishingLine line = FindFirstObjectByType<FishingLine>();
        if (line != null) line.bobber = null;
    }

    public void ResetCast() {
        if (status == Status.castOut) {
            status = Status.idle;
            if (bobber != null) {
                Destroy(bobber);
                bobber = null;
            }
            clearFishingLine();
        }
    }
}