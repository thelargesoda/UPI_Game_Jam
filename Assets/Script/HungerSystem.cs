using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HungerSystem : MonoBehaviour
{
    public static HungerSystem Instance { get; private set; }

    [Header("Hunger Values")]
    public float maxHunger = 100f;
    public float currentHunger = 100f;
    public float plantHungerCost = 10f;
    public float sekopHungerCost = 10f;
    public float siramHungerCost = 5f;

    [Header("UI References (Auto-Found if Left Empty)")]
    public Image healthBarImage;
    public TMP_Text healthTextTMP;
    public Text healthTextLegacy;

    [Header("Bar Behavior")]
    [Tooltip("If HealthBar is set to Simple mode, keeps the left side pinned while shrinking.")]
    public bool pinLeftEdgeInSimpleMode = true;

    [Header("Defeat Scene")]
    [Tooltip("Placeholder scene name; add it to File > Build Settings when ready.")]
    public string defeatedSceneName = "DefeatedScene_Placeholder";
    public bool preventMultipleDefeatTriggers = true;

    private bool hasTriggeredDefeat;
    private float initialWidth;
    private Vector3 initialScale;
    private Vector3 initialLocalPos;
    private bool cachedBarMetrics;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
        AutoWireUI();
        CacheBarMetrics();
        RefreshUI();
    }

    private void AutoWireUI()
    {
        if (healthBarImage == null)
        {
            GameObject barObj = FindSceneObjectByName("HealthBar");
            if (barObj != null) healthBarImage = barObj.GetComponent<Image>();
        }

        if (healthTextTMP == null && healthTextLegacy == null)
        {
            GameObject textObj = FindSceneObjectByName("HealthText");
            if (textObj != null)
            {
                healthTextTMP = textObj.GetComponent<TMP_Text>();
                if (healthTextTMP == null)
                {
                    healthTextLegacy = textObj.GetComponent<Text>();
                }
            }
        }
    }

    private void CacheBarMetrics()
    {
        if (healthBarImage == null || cachedBarMetrics) return;

        RectTransform rt = healthBarImage.rectTransform;
        initialWidth = rt.rect.width;
        initialScale = rt.localScale;
        initialLocalPos = rt.localPosition;
        cachedBarMetrics = true;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.IsValid() && obj.name == objectName)
                return obj;
        }
        return null;
    }

    public void ReduceHungerForPlant()
    {
        ReduceHunger(plantHungerCost);
    }

    public void ReduceHungerForSekop()
    {
        ReduceHunger(sekopHungerCost);
    }

    public void ReduceHungerForSiram()
    {
        ReduceHunger(siramHungerCost);
    }

    public void ReduceHunger(float amount)
    {
        if (amount <= 0f) return;
        if (preventMultipleDefeatTriggers && hasTriggeredDefeat) return;

        currentHunger = Mathf.Clamp(currentHunger - amount, 0f, maxHunger);
        RefreshUI();

        if (currentHunger <= 0f)
        {
            TriggerDefeatedScene();
        }
    }

    public void RestoreHunger(float amount)
    {
        if (amount <= 0f) return;
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
        RefreshUI();
    }

    public void RefreshUI()
    {
        float ratio = maxHunger > 0f ? Mathf.Clamp01(currentHunger / maxHunger) : 0f;

        if (healthBarImage != null)
        {
            CacheBarMetrics();

            if (healthBarImage.type == Image.Type.Filled)
            {
                healthBarImage.fillAmount = ratio;
            }
            else
            {
                RectTransform rt = healthBarImage.rectTransform;
                rt.localScale = new Vector3(initialScale.x * ratio, initialScale.y, initialScale.z);

                if (pinLeftEdgeInSimpleMode)
                {
                    // Compensate for center pivot (0.5) so the left edge stays in place
                    float deltaWorldLikeX = (initialWidth * initialScale.x * (1f - ratio)) * 0.5f;
                    rt.localPosition = new Vector3(initialLocalPos.x - deltaWorldLikeX, initialLocalPos.y, initialLocalPos.z);
                }
            }
        }

        string displayValue = Mathf.CeilToInt(currentHunger).ToString();

        if (healthTextTMP != null)
        {
            healthTextTMP.SetText(displayValue);
        }

        if (healthTextLegacy != null)
        {
            healthTextLegacy.text = displayValue;
        }
    }

    private void TriggerDefeatedScene()
    {
        if (preventMultipleDefeatTriggers)
        {
            if (hasTriggeredDefeat) return;
            hasTriggeredDefeat = true;
        }

        Debug.Log($"[HungerSystem] Hunger reached 0! Loading defeated scene placeholder: '{defeatedSceneName}'");

        if (!string.IsNullOrEmpty(defeatedSceneName) && Application.CanStreamedLevelBeLoaded(defeatedSceneName))
        {
            SceneManager.LoadScene(defeatedSceneName);
        }
        else
        {
            Debug.LogWarning($"[HungerSystem] Scene '{defeatedSceneName}' is not in Build Settings yet. Add it under File > Build Settings when ready.");
        }
    }
}