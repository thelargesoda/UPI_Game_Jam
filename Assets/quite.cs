using UnityEngine;

public class QuitGame : MonoBehaviour
{
    [Header("Optional Key Shortcut")]
    public bool allowEscapeKey = false;
    public KeyCode quitKey = KeyCode.Escape;

    private void Update()
    {
        if (allowEscapeKey && Input.GetKeyDown(quitKey))
        {
            Quit();
        }
    }

    public void Quit()
    {
        Debug.Log("[QuitGame] Quitting application...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}