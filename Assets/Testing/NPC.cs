using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NPC : MonoBehaviour
{
    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text textDialogue, textNama;
    public Image portraitImage;

    private int dialogueIndex;
    private bool isTyping, isDialogueActive;
}
