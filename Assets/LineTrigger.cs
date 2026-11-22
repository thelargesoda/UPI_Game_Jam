using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LineTrigger : MonoBehaviour
{
    [Header("Dialogue Data & UI")]
    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text textDialogue;
    public TMP_Text textNama;
    public Image portraitImage;

    [Header("Auto-Find Settings (Optional)")]
    public string dialoguePanelTag = "DialoguePanel";
    public string dialogueTextObjectName = "TextDialogue";
    public string nameTextObjectName = "TextNama";
    public string portraitObjectName = "PortraitImage";

    [Header("Trigger Settings")]
    public string targetTag = "Player";
    public bool hideOnExit = true;
    public bool triggerOnce = false;

    private int dialogueIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private bool hasTriggered;
    private Coroutine typingCoroutine;

    private void Awake()
    {
        if (dialoguePanel == null)
        {
            if (!string.IsNullOrEmpty(dialoguePanelTag))
            {
                GameObject taggedPanel = GameObject.FindWithTag(dialoguePanelTag);
                if (taggedPanel != null) dialoguePanel = taggedPanel;
            }

            if (dialoguePanel == null)
            {
                dialoguePanel = FindSceneObjectByName("ChatPanel");
            }
        }

        if (textDialogue == null)
        {
            textDialogue = FindComponentByName<TMP_Text>(dialogueTextObjectName);
        }

        if (textNama == null)
        {
            textNama = FindComponentByName<TMP_Text>(nameTextObjectName);
        }

        if (portraitImage == null)
        {
            portraitImage = FindComponentByName<Image>(portraitObjectName);
        }
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.scene.IsValid() && obj.name == objectName)
            {
                return obj;
            }
        }
        return null;
    }

    private T FindComponentByName<T>(string objectName) where T : Component
    {
        GameObject obj = FindSceneObjectByName(objectName);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered && triggerOnce) return;
        if (!other.CompareTag(targetTag)) return;
        if (dialogueData == null || isDialogueActive) return;

        hasTriggered = true;
        StartDialogue();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!hideOnExit) return;
        if (!other.CompareTag(targetTag)) return;
        if (!isDialogueActive) return;

        EndDialogue();
    }

    public void StartDialogue()
    {
        isDialogueActive = true;
        dialogueIndex = 0;

        if (textNama != null) textNama.SetText(dialogueData.namaNpc);
        if (portraitImage != null) portraitImage.sprite = dialogueData.portraitNPC;
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine());
    }

    public void NextLine()
    {
        if (!isDialogueActive) return;

        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            textDialogue.SetText(dialogueData.lineDialogue[dialogueIndex]);
            isTyping = false;
        }
        else if (++dialogueIndex < dialogueData.lineDialogue.Length)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeLine());
        }
        else
        {
            EndDialogue();
        }
    }

    private IEnumerator TypeLine()
    {
        isTyping = true;
        textDialogue.SetText("");

        foreach (char letter in dialogueData.lineDialogue[dialogueIndex])
        {
            textDialogue.text += letter;
            yield return new WaitForSeconds(dialogueData.typingSpeed);
        }

        isTyping = false;

        if (dialogueData.autoProgressLines != null &&
            dialogueData.autoProgressLines.Length > dialogueIndex &&
            dialogueData.autoProgressLines[dialogueIndex])
        {
            yield return new WaitForSeconds(dialogueData.autoProgressDelay);
            NextLine();
        }
    }

    public void EndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isDialogueActive = false;
        isTyping = false;

        if (textDialogue != null) textDialogue.SetText("");
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }
}