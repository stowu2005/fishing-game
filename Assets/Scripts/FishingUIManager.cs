using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class FishingUIManager : MonoBehaviour {
    public static FishingUIManager Instance { get; private set; }

    [Header("UI Rows (Assign your 5 text elements)")]
    [Tooltip("Drop your 5 UI text components here in order from top to bottom.")]
    public List<TextMeshProUGUI> fishLogRows = new List<TextMeshProUGUI>();

    [Header("Popup Settings")]
    public TextMeshProUGUI popupText;
    [Tooltip("Assign the background box, border, or outline GameObject that should appear/disappear with the text.")]
    public GameObject popupOutline;
    public float popupDuration = 3f;

    [Header("Size Tuning")]
    [Tooltip("Multiplies the fish's localScale value to look like a realistic weight/length (e.g. 1.2 scale * 15 = 18.0 lbs)")]
    public float baseSizeMultiplier = 10f;
    public string sizeUnit = " lbs";

    // Internal data structures to track custom records
    private Dictionary<string, float> largestCaughtSizes = new Dictionary<string, float>();
    private List<string> discoveredFishNames = new List<string>();

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Turn off popup text and outline until a fish is hooked
        if (popupText != null) popupText.gameObject.SetActive(false);
        if (popupOutline != null) popupOutline.SetActive(false);

        // Initialize everything to a mystery state
        ResetLogToMysteries();
    }

    private void ResetLogToMysteries() {
        for (int i = 0; i < fishLogRows.Count; i++) {
            if (fishLogRows[i] != null) {
                // Changed from "Mystery Fish X: ???" to just "Mystery"
                fishLogRows[i].text = "Mystery";
            }
        }
    }

    /// <summary>
    /// Called externally when a fish is successfully reeled in.
    /// </summary>
    public void OnFishCaught(string fishName, float rawScaleMultiplier) {
        // Calculate a nice cosmetic size
        float calculatedSize = rawScaleMultiplier * baseSizeMultiplier;

        // Trigger the overlay notification
        TriggerPopupNotice(fishName, calculatedSize);

        // If it's a completely new type discovered
        if (!largestCaughtSizes.ContainsKey(fishName)) {
            largestCaughtSizes[fishName] = calculatedSize;

            // Assign this new name to the next empty mystery row layout slot
            if (discoveredFishNames.Count < fishLogRows.Count) {
                discoveredFishNames.Add(fishName);
            }
        }
        // If it's an existing fish type, check if it's a new record size
        else {
            if (calculatedSize > largestCaughtSizes[fishName]) {
                largestCaughtSizes[fishName] = calculatedSize;
            }
        }

        // Repaint strings out onto the screen
        UpdateLogDisplay();
    }

    private void UpdateLogDisplay() {
        for (int i = 0; i < discoveredFishNames.Count; i++) {
            string name = discoveredFishNames[i];
            float maxSize = largestCaughtSizes[name];

            if (i < fishLogRows.Count && fishLogRows[i] != null) {
                // Formats to 2 decimal places: "Salmon: 11.45 lbs"
                fishLogRows[i].text = $"{name}: {maxSize:F2}{sizeUnit}";
            }
        }
    }

    private void TriggerPopupNotice(string fishName, float calculatedSize) {
        if (popupText == null) return;

        // Formats the size to 1 decimal place (e.g., "12.4")
        string formattedSize = calculatedSize.ToString("F1");

        // Quick grammatical polish to pick between "a" or "an" based on the weight number
        // (e.g., "an 8.2 lbs Salmon" vs "a 12.4 lbs Bass")
        string article = "AEIOUaeiou8".Contains(formattedSize[0].ToString()) ? "an" : "a";

        // Builds: "You caught a 12.4 lbs Salmon!"
        string message = $"You caught {article} {formattedSize}{sizeUnit} {fishName}!";

        StopAllCoroutines(); // Reset sequence if tracking rapid inputs
        StartCoroutine(AnimatePopup(message));
    }

    private IEnumerator AnimatePopup(string message) {
        popupText.text = message;

        // Turn BOTH the text and the outline ON together
        popupText.gameObject.SetActive(true);
        if (popupOutline != null) popupOutline.SetActive(true);

        Color originalColor = popupText.color;
        popupText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

        // Display at solid opacity
        yield return new WaitForSeconds(Mathf.Max(0.5f, popupDuration - 1f));

        // Smooth fade out text over the final second
        float elapsed = 0f;
        while (elapsed < 1f) {
            elapsed += Time.deltaTime;
            popupText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - elapsed);
            yield return null;
        }

        // Turn BOTH the text and the outline OFF together
        popupText.gameObject.SetActive(false);
        if (popupOutline != null) popupOutline.SetActive(false);

        popupText.color = originalColor;
    }
}