using UnityEngine;

public class CreditHalaman : MonoBehaviour
{
    public GameObject halaman1;
    public GameObject halaman2;
    public bool iniTombolNext = true;

    void Start()
    {
        if (GetComponent<Collider2D>() == null)
            gameObject.AddComponent<BoxCollider2D>();
    }

    void OnMouseDown()
    {
        if (AudioManager.instance != null)
            AudioManager.instance.PutarSFXTombol();

        if (iniTombolNext)
        {
            halaman1.SetActive(false);
            halaman2.SetActive(true);
        }
        else
        {
            halaman2.SetActive(false);
            halaman1.SetActive(true);
        }
    }
}