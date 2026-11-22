using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class FoodPickup : MonoBehaviour, IInteractable
{
    [Header("Hunger Reward")]
    public float hungerRestoreAmount = 10f;
    public HungerSystem hungerSystem;

    [Header("Interaction Modes")]
    [Tooltip("Allows external interaction scripts calling IInteractable.Interact()")]
    public bool allowIInteractable = true;

    [Tooltip("Allows pressing interactKey while player is inside a trigger collider")]
    public bool allowKeyPressInTrigger = true;
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Restores hunger immediately when player enters a trigger collider")]
    public bool eatOnTriggerEnter2D = false;

    public string playerTag = "Player";

    [Header("After Eating")]
    public bool destroyOnEat = false;
    public bool disableOnEat = false;
    public float eatCooldown = 0.25f;
    public UnityEvent onFoodEaten;

    private bool playerInRange;
    private float lastEatTime = -999f;

    private void Awake()
    {
        if (hungerSystem == null)
        {
            hungerSystem = HungerSystem.Instance != null
                ? HungerSystem.Instance
                : FindFirstObjectByType<HungerSystem>();
        }
    }

    private void Update()
    {
        if (!allowKeyPressInTrigger || !playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            EatFood();
        }
    }

    public bool CanInteract()
    {
        if (!allowIInteractable || !isActiveAndEnabled)
            return false;

        return Time.time - lastEatTime >= eatCooldown;
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        EatFood();
    }

    public void EatFood()
    {
        if (Time.time - lastEatTime < eatCooldown)
            return;

        lastEatTime = Time.time;

        HungerSystem targetHunger = hungerSystem != null
            ? hungerSystem
            : (HungerSystem.Instance != null ? HungerSystem.Instance : FindFirstObjectByType<HungerSystem>());

        if (targetHunger != null)
        {
            targetHunger.RestoreHunger(hungerRestoreAmount);
        }
        else
        {
            Debug.LogWarning("[FoodPickup] No HungerSystem found in scene!");
        }

        onFoodEaten?.Invoke();

        if (destroyOnEat)
        {
            Destroy(gameObject);
        }
        else if (disableOnEat)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInRange = true;

        if (eatOnTriggerEnter2D)
        {
            EatFood();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInRange = false;
    }
}