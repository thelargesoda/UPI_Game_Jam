using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "NewNPCDialogue", menuName ="NPC Dialogue")]
public class NPCDialogue : ScriptableObject
{
    public string namaNpc;
    public Sprite portraitNPC;
    public string[] lineDialogue;
    public bool[] autoProgressLines;
    public float autoProgressDelay;
    public float typingSpeed = 0.05f;
    public AudioClip voiceSound;
    public float pitchSuara = 1f;
   
    
}
