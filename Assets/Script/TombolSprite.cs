using UnityEngine;
using UnityEngine.Events;

public class TombolSprite : MonoBehaviour
{
    public UnityEvent saatDiklik;

    private void OnMouseDown()
    {
        saatDiklik.Invoke();
    }
}