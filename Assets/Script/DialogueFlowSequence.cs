using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DialogueFlowSequence : MonoBehaviour, IInteractable
{
    public static DialogueFlowSequence ActiveSequence { get; private set; }

    public enum PanelType
    {
        MCPanel,
        NPCPanel
    }

    [Serializable]
    public struct DialogueStep
    {
        public string stepLabel;
        public PanelType panelType;
        public NPCDialogue dialogueData;
    }

    [Serializable]
    public struct PanelLayoutRefs
    {
        public GameObject panelRoot;
        public TMP_Text textDialogue;
        public TMP_Text textNama;
        public Image portraitImage;
    }

    [Header("Panel Layouts (Auto-Found in Awake)")]
    public GameObject chatContainerPanel;
    public GameObject panelAtas;
    public GameObject panelBawah;
    public PanelLayoutRefs mcLayout;
    public PanelLayoutRefs npcLayout;

    [Header("Other Panels to Close During Dialogue")]
    [Tooltip("Auto-populates MainUIPanel and PlantPanel if left empty. Never hides ChatPanel or its parents.")]
    public List<GameObject> panelsToHideDuringDialogue = new List<GameObject>();
    public bool restorePanelsOnEnd = true;

    [Header("Flow Steps (Ordered by Index)")]
    public List<DialogueStep> dialogueSteps = new List<DialogueStep>();

    [Header("Trigger & Interaction Settings")]
    public bool triggerOnEnter2D = true;
    public bool allowManualInteract = false;
    public string targetTag = "Player";
    public bool hideOnExit2D = false;
    public bool triggerOnce = false;

    [Header("On Sequence Complete")]
    public UnityEvent onSequenceComplete;

    private readonly List<GameObject> previouslyActivePanels = new List<GameObject>();
    private int stepIndex;
    private int lineIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private bool hasTriggered;

    private PanelLayoutRefs CurrentLayout =>
        dialogueSteps[stepIndex].panelType == PanelType.MCPanel ? mcLayout : npcLayout;

    private NPCDialogue CurrentData => dialogueSteps[stepIndex].dialogueData;

    private void Awake()
    {
        AutoAssignUIReferences();
        HideAllPanels();
    }

    private void AutoAssignUIReferences()
    {
        if (chatContainerPanel == null)
            chatContainerPanel = FindSceneObjectByName("ChatPanel");

        Transform chatRoot = chatContainerPanel != null ? chatContainerPanel.transform : null;

        if (panelAtas == null)
            panelAtas = FindChildOrSceneObject(chatRoot, "PanelAtas");

        if (panelBawah == null)
            panelBawah = FindChildOrSceneObject(chatRoot, "Panelbawah'");
        if (panelBawah == null)
            panelBawah = FindChildOrSceneObject(chatRoot, "Panelbawah");

        // Auto-wire MC Layout
        if (mcLayout.panelRoot == null)
            mcLayout.panelRoot = FindChildOrSceneObject(chatRoot, "MCDialoguePanel");

        if (mcLayout.panelRoot != null)
        {
            Transform mcRoot = mcLayout.panelRoot.transform;

            if (mcLayout.portraitImage == null)
                mcLayout.portraitImage = FindComponentInChild<Image>(mcRoot, "Portrait");
            if (mcLayout.portraitImage == null)
                mcLayout.portraitImage = FindComponentInChildRecursive<Image>(mcRoot, "Portrait");
            if (mcLayout.portraitImage == null)
                mcLayout.portraitImage = FindComponentByName<Image>("Portrait");

            if (mcLayout.textDialogue == null)
                mcLayout.textDialogue = FindComponentInChild<TMP_Text>(mcRoot, "Chat/DialogText");
            if (mcLayout.textDialogue == null)
                mcLayout.textDialogue = FindComponentInChildRecursive<TMP_Text>(mcRoot, "DialogText");

            if (mcLayout.textNama == null)
                mcLayout.textNama = FindComponentInChild<TMP_Text>(mcRoot, "Nama");
            if (mcLayout.textNama == null)
                mcLayout.textNama = FindComponentInChildRecursive<TMP_Text>(mcRoot, "Nama");
        }

        // Auto-wire NPC Layout
        if (npcLayout.panelRoot == null)
            npcLayout.panelRoot = FindChildOrSceneObject(chatRoot, "NPCDialoguePanel");

        if (npcLayout.panelRoot != null)
        {
            Transform npcRoot = npcLayout.panelRoot.transform;

            if (npcLayout.portraitImage == null)
                npcLayout.portraitImage = FindComponentInChild<Image>(npcRoot, "NPC_Portrait");
            if (npcLayout.portraitImage == null)
                npcLayout.portraitImage = FindComponentInChildRecursive<Image>(npcRoot, "NPC_Portrait");
            if (npcLayout.portraitImage == null)
                npcLayout.portraitImage = FindComponentByName<Image>("NPC_Portrait");

            if (npcLayout.textDialogue == null)
                npcLayout.textDialogue = FindComponentInChild<TMP_Text>(npcRoot, "NPC_Chat/NPC_DialogText");
            if (npcLayout.textDialogue == null)
                npcLayout.textDialogue = FindComponentInChildRecursive<TMP_Text>(npcRoot, "NPC_DialogText");

            if (npcLayout.textNama == null)
                npcLayout.textNama = FindComponentInChild<TMP_Text>(npcRoot, "NPC_Nama");
            if (npcLayout.textNama == null)
                npcLayout.textNama = FindComponentInChildRecursive<TMP_Text>(npcRoot, "NPC_Nama");
        }

        // Auto-populate other UI panels if none were assigned in Inspector
        if (panelsToHideDuringDialogue.Count == 0)
        {
            AddPanelIfSafe(FindSceneObjectByName("MainUIPanel"));
            AddPanelIfSafe(FindSceneObjectByName("PlantPanel"));
        }
    }

    private void AddPanelIfSafe(GameObject candidate)
    {
        if (candidate == null) return;
        if (panelsToHideDuringDialogue.Contains(candidate)) return;
        if (IsProtectedChatHierarchyObject(candidate)) return;

        panelsToHideDuringDialogue.Add(candidate);
    }

    private bool IsProtectedChatHierarchyObject(GameObject obj)
    {
        if (obj == null) return true;
        if (obj == chatContainerPanel || obj == panelAtas || obj == panelBawah) return true;
        if (obj == mcLayout.panelRoot || obj == npcLayout.panelRoot) return true;

        if (chatContainerPanel != null)
        {
            Transform objTransform = obj.transform;
            Transform chatTransform = chatContainerPanel.transform;

            // Never hide ancestors of ChatPanel (like UI Canvas) or ChatPanel itself
            if (chatTransform.IsChildOf(objTransform))
                return true;
        }

        return false;
    }

    private T FindComponentByName<T>(string objectName) where T : Component
    {
        GameObject obj = FindSceneObjectByName(objectName);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    private GameObject FindChildOrSceneObject(Transform parent, string childName)
    {
        if (parent != null)
        {
            Transform found = parent.Find(childName);
            if (found != null) return found.gameObject;
        }
        return FindSceneObjectByName(childName);
    }

    private T FindComponentInChild<T>(Transform parent, string relativePath) where T : Component
    {
        if (parent == null) return null;
        Transform target = parent.Find(relativePath);
        return target != null ? target.GetComponent<T>() : null;
    }

    private T FindComponentInChildRecursive<T>(Transform parent, string targetName) where T : Component
    {
        if (parent == null) return null;

        T[] components = parent.GetComponentsInChildren<T>(true);
        foreach (T comp in components)
        {
            if (comp.gameObject.name == targetName)
                return comp;
        }
        return null;
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

    public bool CanInteract()
    {
        return allowManualInteract && !isDialogueActive && (!triggerOnce || !hasTriggered);
    }

    public void Interact()
    {
        if (!allowManualInteract) return;

        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!triggerOnEnter2D) return;
        if (hasTriggered && triggerOnce) return;
        if (!other.CompareTag(targetTag)) return;
        if (isDialogueActive) return;

        StartDialogue();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!hideOnExit2D) return;
        if (!other.CompareTag(targetTag)) return;
        if (!isDialogueActive) return;

        EndDialogue();
    }

    public void StartDialogue()
    {
        if (dialogueSteps == null || dialogueSteps.Count == 0)
        {
            EndDialogue();
            return;
        }

        ActiveSequence = this;
        hasTriggered = true;
        isDialogueActive = true;
        stepIndex = 0;

        HideOtherPanelsForDialogue();

        if (chatContainerPanel != null)
            chatContainerPanel.SetActive(true);

        SetLetterboxPanelsActive(true);

        BeginStep(stepIndex);
    }

    private void HideOtherPanelsForDialogue()
    {
        previouslyActivePanels.Clear();

        foreach (GameObject panel in panelsToHideDuringDialogue)
        {
            if (panel == null || IsProtectedChatHierarchyObject(panel)) continue;

            if (panel.activeSelf)
            {
                previouslyActivePanels.Add(panel);
                panel.SetActive(false);
            }
        }
    }

    private void RestoreOtherPanelsAfterDialogue()
    {
        if (!restorePanelsOnEnd)
        {
            previouslyActivePanels.Clear();
            return;
        }

        foreach (GameObject panel in previouslyActivePanels)
        {
            if (panel != null && !IsProtectedChatHierarchyObject(panel))
            {
                panel.SetActive(true);
            }
        }

        previouslyActivePanels.Clear();
    }

    private void BeginStep(int index)
    {
        stepIndex = index;
        lineIndex = 0;

        DialogueStep step = dialogueSteps[stepIndex];
        if (step.dialogueData == null ||
            step.dialogueData.lineDialogue == null ||
            step.dialogueData.lineDialogue.Length == 0)
        {
            MoveToNextStepOrEnd();
            return;
        }

        ActivateOnlySelectedPanel(step.panelType);

        PanelLayoutRefs layout = CurrentLayout;
        if (layout.textNama != null)
            layout.textNama.SetText(step.dialogueData.namaNpc);
        if (layout.portraitImage != null)
            layout.portraitImage.sprite = step.dialogueData.portraitNPC;

        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    public void NextLine()
    {
        if (!isDialogueActive) return;

        NPCDialogue data = CurrentData;
        PanelLayoutRefs layout = CurrentLayout;

        if (isTyping)
        {
            StopAllCoroutines();
            if (layout.textDialogue != null && data != null)
                layout.textDialogue.SetText(data.lineDialogue[lineIndex]);
            isTyping = false;

            if (data != null &&
                data.autoProgressLines != null &&
                data.autoProgressLines.Length > lineIndex &&
                data.autoProgressLines[lineIndex])
            {
                StartCoroutine(AutoProgressAfterDelay(data.autoProgressDelay));
            }
        }
        else if (data != null && ++lineIndex < data.lineDialogue.Length)
        {
            StopAllCoroutines();
            StartCoroutine(TypeLine());
        }
        else
        {
            MoveToNextStepOrEnd();
        }
    }

    private void MoveToNextStepOrEnd()
    {
        StopAllCoroutines();
        isTyping = false;

        if (++stepIndex < dialogueSteps.Count)
        {
            BeginStep(stepIndex);
        }
        else
        {
            EndDialogue();
        }
    }

    private IEnumerator TypeLine()
    {
        isTyping = true;
        NPCDialogue data = CurrentData;
        PanelLayoutRefs layout = CurrentLayout;

        if (layout.textDialogue != null)
            layout.textDialogue.SetText("");

        foreach (char letter in data.lineDialogue[lineIndex])
        {
            if (layout.textDialogue != null)
                layout.textDialogue.text += letter;

            yield return new WaitForSeconds(data.typingSpeed);
        }

        isTyping = false;

        if (data.autoProgressLines != null &&
            data.autoProgressLines.Length > lineIndex &&
            data.autoProgressLines[lineIndex])
        {
            yield return new WaitForSeconds(data.autoProgressDelay);
            NextLine();
        }
    }

    private IEnumerator AutoProgressAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        NextLine();
    }

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;
        isTyping = false;

        if (ActiveSequence == this)
            ActiveSequence = null;

        if (mcLayout.textDialogue != null) mcLayout.textDialogue.SetText("");
        if (npcLayout.textDialogue != null) npcLayout.textDialogue.SetText("");

        HideAllPanels();
        RestoreOtherPanelsAfterDialogue();

        if (onSequenceComplete != null)
        {
            onSequenceComplete.Invoke();
        }
    }

    private void SetLetterboxPanelsActive(bool isActive)
    {
        if (panelAtas != null)
            panelAtas.SetActive(isActive);

        if (panelBawah != null)
            panelBawah.SetActive(isActive);
    }

    private void ActivateOnlySelectedPanel(PanelType activeType)
    {
        if (mcLayout.panelRoot != null)
            mcLayout.panelRoot.SetActive(activeType == PanelType.MCPanel);

        if (npcLayout.panelRoot != null)
            npcLayout.panelRoot.SetActive(activeType == PanelType.NPCPanel);
    }

    private void HideAllPanels()
    {
        SetLetterboxPanelsActive(false);

        if (mcLayout.panelRoot != null)
            mcLayout.panelRoot.SetActive(false);

        if (npcLayout.panelRoot != null)
            npcLayout.panelRoot.SetActive(false);

        if (chatContainerPanel != null)
            chatContainerPanel.SetActive(false);
    }
}