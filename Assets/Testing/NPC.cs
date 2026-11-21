using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.VisualScripting;

public class NPC : MonoBehaviour, IInteractable
{

    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text textDialogue, textNama;
    public Image portraitImage;

    private int dialogueIndex;
    private bool isTyping, isDialogueActive;

    public bool CanInteract()
    {
        return !isDialogueActive;
    }

    public void Interact()
    {
        if (dialogueData == null )  // PauseController.IsGamePaused && !isDialogueActive
        return;

        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        isDialogueActive = true;
        dialogueIndex = 0;

        textNama.SetText(dialogueData.namaNpc);
        portraitImage.sprite = dialogueData.portraitNPC;

        dialoguePanel.SetActive(true);
        // PauseController.SetPause(true);

        StartCoroutine(TypeLine());
    }

    void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            textDialogue.SetText(dialogueData.lineDialogue[dialogueIndex]);
            isTyping = false;
        }
        else if(++dialogueIndex < dialogueData.lineDialogue.Length)
        {
            StartCoroutine(TypeLine());
        }
        else
        {
            EndDialogue();
        }
    }
    

    IEnumerator TypeLine()
    {
        isTyping = true;
        textDialogue.SetText("");

        foreach (char letter in dialogueData.lineDialogue[dialogueIndex])
        {
            textDialogue.text += letter;
            yield return new WaitForSeconds(dialogueData.typingSpeed);
        }

        isTyping = false;

        if(dialogueData.autoProgressLines.Length > dialogueIndex && dialogueData.autoProgressLines[dialogueIndex])
        {
            yield return new WaitForSeconds(dialogueData.autoProgressDelay);
            NextLine();
        }
    }    

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;
        textDialogue.SetText("");
        dialoguePanel.SetActive(false);
        // PauseController.SetPause(false);

    }
}
