using System;
using UnityEngine;

public class Fish : MonoBehaviour {
    [HideInInspector] public Bobber bobber;

    [Header("Movement & Smoothness")]
    public float moveSpeed = 2f;
    public float wanderRadius = 5f;
    public float rotationSpeed = 4f;
    public Vector3 modelRotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float obstacleCheckDistance = 2f;

    [Header("Fishing Logic")]
    [Range(0f, 1f)] public float realBiteChance = 0.3f;
    public float biteDetectionRange = 4f;
    public float backOffDistance = 2.5f;
    public int minNibblesBeforeBite = 1;
    public int maxNibblesBeforeBite = 3;
    [Tooltip("How many seconds the player has to reel in the fish once it starts biting before it flees.")]
    public float maxBiteDuration = 4f;

    [Header("Struggle/Bite Animation")]
    public float struggleSpeed = 5f;
    public float struggleAmount = 0.7f;
    // How many seconds between each bobber-dunking pull while the fish is biting
    public float bitePulseInterval = 0.8f;

    [Header("Fish Types")]
    [Tooltip("Assign sprites, weights, and size ranges. Weights are relative — e.g. 3, 1, 1 means the first is 3x more likely.")]
    public FishType[] fishTypes;

    // Read by Fishing.cs (or UI code) after a catch to know what was landed
    public FishType CaughtType { get; private set; }

    public event Action OnNibble;
    public event Action OnBite;

    private Vector3 targetWanderPosition;
    private Vector3 backOffPosition;
    private float lockedYLevel;
    private int currentNibbleCount;
    private float biteTimer;
    private float escapeTimer;

    // FIX: tracks whether THIS fish is the one that set bobber.hasTarget = true.
    // Prevents other fish from stealing the lock, and prevents obstacle bounces
    // from resetting currentNibbleCount via a false "fresh lock-on" in CheckForBobber.
    private bool isClaimingBobber = false;

    private enum FishState { Wandering, Approaching, BackingOff, Biting, Caught }
    private FishState currentState = FishState.Wandering;

    // Used by Fishing.cs to detect whether a fish can be reeled in
    public bool IsBiting => currentState == FishState.Biting;

    void Start() {
        lockedYLevel = transform.position.y;
        PickNewWanderTarget();
    }

    // FIX: if this fish is destroyed while holding the bobber lock, release it
    // so the next fish can pick it up.
    void OnDestroy() {
        if (bobber != null && isClaimingBobber) {
            bobber.hasTarget = false;
        }
    }

    void Update() {
        // Hand off transform control to Fishing.cs while being reeled in
        if (currentState == FishState.Caught) return;

        if (currentState != FishState.Wandering && bobber == null) {
            ForceReturnToWandering();
            return;
        }

        switch (currentState) {
            case FishState.Wandering: Wander(); CheckForBobber(); break;
            case FishState.Approaching: ApproachBobber(); break;
            case FishState.BackingOff: BackOffFromBobber(); break;
            case FishState.Biting: StruggleAtBobber(); break;
        }

        if (currentState != FishState.Biting) {
            CheckForObstacles();
        }
    }

    // FIX: unsubscribe any previous bobber's handlers before subscribing to the new
    // one, preventing duplicate event registrations if AssignBobber is ever called
    // more than once on the same fish instance.
    public void AssignBobber(Bobber newBobber) {
        if (bobber != null) {
            OnNibble -= bobber.HandleFishNibble;
            OnBite -= bobber.HandleFishBite;
        }
        bobber = newBobber;
        if (bobber != null) {
            OnNibble += bobber.HandleFishNibble;
            OnBite += bobber.HandleFishBite;
        }
    }

    public void RemoveBobber() {
        // Don't interfere if we're already in the caught/reeling sequence
        if (currentState == FishState.Caught) return;

        if (bobber != null) {
            // FIX: only release hasTarget if we were the one who claimed it
            if (isClaimingBobber) bobber.hasTarget = false;
            OnNibble -= bobber.HandleFishNibble;
            OnBite -= bobber.HandleFishBite;
        }
        isClaimingBobber = false;
        bobber = null;
        ForceReturnToWandering();
    }

    // Called by Fishing.cs at the start of the reel-in arc so this script
    // stops all AI behaviour and hands position control to the coroutine.
    public void SetCaughtMode() {
        if (bobber != null) {
            // FIX: release the bobber lock so the slot isn't left permanently occupied
            if (isClaimingBobber) bobber.hasTarget = false;
            OnNibble -= bobber.HandleFishNibble;
            OnBite -= bobber.HandleFishBite;
            bobber = null;
        }
        isClaimingBobber = false;
        currentState = FishState.Caught;
        RollAndApplyFishType();
    }

    void ForceReturnToWandering() {
        currentState = FishState.Wandering;
        PickNewWanderTarget();
    }

    void Wander() {
        transform.position = Vector3.MoveTowards(transform.position, targetWanderPosition, moveSpeed * Time.deltaTime);

        bool returningToDepth = transform.position.y > lockedYLevel + 0.3f;
        RotateSmoothlyTowards(targetWanderPosition, lockVertical: !returningToDepth);

        if (Vector3.Distance(transform.position, targetWanderPosition) < 0.5f) {
            PickNewWanderTarget();
        }
    }

    void CheckForObstacles() {
        Vector3 currentTargetPos = transform.position;

        switch (currentState) {
            case FishState.Wandering: currentTargetPos = targetWanderPosition; break;
            case FishState.Approaching: if (bobber != null) currentTargetPos = bobber.transform.position; break;
            case FishState.BackingOff: currentTargetPos = backOffPosition; break;
        }

        Vector3 moveDirection = (currentTargetPos - transform.position).normalized;
        if (moveDirection == Vector3.zero) return;

        int terrainMask = LayerMask.GetMask("Terrain");
        if (Physics.SphereCast(transform.position, 0.5f, moveDirection, out RaycastHit hit, obstacleCheckDistance, terrainMask)) {
            if (currentState != FishState.Wandering) {
                // FIX: do NOT reset bobber.hasTarget here.
                // The fish keeps its claim (isClaimingBobber stays true) and will
                // re-enter Approaching next frame via CheckForBobber, preserving
                // currentNibbleCount. Previously this reset hasTarget to false,
                // which let other fish steal the slot AND caused CheckForBobber to
                // treat the next approach as a fresh lock-on, resetting nibble count to 0.
                ForceReturnToWandering();
            } else {
                PickNewWanderTarget();
            }
        }
    }

    void CheckForBobber() {
        if (bobber == null) return;

        // FIX: if another fish already claimed the bobber, do not steal it.
        // The old code only checked bobber.hasTarget, but hasTarget could be
        // momentarily false (e.g. after an obstacle bounce) even while a fish
        // was in the middle of an approach-nibble cycle, allowing a steal.
        if (bobber.hasTarget && !isClaimingBobber) return;

        float xzDist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(bobber.transform.position.x, bobber.transform.position.z)
        );

        if (xzDist <= biteDetectionRange) {
            if (!bobber.hasTarget) {
                // Genuine fresh lock-on: claim the slot and reset the nibble count.
                currentNibbleCount = 0;
                bobber.hasTarget = true;
                isClaimingBobber = true;
            }
            // Either fresh lock-on, or recovering after an obstacle detour.
            // In the recovery case bobber.hasTarget is already true and
            // isClaimingBobber is already true, so currentNibbleCount is untouched.
            currentState = FishState.Approaching;
        } else if (isClaimingBobber) {
            // We wandered beyond detection range while holding the lock.
            // Release it so a closer fish can take over rather than locking
            // the bobber indefinitely.
            bobber.hasTarget = false;
            isClaimingBobber = false;
        }
    }

    void ApproachBobber() {
        Vector3 bobberPos = bobber.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, bobberPos, moveSpeed * 1.2f * Time.deltaTime);
        RotateSmoothlyTowards(bobberPos, lockVertical: false);

        if (Vector3.Distance(transform.position, bobberPos) < 0.4f) {
            DecideNibbleOrBite();
        }
    }

    void DecideNibbleOrBite() {
        bool forceBite = currentNibbleCount >= maxNibblesBeforeBite;
        bool canBite = currentNibbleCount >= minNibblesBeforeBite;

        if ((canBite && UnityEngine.Random.value <= realBiteChance) || forceBite) {
            currentState = FishState.Biting;
            // Fire the first dunk immediately, then start the repeating interval
            OnBite?.Invoke();
            biteTimer = bitePulseInterval;
            escapeTimer = maxBiteDuration;
        } else {
            currentNibbleCount++;

            Vector3 awayXZ = new Vector3(
                transform.position.x - bobber.transform.position.x,
                0f,
                transform.position.z - bobber.transform.position.z
            ).normalized;

            backOffPosition = new Vector3(
                bobber.transform.position.x + awayXZ.x * backOffDistance,
                lockedYLevel,
                bobber.transform.position.z + awayXZ.z * backOffDistance
            );

            currentState = FishState.BackingOff;
            OnNibble?.Invoke();
        }
    }

    void BackOffFromBobber() {
        transform.position = Vector3.MoveTowards(transform.position, backOffPosition, moveSpeed * 0.8f * Time.deltaTime);
        RotateSmoothlyTowards(bobber.transform.position, lockVertical: false);

        if (Vector3.Distance(transform.position, backOffPosition) < 0.3f) {
            currentState = FishState.Approaching;
        }
    }

    void StruggleAtBobber() {
        Vector3 anchorPos = bobber.transform.position + Vector3.down * 0.5f;
        Vector3 struggleOffset = transform.right * Mathf.Sin(Time.time * struggleSpeed) * struggleAmount;
        transform.position = anchorPos + struggleOffset;
        RotateSmoothlyTowards(bobber.transform.position, lockVertical: false);

        // Check if the player took too long to reel in
        escapeTimer -= Time.deltaTime;
        if (escapeTimer <= 0f) {
            EscapeFromBobber();
            return;
        }

        // Repeatedly yank the bobber downward on a timed interval
        biteTimer -= Time.deltaTime;
        if (biteTimer <= 0f) {
            OnBite?.Invoke();
            biteTimer = bitePulseInterval;
        }
    }

    void EscapeFromBobber() {
        if (bobber != null) {
            bobber.hasTarget = false; // Allow other fish to target the bobber again
        }
        isClaimingBobber = false;   // FIX: clear claim before RemoveBobber so it
                                    // doesn't try to reset hasTarget a second time
        RemoveBobber();             // Disconnects events and goes back to wandering state
    }

    void PickNewWanderTarget() {
        targetWanderPosition = new Vector3(
            transform.position.x + UnityEngine.Random.Range(-wanderRadius, wanderRadius),
            lockedYLevel,
            transform.position.z + UnityEngine.Random.Range(-wanderRadius, wanderRadius)
        );
    }

    void RotateSmoothlyTowards(Vector3 target, bool lockVertical = true) {
        Vector3 direction = (target - transform.position).normalized;

        if (lockVertical) direction.y = 0f;
        if (direction == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        Quaternion finalRot = targetRot * Quaternion.Euler(modelRotationOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, rotationSpeed * Time.deltaTime);
    }

    // ─────────────────────────── Fish Type Rolling ────────────────────────────

    FishType PickFishType() {
        float total = 0f;
        foreach (FishType ft in fishTypes) total += ft.weight;

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        foreach (FishType ft in fishTypes) {
            cumulative += ft.weight;
            if (roll <= cumulative) return ft;
        }
        return fishTypes[fishTypes.Length - 1]; // fallback (floating-point edge case)
    }

    void RollAndApplyFishType() {
        if (fishTypes == null || fishTypes.Length == 0) return;

        CaughtType = PickFishType();

        if (CaughtType.prefab != null) {
            MeshRenderer placeholderMesh = GetComponentInChildren<MeshRenderer>();
            if (placeholderMesh != null) placeholderMesh.enabled = false;

            SpriteRenderer placeholderSprite = GetComponentInChildren<SpriteRenderer>();
            if (placeholderSprite != null) placeholderSprite.enabled = false;

            GameObject spawnedVisual = Instantiate(CaughtType.prefab, transform.position, transform.rotation, transform);
            spawnedVisual.transform.localRotation = Quaternion.Euler(modelRotationOffset);
        }

        float size = UnityEngine.Random.Range(CaughtType.minSizeMultiplier, CaughtType.maxSizeMultiplier);
        if (size <= 0) size = 1f; // Safety fallback
        transform.localScale = Vector3.one * size;
    }
}

[System.Serializable]
public class FishType {
    public string name = "Fish";
    [Tooltip("Drag your fish prefab here — the sprite is pulled from its SpriteRenderer automatically.")]
    public GameObject prefab;

    [Tooltip("Relative probability. Higher = more common. E.g. Common=10, Rare=1.")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Minimum scale multiplier applied to the fish when caught.")]
    public float minSizeMultiplier = 0.8f;

    [Tooltip("Maximum scale multiplier applied to the fish when caught.")]
    public float maxSizeMultiplier = 1.2f;
}