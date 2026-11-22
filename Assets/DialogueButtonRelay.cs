using UnityEngine;

public class DialogueButtonRelay : MonoBehaviour
{
    public void OnSkipOrNextClicked()
    {
        if (DialogueFlowSequence.ActiveSequence != null)
        {
            DialogueFlowSequence.ActiveSequence.NextLine();
            Debug.Log("clicked");
        }
    }
}