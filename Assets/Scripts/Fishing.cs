using UnityEngine;
using UnityEngine.InputSystem;

public class BestFishing : MonoBehaviour {
    public Transform fishingRod;
    public GameObject bobberPrefab;
    public Transform castPoint;
    public float castForce;
    public float maxChargeAngle;
    public float maxChargeTime;
    public float maxCastAngle;
    private float charge;
    private GameObject bobber;

    private enum Status {
        idle,
        charging,
        casting,
        castOut
    }

    Status status;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        handleInput();
        updateRod();
    }

    void handleInput() {
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            if (status == Status.idle) {
                status = Status.charging;
                charge = 0;
            } else if (status == Status.castOut) {
                status = Status.idle;
                bringIn();
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

    void updateRod() {
        if (status == Status.charging) {
            fishingRod.localRotation = Quaternion.Euler(maxChargeAngle, 0, 0);
        } else if (status == Status.casting) {
            fishingRod.localRotation = Quaternion.Euler(maxCastAngle, 0, 0);
        } else if (status == Status.castOut) {
            fishingRod.localRotation = Quaternion.Euler(maxCastAngle, 0, 0);
        } else if (status == Status.idle) {
            fishingRod.localRotation = Quaternion.Euler(0, 0, 0);
        }
    }

    void cast() {
        bobber = Instantiate(bobberPrefab, castPoint.position, Quaternion.identity);

        FishingLine line = FindFirstObjectByType<FishingLine>();
        line.castPoint = castPoint;
        line.bobber = bobber.transform;

        Rigidbody rb = bobber.GetComponent<Rigidbody>();

        Vector3 launchDirection = Camera.main.transform.forward; ;

        launchDirection += Vector3.up * 0.1f;
        launchDirection.Normalize();

        rb.AddForce(launchDirection * castForce, ForceMode.Impulse);
    }

    void bringIn() {
        Destroy(bobber);
    }
}
