using UnityEngine;
using UnityEngine.SceneManagement;

public class PindahScene : MonoBehaviour
{
    // Bisa dipakai untuk pindah ke scene mana saja (ketik nama scene di Inspector)
    public void KeScene(string namaScene)
    {
        SceneManager.LoadScene(namaScene);
    }

    // Khusus untuk tombol Start -> MainInGame
    public void KeMainInGame()
    {
        SceneManager.LoadScene("MainInGame");
    }

    // Khusus untuk tombol Quit
    public void KeluarGame()
    {
        Application.Quit();
    }
}