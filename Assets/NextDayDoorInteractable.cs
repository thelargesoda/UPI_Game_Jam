using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class NextDayDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Activation Gate")]
    [Tooltip("If true, NextDayContinue is always active and interactable so the player can end the day either way.")]
    public bool alwaysAllowTrigger = true;

    [Tooltip("Used only if Always Allow Trigger is false: starts disabled until all quests are completed.")]
    public bool startDisabledUntilQuestsDone = false;

    [Header("Ending Scenes (Set in Inspector)")]
    [Tooltip("Scene name loaded when all quests ARE completed.")]
    public string goodEndSceneName = "";

    [Tooltip("Scene name loaded when quests are NOT completed.")]
    public string badEndSceneName = "";

    [Tooltip("Optional: increments DaySuccess when completing all quests before loading Good End Scene.")]
    public bool incrementDaySuccessOnCompletion = true;

    [Header("Interaction Input")]
    public bool allowIInteractable = true;
    public bool allowKeyPressInTrigger = true;
    public KeyCode interactKey = KeyCode.E;
    public bool allowMouseClickOnCollider = true;
    public string playerTag = "Player";
    public Camera mainCamera;

    [Header("Black Screen Transition")]
    [Tooltip("Full-screen black UI panel/image (auto-finds 'ChangeToNight' if left empty).")]
    public GameObject blackScreenPanel;
    public Image blackScreenImage;
    public CanvasGroup blackScreenCanvasGroup;
    public float fadeToBlackDuration = 1.25f;
    public Color targetBlackColor = Color.black;
    public float holdBlackDurationBeforeLoad = 0.35f;

    private bool isUnlocked;
    private bool playerInRange;
    private bool isTransitioning;
    private Coroutine transitionRoutine;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        AutoWireBlackScreen();

        if (!alwaysAllowTrigger && startDisabledUntilQuestsDone && !isUnlocked)
        {
            bool allDone = QuestSystem.Instance != null && QuestSystem.Instance.AreAllQuestsCompleted;
            if (!allDone)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void AutoWireBlackScreen()
    {
        if (blackScreenPanel == null)
        {
            GameObject found = FindSceneObjectByName("ChangeToNight");
            if (found != null)
            {
                blackScreenPanel = found;
            }
        }

        if (blackScreenPanel != null)
        {
            if (blackScreenImage == null)
                blackScreenImage = blackScreenPanel.GetComponent<Image>();

            if (blackScreenCanvasGroup == null)
                blackScreenCanvasGroup = blackScreenPanel.GetComponent<CanvasGroup>();
        }
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

    public void UnlockNextDayDoor()
    {
        isUnlocked = true;
        gameObject.SetActive(true);
    }

    private bool AreQuestsCompleted()
    {
        return QuestSystem.Instance != null && QuestSystem.Instance.AreAllQuestsCompleted;
    }

    public bool CanInteract()
    {
        if (!isActiveAndEnabled || isTransitioning || !allowIInteractable)
            return false;

        if (alwaysAllowTrigger || isUnlocked)
            return true;

        return AreQuestsCompleted();
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        TriggerNextDayTransition();
    }

    private void Update()
    {
        if (isTransitioning || !isActiveAndEnabled)
            return;

        if (!alwaysAllowTrigger && !isUnlocked && !AreQuestsCompleted())
            return;

        if (allowKeyPressInTrigger && playerInRange && Input.GetKeyDown(interactKey))
        {
            TriggerNextDayTransition();
            return;
        }

        if (allowMouseClickOnCollider && Input.GetMouseButtonDown(0))
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                Collider2D hit = Physics2D.OverlapPoint(worldPos);
                if (hit != null && hit.gameObject == gameObject)
                {
                    TriggerNextDayTransition();
                }
            }
        }
    }

    public void TriggerNextDayTransition()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        AutoWireBlackScreen();

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }
        transitionRoutine = StartCoroutine(FadeAndLoadEndingSceneRoutine());
    }

    private IEnumerator FadeAndLoadEndingSceneRoutine()
    {
        bool questsDone = AreQuestsCompleted();

        if (questsDone && incrementDaySuccessOnCompletion)
        {
            QuestSystem.DaySuccess += 1;
        }

        if (blackScreenPanel != null)
        {
            blackScreenPanel.SetActive(true);

            if (blackScreenCanvasGroup != null)
            {
                blackScreenCanvasGroup.alpha = 0f;
            }

            if (blackScreenImage != null)
            {
                Color startColor = targetBlackColor;
                startColor.a = 0f;
                blackScreenImage.color = startColor;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fadeToBlackDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (blackScreenCanvasGroup != null)
                {
                    blackScreenCanvasGroup.alpha = t;
                }

                if (blackScreenImage != null)
                {
                    Color c = targetBlackColor;
                    c.a = t;
                    blackScreenImage.color = c;
                }

                yield return null;
            }

            if (blackScreenCanvasGroup != null)
            {
                blackScreenCanvasGroup.alpha = 1f;
            }

            if (blackScreenImage != null)
            {
                Color endColor = targetBlackColor;
                endColor.a = 1f;
                blackScreenImage.color = endColor;
            }
        }

        if (holdBlackDurationBeforeLoad > 0f)
        {
            yield return new WaitForSeconds(holdBlackDurationBeforeLoad);
        }

        string targetScene = questsDone ? goodEndSceneName : badEndSceneName;

        if (!string.IsNullOrWhiteSpace(targetScene))
        {
            if (Application.CanStreamedLevelBeLoaded(targetScene))
            {
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                Debug.LogWarning($"[NextDayDoorInteractable] Scene '{targetScene}' is not in Build Settings.");
            }
        }
        else
        {
            Debug.LogWarning(questsDone
                ? "[NextDayDoorInteractable] Good End Scene Name is empty in Inspector."
                : "[NextDayDoorInteractable] Bad End Scene Name is empty in Inspector.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
        }
    }
}