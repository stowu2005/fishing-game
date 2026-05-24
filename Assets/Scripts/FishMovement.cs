using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FishMovement : MonoBehaviour
{
    [Header("Movement")]
    public float swimSpeed = 2f;
    public float turnSpeed = 90f;
    public float directionChangeInterval = 2f;

    [Header("Vertical Limit")]
    public float maxY = 1f; // Fish will not go above this world Y position

    private Rigidbody rb;
    private float timer;
    private float turnDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Optional: keeps fish from rolling/flipping from physics collisions.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Start()
    {
        PickNewDirection();
    }

    void FixedUpdate()
    {
        // Random turning
        Quaternion turn = Quaternion.Euler(
            0f,
            turnDirection * turnSpeed * Time.fixedDeltaTime,
            0f
        );

        rb.MoveRotation(rb.rotation * turn);

        // Move forward
        Vector3 nextPosition = rb.position + transform.forward * swimSpeed * Time.fixedDeltaTime;

        // Do not allow fish above maxY
        if (nextPosition.y > maxY)
        {
            nextPosition.y = maxY;
        }

        rb.MovePosition(nextPosition);

        timer -= Time.fixedDeltaTime;
        if (timer <= 0f)
        {
            PickNewDirection();
        }
    }

    void PickNewDirection()
    {
        timer = directionChangeInterval;
        turnDirection = Random.Range(-1f, 1f);
    }

    void OnCollisionEnter(Collision collision)
    {
        // When fish hits terrain/wall/etc., turn away.
        turnDirection = Random.value < 0.5f ? -1f : 1f;

        Quaternion turnAround = Quaternion.Euler(0f, Random.Range(90f, 160f), 0f);
        rb.MoveRotation(rb.rotation * turnAround);
    }
}