using UnityEngine;

public class Bobber : MonoBehaviour {
    [HideInInspector] public bool hasTarget = false;

    [Header("Water Settings")]
    public float waterDensity = 1f;
    public float waterDrag = 2f;
    public float nibbleStrength = 0.5f;
    public float biteStrength = 2;

    [Header("Bobber Physical Dimensions")]
    public float bobberRadius = 0.2f;
    public float bobberMass = 0.1f;

    private GameObject waterPlane;
    private Rigidbody rb;
    private float originalAirDrag;
    private float bobberVolume;
    private bool bitten;

    void Start() {
        rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.mass = bobberMass;
            originalAirDrag = rb.linearDamping;
        }

        bobberVolume = (4f / 3f) * Mathf.PI * Mathf.Pow(bobberRadius, 3f);

        // Automatically find the water plane in the scene using its tag
        waterPlane = GameObject.FindWithTag("Water");
        if (waterPlane == null) {
            Debug.LogError("Bobber could not find a GameObject with the tag 'Water' in the scene!");
        }

        if (FishManager.Instance != null) {
            FishManager.Instance.RegisterBobber(this);
        }
    }

    void OnDestroy() {
        if (FishManager.Instance != null) {
            FishManager.Instance.UnregisterBobber();
        }
    }

    void FixedUpdate() {
        float currentWaterLevel = waterPlane != null ? waterPlane.transform.position.y : 0f;
        float displacementMultiplier = GetSubmergedVolumeMultiplier(currentWaterLevel);

        if (displacementMultiplier > 0f) {

            float displacedVolume = bobberVolume * displacementMultiplier;
            Vector3 buoyancyForce = -Physics.gravity.normalized * (waterDensity * displacedVolume * Physics.gravity.magnitude);
            rb.AddForce(buoyancyForce, ForceMode.Force);

            Vector3 viscousDragForce = -rb.linearVelocity * (waterDrag * displacementMultiplier);
            rb.AddForce(viscousDragForce, ForceMode.Force);
        } else {
            rb.linearDamping = originalAirDrag;
        }
    }

    float GetSubmergedVolumeMultiplier(float currentWaterLevel) {
        float distanceToSurface = currentWaterLevel - transform.position.y;

        if (distanceToSurface <= -bobberRadius) return 0f;
        if (distanceToSurface >= bobberRadius) return 1f;

        float h = distanceToSurface + bobberRadius;
        float capVolume = (Mathf.PI * Mathf.Pow(h, 2f) / 3f) * (3f * bobberRadius - h);
        return Mathf.Clamp01(capVolume / bobberVolume);
    }

    public void HandleFishNibble() {
        if (rb != null) {
            rb.AddForce(Vector3.down * nibbleStrength, ForceMode.Impulse);
        }
    }

    public void HandleFishBite() {
        if (rb != null) {
            rb.AddForce(Vector3.down * biteStrength, ForceMode.Impulse);
        }
    }
}