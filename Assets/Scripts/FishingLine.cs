using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class FishingLine : MonoBehaviour {
    [Header("Connections")]
    public Transform castPoint; // The tip of the fishing rod
    public Transform bobber;    // The spawned bobber object

    [Header("Line Smoothness")]
    [Range(10, 50)]
    public int segments = 25;   // How smooth the line looks

    [Header("Dynamic Sag Settings")]
    [Range(0f, 0.5f)]
    public float sagFactor = 0.15f; // How much the line sags based on distance (0.15 = 15% of distance)

    [Tooltip("Extra padding to keep the line slightly above the water surface")]
    public float waterSurfaceOffset = 0.05f;

    private LineRenderer lineRenderer;

    void Start() {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments;
    }

    void LateUpdate() {
        // Safety check if the bobber is reeled in or doesn't exist
        if (castPoint == null || bobber == null) {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        DrawDynamicLine();
    }

    void DrawDynamicLine() {
        Vector3 p0 = castPoint.position;
        Vector3 p2 = bobber.position;

        // 1. Calculate straight-line distance
        float distance = Vector3.Distance(p0, p2);

        // 2. Start with a perfect center midpoint
        Vector3 midPoint = Vector3.Lerp(p0, p2, 0.5f);

        // 3. Apply a natural sag proportional to the current distance
        float calculatedSag = distance * sagFactor;
        midPoint.y -= calculatedSag;

        // 4. WATER GUARD: Prevent the midpoint from dropping below the bobber's water level
        float waterLevel = p2.y + waterSurfaceOffset;
        if (midPoint.y < waterLevel) {
            midPoint.y = waterLevel;
        }

        // 5. Generate the smooth curve points
        for (int i = 0; i < segments; i++) {
            float t = i / (float)(segments - 1);

            // Quadratic Bezier Interpolation
            Vector3 m1 = Vector3.Lerp(p0, midPoint, t);
            Vector3 m2 = Vector3.Lerp(midPoint, p2, t);
            Vector3 curvePoint = Vector3.Lerp(m1, m2, t);

            lineRenderer.SetPosition(i, curvePoint);
        }
    }
}