using UnityEngine;

public class FishMovement : MonoBehaviour
{
    [Header("Movement")]
    public float swimSpeed = 2f;
    public float verticalSpeed = 0.25f;
    public float directionChangeTime = 3f;

    [Header("Model Rotation Fix")]
    public float modelPitchOffset = -90f;
    public float modelYawOffset = 0f;
    public float modelRollOffset = 0f;

    [Header("Water Area")]
    public Collider waterBounds;
    public float edgePadding = 2f;

    [Header("Slope Climbing")]
    public float slopeCheckDistance = 1.2f;
    public float slopeCheckRadius = 0.35f;
    public float slopeClimbTime = 0.35f;
    public float slopeClimbLift = 0.7f;
    public float maxSlopeNormalY = 0.9f;

    [Header("Stability")]
    public float uprightCorrectionSpeed = 8f;
    public float verticalSmoothSpeed = 2f;

    private Rigidbody rb;
    private bool isInWater = false;

    private Vector3 swimDirection;
    private float targetVertical;
    private float currentYVelocity;

    private bool isClimbingSlope = false;
    private float slopeClimbTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.angularDamping = 10f;

        rb.constraints = RigidbodyConstraints.None;

        PickNewDirection();

        InvokeRepeating(nameof(PickNewDirection), directionChangeTime, directionChangeTime);
    }

    void FixedUpdate()
    {
        if (isInWater)
        {
            SwimInWater();
        }
        else
        {
            NormalRigidbody();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == waterBounds)
        {
            EnterWater();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other == waterBounds)
        {
            isInWater = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other == waterBounds)
        {
            ExitWater();
        }
    }

    void EnterWater()
    {
        isInWater = true;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        currentYVelocity = 0f;
    }

    void ExitWater()
    {
        isInWater = false;
    }

    void SwimInWater()
    {
        rb.useGravity = false;
        rb.isKinematic = false;

        rb.constraints = RigidbodyConstraints.FreezeRotationX |
    RigidbodyConstraints.FreezeRotationZ;

        KeepAwayFromWaterBounds();
        CheckForClimbableSlope();
        RotateBackToCorrectOrientation();

        float desiredYVelocity = targetVertical * verticalSpeed;

        if (waterBounds != null)
        {
            Bounds b = waterBounds.bounds;

            if (transform.position.y > b.max.y - edgePadding)
            {
                desiredYVelocity = -verticalSpeed;
            }
            else if (transform.position.y < b.min.y + edgePadding)
            {
                desiredYVelocity = verticalSpeed;
            }
        }

        if (isClimbingSlope)
        {
            slopeClimbTimer -= Time.fixedDeltaTime;

            // Give the fish a little extra upward movement while climbing the slope.
            desiredYVelocity = Mathf.Max(desiredYVelocity, slopeClimbLift);

            if (slopeClimbTimer <= 0f)
            {
                isClimbingSlope = false;
            }
        }

        currentYVelocity = Mathf.Lerp(
            currentYVelocity,
            desiredYVelocity,
            verticalSmoothSpeed * Time.fixedDeltaTime
        );

        rb.linearVelocity =
            swimDirection * swimSpeed +
            Vector3.up * currentYVelocity;

        rb.angularVelocity = Vector3.zero;
    }

    void CheckForClimbableSlope()
    {
        Vector3 checkDirection = swimDirection;
        checkDirection.y = 0f;

        if (checkDirection.sqrMagnitude < 0.01f) return;

        checkDirection.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.15f;

        if (Physics.SphereCast(
            origin,
            slopeCheckRadius,
            checkDirection,
            out RaycastHit hit,
            slopeCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        ))
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
            {
                return;
            }

            if (hit.normal.y > 0.05f && hit.normal.y < maxSlopeNormalY)
            {
                StartClimbingSlope();
            }
        }
    }

    void StartClimbingSlope()
    {
        isClimbingSlope = true;
        slopeClimbTimer = slopeClimbTime;

        targetVertical = Mathf.Max(targetVertical, 0f);
        currentYVelocity = Mathf.Max(currentYVelocity, 0f);
    }

    void KeepAwayFromWaterBounds()
    {
        if (waterBounds == null) return;

        Bounds b = waterBounds.bounds;

        bool nearSide =
            transform.position.x < b.min.x + edgePadding ||
            transform.position.x > b.max.x - edgePadding ||
            transform.position.z < b.min.z + edgePadding ||
            transform.position.z > b.max.z - edgePadding;

        if (nearSide)
        {
            Vector3 toCenter = new Vector3(
                b.center.x - transform.position.x,
                0f,
                b.center.z - transform.position.z
            );

            if (toCenter.sqrMagnitude > 0.01f)
            {
                swimDirection = toCenter.normalized;
            }
        }
    }

    void RotateBackToCorrectOrientation()
    {
        Vector3 flatDirection = swimDirection;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.01f)
        {
            PickNewDirection();
            return;
        }

        flatDirection.Normalize();

        Quaternion baseRotation = Quaternion.LookRotation(flatDirection, Vector3.up);

        Quaternion modelCorrection = Quaternion.Euler(
            modelPitchOffset,
            modelYawOffset,
            modelRollOffset
        );

        Quaternion targetRotation = baseRotation * modelCorrection;

        rb.MoveRotation(Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            uprightCorrectionSpeed * 60f * Time.fixedDeltaTime
        ));
    }

    void NormalRigidbody()
    {
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
    }

    void PickNewDirection()
    {
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        );

        if (randomDirection.sqrMagnitude < 0.01f)
        {
            randomDirection = Vector3.forward;
        }

        swimDirection = randomDirection.normalized;
        targetVertical = Random.Range(-0.5f, 0.5f);
    }
}
