using System.Collections.Generic;
using UnityEngine;

public class FishManager : MonoBehaviour {
    // Singleton pattern so the Bobber can easily find the Manager
    public static FishManager Instance { get; private set; }

    [Header("Setup")]
    public GameObject fishPrefab;

    [Header("Settings")]
    public int maxFishInPond = 5;
    public float spawnRadius = 10f;
    public float waterLevelY = 0f;

    public Bobber ActiveBobber { get; private set; }
    private List<GameObject> activeFish = new List<GameObject>();

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update() {
        activeFish.RemoveAll(fish => fish == null);

        if (activeFish.Count < maxFishInPond) {
            SpawnFish();
        }
    }

    void SpawnFish() {
        Vector3 spawnPos = new Vector3(
            transform.position.x + Random.Range(-spawnRadius, spawnRadius),
            waterLevelY,
            transform.position.z + Random.Range(-spawnRadius, spawnRadius)
        );

        GameObject newFish = Instantiate(fishPrefab, spawnPos, Quaternion.identity);
        Fish fishScript = newFish.GetComponent<Fish>();

        if (fishScript != null) {
            // If a bobber already exists when this fish is born, give it the reference immediately
            if (ActiveBobber != null) {
                fishScript.AssignBobber(ActiveBobber);
            }
        }

        activeFish.Add(newFish);
    }

    // Called automatically by the Bobber script when the player casts
    public void RegisterBobber(Bobber bobber) {
        ActiveBobber = bobber;

        // Tell all existing fish that a new bobber has entered the water
        foreach (GameObject fishObj in activeFish) {
            if (fishObj != null) {
                fishObj.GetComponent<Fish>().AssignBobber(ActiveBobber);
            }
        }
    }

    // Called automatically by the Bobber script when it is reeled in/destroyed
    public void UnregisterBobber() {
        ActiveBobber = null;

        // Tell all fish to disconnect from the bobber and stop tracking it
        foreach (GameObject fishObj in activeFish) {
            if (fishObj != null) {
                fishObj.GetComponent<Fish>().RemoveBobber();
            }
        }
    }
}