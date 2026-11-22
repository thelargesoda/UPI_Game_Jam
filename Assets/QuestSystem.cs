using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QuestSystem : MonoBehaviour
{
    public const string DaySuccessPrefsKey = "DaySuccess";

    public static QuestSystem Instance { get; private set; }

    public static int DaySuccess
    {
        get => Instance != null ? Instance.daySuccess : PlayerPrefs.GetInt(DaySuccessPrefsKey, 0);
        set
        {
            int clamped = Mathf.Max(0, value);
            if (Instance != null)
            {
                Instance.daySuccess = clamped;
            }
            PlayerPrefs.SetInt(DaySuccessPrefsKey, clamped);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public class QuestObjective
    {
        [Tooltip("Unique ID used when calling AddProgress(\"id\", amount) from other scripts")]
        public string questId;

        [Tooltip("Display title shown in Quest1 / Quest2 / Quest3")]
        public string questTitle;

        public bool isDailyQuest = true;
        public int currentAmount = 0;
        public int requiredAmount = 3;

        [HideInInspector]
        public bool isCompleted;

        public string SlotDisplayText =>
            $"{questTitle} ({Mathf.Min(currentAmount, requiredAmount)}/{requiredAmount})";
    }

    [Serializable]
    public struct QuestSlotUI
    {
        [Tooltip("Slot container such as Quest1, Quest2, or Quest3")]
        public GameObject slotObject;

        [Tooltip("Text (TMP) inside the slot; auto-found from slotObject if left empty")]
        public TMP_Text questText;

        [Tooltip("Graphic tinted slightly green when completed; auto-found on slotObject if left empty")]
        public Graphic tintGraphic;
    }

    [Serializable]
    public class QuestUpdatedEvent : UnityEvent<QuestObjective> { }

    [Header("Quest Objectives (Max 3 Slots by Index: 0, 1, 2)")]
    public List<QuestObjective> quests = new List<QuestObjective>();

    [Header("Remaining / Not-Completed Counter UI (Auto-Found if Left Empty)")]
    public GameObject questNotCompletedObject;
    public Text notCompletedTextLegacy;
    public TMP_Text notCompletedTextTMP;

    [Header("Quest List UI (Quest1, Quest2, Quest3)")]
    public GameObject questListPanel;
    public QuestSlotUI[] questSlots = new QuestSlotUI[3];
    public Color completedSlotColor = new Color(0.55f, 0.95f, 0.55f, 1f);
    public bool hideEmptySlots = false;

    [Header("Optional List Toggle Buttons (Auto-Found if Left Empty)")]
    public Button openListButton;
    public Button closeListButton;

    [Header("Daily Quest Popup UI (Auto-Found if Left Empty)")]
    public GameObject dailyQuestPopupPanel;
    public TMP_Text dailyQuestPopupText;
    public List<GameObject> panelsToCloseOnPopup = new List<GameObject>();
    public bool restoreClosedPanelsWhenPopupCloses = true;

    [Header("DayInfo & WeatherLogo Sprite Swap On Quests Done")]
    [Tooltip("Auto-found by name 'DayInfo' if left empty")]
    public Image dayInfoImage;

    [Tooltip("Auto-found by name 'WeatherLogo' if left empty")]
    public Image weatherLogoImage;

    [Tooltip("Sprite to apply to DayInfo when all quests are completed (e.g. Night)")]
    public Sprite completedDayInfoSprite;

    [Tooltip("Sprite to apply to WeatherLogo when all quests are completed (e.g. MOON STAR)")]
    public Sprite completedWeatherLogoSprite;

    public bool setNativeSizeOnSpriteChange = false;

    [Header("All Quests Done -> Night Transition & DaySuccess")]
    [Tooltip("Auto-found by name 'ChangeToNight' if left empty")]
    public GameObject changeToNightPanel;

    [Range(0f, 15f)]
    public float nightTransitionDuration = 2f;

    [Range(0f, 1f)]
    public float nightTargetAlpha = 0.85f;

    [Tooltip("Saved & inherited across scenes via PlayerPrefs ('DaySuccess')")]
    [SerializeField]
    private int daySuccess = 0;

    [Tooltip("Optional: load a scene after the night transition finishes (leave blank to stay in scene)")]
    public string nextSceneAfterNightTransition = "";
    public float delayAfterNightBeforeSceneLoad = 0.5f;

    public UnityEvent onAllQuestsCompleted;

    [Header("Events (For Future Quest List UI)")]
    public QuestUpdatedEvent onQuestProgressChanged;
    public QuestUpdatedEvent onQuestCompleted;
    public UnityEvent onQuestListUpdated;

    private readonly Dictionary<string, QuestObjective> questLookup = new Dictionary<string, QuestObjective>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private readonly List<GameObject> closedPanelsCache = new List<GameObject>();

    private Image nightPanelImage;
    private CanvasGroup nightPanelCanvasGroup;
    private Color nightPanelBaseColor = Color.white;
    private Coroutine nightTransitionCoroutine;

    private bool isDailyPopupOpen;
    private bool allQuestsCompletedTriggered;

    public int RemainingQuestCount
    {
        get
        {
            int remaining = 0;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestObjective q = quests[i];
                if (q != null && !q.isCompleted)
                {
                    remaining++;
                }
            }
            return remaining;
        }
    }

    public bool AreAllQuestsCompleted => quests.Count > 0 && RemainingQuestCount == 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        daySuccess = PlayerPrefs.GetInt(DaySuccessPrefsKey, daySuccess);

        AutoWireReferences();
        CacheOriginalColors();
        RebuildLookup();
        RefreshCompletionStates();
        RefreshAllUI();

        if (dailyQuestPopupPanel != null)
        {
            dailyQuestPopupPanel.SetActive(false);
        }

        if (changeToNightPanel != null)
        {
            changeToNightPanel.SetActive(false);
        }
    }

    private void AutoWireReferences()
    {
        if (questNotCompletedObject == null)
        {
            questNotCompletedObject = FindSceneObjectByName("QuestNotCompletedAmount");
        }

        if (questNotCompletedObject != null)
        {
            if (notCompletedTextLegacy == null)
                notCompletedTextLegacy = questNotCompletedObject.GetComponent<Text>();
            if (notCompletedTextTMP == null)
                notCompletedTextTMP = questNotCompletedObject.GetComponent<TMP_Text>();
        }

        if (questListPanel == null)
        {
            questListPanel = FindSceneObjectByName("QuestList");
        }

        if (questSlots == null || questSlots.Length < 3)
        {
            QuestSlotUI[] resized = new QuestSlotUI[3];
            if (questSlots != null)
            {
                for (int i = 0; i < Mathf.Min(questSlots.Length, 3); i++)
                    resized[i] = questSlots[i];
            }
            questSlots = resized;
        }

        string[] defaultSlotNames = { "Quest1", "Quest2", "Quest3" };
        for (int i = 0; i < 3; i++)
        {
            if (questSlots[i].slotObject == null)
            {
                Transform parent = questListPanel != null ? questListPanel.transform : null;
                questSlots[i].slotObject = FindChildOrSceneObject(parent, defaultSlotNames[i]);
            }

            if (questSlots[i].slotObject != null)
            {
                if (questSlots[i].questText == null)
                {
                    questSlots[i].questText = questSlots[i].slotObject.GetComponentInChildren<TMP_Text>(true);
                }

                if (questSlots[i].tintGraphic == null)
                {
                    questSlots[i].tintGraphic = questSlots[i].slotObject.GetComponent<Graphic>();
                }
            }
        }

        if (openListButton == null)
        {
            GameObject openObj = FindSceneObjectByName("OpenListQuest");
            if (openObj != null) openListButton = openObj.GetComponent<Button>();
        }

        if (closeListButton == null)
        {
            GameObject closeObj = FindSceneObjectByName("CloseQuest");
            if (closeObj != null) closeListButton = closeObj.GetComponent<Button>();
        }

        if (openListButton != null)
        {
            openListButton.onClick.RemoveListener(OpenQuestList);
            openListButton.onClick.AddListener(OpenQuestList);
        }

        if (closeListButton != null)
        {
            closeListButton.onClick.RemoveListener(CloseQuestList);
            closeListButton.onClick.AddListener(CloseQuestList);
        }

        if (dailyQuestPopupPanel == null)
        {
            dailyQuestPopupPanel = FindSceneObjectByName("QuestPop");
        }

        if (dailyQuestPopupPanel != null && dailyQuestPopupText == null)
        {
            dailyQuestPopupText = dailyQuestPopupPanel.GetComponentInChildren<TMP_Text>(true);
        }

        if (dayInfoImage == null)
        {
            GameObject dayInfoObj = FindSceneObjectByName("DayInfo");
            if (dayInfoObj != null) dayInfoImage = dayInfoObj.GetComponent<Image>();
        }

        if (weatherLogoImage == null)
        {
            GameObject weatherLogoObj = FindSceneObjectByName("WeatherLogo");
            if (weatherLogoObj != null) weatherLogoImage = weatherLogoObj.GetComponent<Image>();
        }

        if (changeToNightPanel == null)
        {
            changeToNightPanel = FindSceneObjectByName("ChangeToNight");
        }

        if (changeToNightPanel != null)
        {
            nightPanelImage = changeToNightPanel.GetComponent<Image>();
            nightPanelCanvasGroup = changeToNightPanel.GetComponent<CanvasGroup>();
            if (nightPanelImage != null)
            {
                nightPanelBaseColor = nightPanelImage.color;
            }
        }

        if (panelsToCloseOnPopup.Count == 0)
        {
            AddSafePanelToClose(FindSceneObjectByName("MainUIPanel"));
            AddSafePanelToClose(FindSceneObjectByName("ChatPanel"));
            AddSafePanelToClose(FindSceneObjectByName("PlantPanel"));
            AddSafePanelToClose(questListPanel);
        }
    }

    private void CacheOriginalColors()
    {
        if (questSlots == null) return;

        for (int i = 0; i < questSlots.Length; i++)
        {
            Graphic g = questSlots[i].tintGraphic != null
                ? questSlots[i].tintGraphic
                : questSlots[i].questText;

            if (g != null && !originalGraphicColors.ContainsKey(g))
            {
                originalGraphicColors[g] = g.color;
            }
        }
    }

    private void AddSafePanelToClose(GameObject candidate)
    {
        if (candidate == null) return;
        if (panelsToCloseOnPopup.Contains(candidate)) return;
        if (IsProtectedPopupHierarchy(candidate)) return;

        panelsToCloseOnPopup.Add(candidate);
    }

    private bool IsProtectedPopupHierarchy(GameObject candidate)
    {
        if (candidate == null) return true;
        if (candidate == dailyQuestPopupPanel) return true;

        if (dailyQuestPopupPanel != null)
        {
            Transform popupTransform = dailyQuestPopupPanel.transform;
            Transform candTransform = candidate.transform;

            if (popupTransform.IsChildOf(candTransform) || candTransform.IsChildOf(popupTransform))
                return true;
        }

        return false;
    }

    private GameObject FindChildOrSceneObject(Transform parent, string childName)
    {
        if (parent != null)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.gameObject.name == childName)
                    return t.gameObject;
            }
        }
        return FindSceneObjectByName(childName);
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

    private void RebuildLookup()
    {
        questLookup.Clear();
        for (int i = 0; i < quests.Count; i++)
        {
            QuestObjective q = quests[i];
            if (q == null || string.IsNullOrWhiteSpace(q.questId)) continue;
            questLookup[q.questId.Trim()] = q;
        }
    }

    private void RefreshCompletionStates()
    {
        for (int i = 0; i < quests.Count; i++)
        {
            QuestObjective q = quests[i];
            if (q == null) continue;
            q.requiredAmount = Mathf.Max(1, q.requiredAmount);
            q.currentAmount = Mathf.Max(0, q.currentAmount);
            q.isCompleted = q.currentAmount >= q.requiredAmount;
        }
    }

    private void ApplyCompletedWeatherAndDaySprites()
    {
        if (dayInfoImage != null && completedDayInfoSprite != null)
        {
            dayInfoImage.sprite = completedDayInfoSprite;
            if (setNativeSizeOnSpriteChange)
            {
                dayInfoImage.SetNativeSize();
            }
        }

        if (weatherLogoImage != null && completedWeatherLogoSprite != null)
        {
            weatherLogoImage.sprite = completedWeatherLogoSprite;
            if (setNativeSizeOnSpriteChange)
            {
                weatherLogoImage.SetNativeSize();
            }
        }
    }

    private void CheckAllQuestsCompleted()
    {
        if (allQuestsCompletedTriggered) return;
        if (!AreAllQuestsCompleted) return;

        allQuestsCompletedTriggered = true;

        DaySuccess += 1;
        ApplyCompletedWeatherAndDaySprites();
        Debug.Log($"[QuestSystem] All quests completed! DaySuccess is now {DaySuccess}. Starting night transition...");

        onAllQuestsCompleted?.Invoke();

        if (nightTransitionCoroutine != null)
        {
            StopCoroutine(nightTransitionCoroutine);
        }
        nightTransitionCoroutine = StartCoroutine(TransitionToNightRoutine());
    }

    private IEnumerator TransitionToNightRoutine()
    {
        if (changeToNightPanel != null)
        {
            changeToNightPanel.SetActive(true);

            if (nightPanelCanvasGroup != null)
            {
                nightPanelCanvasGroup.alpha = 0f;
            }

            if (nightPanelImage != null)
            {
                Color c = nightPanelBaseColor;
                c.a = 0f;
                nightPanelImage.color = c;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, nightTransitionDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float currentAlpha = Mathf.Lerp(0f, nightTargetAlpha, t);

                if (nightPanelCanvasGroup != null)
                {
                    nightPanelCanvasGroup.alpha = currentAlpha;
                }

                if (nightPanelImage != null)
                {
                    Color c = nightPanelBaseColor;
                    c.a = currentAlpha;
                    nightPanelImage.color = c;
                }

                yield return null;
            }

            if (nightPanelCanvasGroup != null)
            {
                nightPanelCanvasGroup.alpha = nightTargetAlpha;
            }

            if (nightPanelImage != null)
            {
                Color c = nightPanelBaseColor;
                c.a = nightTargetAlpha;
                nightPanelImage.color = c;
            }
        }

        if (!string.IsNullOrWhiteSpace(nextSceneAfterNightTransition))
        {
            if (delayAfterNightBeforeSceneLoad > 0f)
            {
                yield return new WaitForSeconds(delayAfterNightBeforeSceneLoad);
            }

            if (Application.CanStreamedLevelBeLoaded(nextSceneAfterNightTransition))
            {
                SceneManager.LoadScene(nextSceneAfterNightTransition);
            }
            else
            {
                Debug.LogWarning($"[QuestSystem] Scene '{nextSceneAfterNightTransition}' is not in Build Settings.");
            }
        }
    }

    public void RefreshAllUI()
    {
        RefreshRemainingCounterUI();
        RefreshQuestListUI();
    }

    public void RefreshRemainingCounterUI()
    {
        string countStr = RemainingQuestCount.ToString();

        if (notCompletedTextLegacy != null)
        {
            notCompletedTextLegacy.text = countStr;
        }

        if (notCompletedTextTMP != null)
        {
            notCompletedTextTMP.SetText(countStr);
        }
    }

    public void RefreshQuestListUI()
    {
        if (questSlots == null) return;

        for (int i = 0; i < questSlots.Length; i++)
        {
            QuestSlotUI slot = questSlots[i];
            bool hasQuest = i < quests.Count && quests[i] != null;

            if (slot.slotObject != null && hideEmptySlots)
            {
                slot.slotObject.SetActive(hasQuest);
            }

            if (slot.questText != null)
            {
                slot.questText.SetText(hasQuest ? quests[i].SlotDisplayText : string.Empty);
            }

            Graphic targetGraphic = slot.tintGraphic != null ? slot.tintGraphic : slot.questText;
            if (targetGraphic != null)
            {
                if (!originalGraphicColors.TryGetValue(targetGraphic, out Color baseColor))
                {
                    baseColor = targetGraphic.color;
                    originalGraphicColors[targetGraphic] = baseColor;
                }

                bool completed = hasQuest && quests[i].isCompleted;
                targetGraphic.color = completed ? completedSlotColor : baseColor;
            }
        }
    }

    // --- QuestList Open / Close ---

    public void OpenQuestList()
    {
        RefreshAllUI();
        if (questListPanel != null)
        {
            questListPanel.SetActive(true);
        }
    }

    public void CloseQuestList()
    {
        if (questListPanel != null)
        {
            questListPanel.SetActive(false);
        }
    }

    // --- Static / Instance Methods Callable From Any Script ---

    public static bool AddProgress(string questId, int amount = 1)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[QuestSystem] No QuestSystem instance found in scene!");
            return false;
        }
        return Instance.AddQuestProgress(questId, amount);
    }

    public bool AddQuestProgress(string questId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(questId) || amount == 0)
            return false;

        if (!questLookup.TryGetValue(questId.Trim(), out QuestObjective objective))
        {
            RebuildLookup();
            if (!questLookup.TryGetValue(questId.Trim(), out objective))
            {
                Debug.LogWarning($"[QuestSystem] Quest ID '{questId}' not found!");
                return false;
            }
        }

        objective.requiredAmount = Mathf.Max(1, objective.requiredAmount);
        bool wasCompleted = objective.isCompleted;

        objective.currentAmount = Mathf.Max(0, objective.currentAmount + amount);
        objective.isCompleted = objective.currentAmount >= objective.requiredAmount;

        RefreshAllUI();

        if (isDailyPopupOpen)
        {
            UpdateDailyPopupText();
        }

        onQuestProgressChanged?.Invoke(objective);
        onQuestListUpdated?.Invoke();

        if (!wasCompleted && objective.isCompleted)
        {
            onQuestCompleted?.Invoke(objective);
        }

        CheckAllQuestsCompleted();
        return true;
    }

    public QuestObjective AddObjective(string questId, string questTitle, int requiredAmount, int startingAmount = 0, bool isDailyQuest = true)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;

        string cleanId = questId.Trim();
        int safeRequired = Mathf.Max(1, requiredAmount);
        int safeCurrent = Mathf.Max(0, startingAmount);

        if (questLookup.TryGetValue(cleanId, out QuestObjective existing))
        {
            existing.questTitle = questTitle;
            existing.requiredAmount = safeRequired;
            existing.currentAmount = safeCurrent;
            existing.isDailyQuest = isDailyQuest;
            existing.isCompleted = existing.currentAmount >= existing.requiredAmount;
        }
        else
        {
            existing = new QuestObjective
            {
                questId = cleanId,
                questTitle = questTitle,
                requiredAmount = safeRequired,
                currentAmount = safeCurrent,
                isDailyQuest = isDailyQuest,
                isCompleted = safeCurrent >= safeRequired
            };
            quests.Add(existing);
            questLookup[cleanId] = existing;
        }

        RefreshAllUI();

        if (isDailyPopupOpen)
        {
            UpdateDailyPopupText();
        }

        onQuestProgressChanged?.Invoke(existing);
        onQuestListUpdated?.Invoke();
        CheckAllQuestsCompleted();
        return existing;
    }

    public QuestObjective GetQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;
        questLookup.TryGetValue(questId.Trim(), out QuestObjective q);
        return q;
    }

    public List<QuestObjective> GetAllQuests()
    {
        return quests;
    }

    public List<QuestObjective> GetDailyQuests()
    {
        List<QuestObjective> daily = new List<QuestObjective>();
        foreach (QuestObjective q in quests)
        {
            if (q != null && q.isDailyQuest)
                daily.Add(q);
        }
        return daily;
    }

    public void ResetDailyQuests()
    {
        allQuestsCompletedTriggered = false;

        foreach (QuestObjective q in quests)
        {
            if (q != null && q.isDailyQuest)
            {
                q.currentAmount = 0;
                q.isCompleted = false;
            }
        }

        RefreshAllUI();

        if (isDailyPopupOpen)
        {
            UpdateDailyPopupText();
        }

        onQuestListUpdated?.Invoke();
    }

    // --- Daily Quest Popup (Closes Other Panels Except QuestPop) ---

    public void ShowDailyQuestPopup()
    {
        AutoWireReferences();
        closedPanelsCache.Clear();

        foreach (GameObject panel in panelsToCloseOnPopup)
        {
            if (panel == null || IsProtectedPopupHierarchy(panel)) continue;

            if (panel.activeSelf)
            {
                closedPanelsCache.Add(panel);
                panel.SetActive(false);
            }
        }

        UpdateDailyPopupText();

        if (dailyQuestPopupPanel != null)
        {
            dailyQuestPopupPanel.SetActive(true);
            isDailyPopupOpen = true;
        }
    }

    public void CloseDailyQuestPopup()
    {
        if (dailyQuestPopupPanel != null)
        {
            dailyQuestPopupPanel.SetActive(false);
        }
        isDailyPopupOpen = false;

        if (restoreClosedPanelsWhenPopupCloses)
        {
            foreach (GameObject panel in closedPanelsCache)
            {
                if (panel != null && !IsProtectedPopupHierarchy(panel))
                {
                    panel.SetActive(true);
                }
            }
        }

        closedPanelsCache.Clear();
    }

    public void UpdateDailyPopupText()
    {
        if (dailyQuestPopupText == null) return;

        List<QuestObjective> daily = GetDailyQuests();
        if (daily.Count == 0)
        {
            dailyQuestPopupText.SetText("Daily Quest\nNo active daily quests.");
            return;
        }

        List<string> lines = new List<string> { "Daily Quest" };
        foreach (QuestObjective q in daily)
        {
            string statusMark = q.isCompleted ? " [Done]" : "";
            lines.Add($"• {q.SlotDisplayText}{statusMark}");
        }

        dailyQuestPopupText.SetText(string.Join("\n", lines));
    }
}