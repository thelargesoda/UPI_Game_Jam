using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Directional Sprites")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite upSprite;
    [SerializeField] private Sprite downSprite;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;

    [Tooltip("Optional: flip leftSprite horizontally instead of assigning a separate leftSprite")]
    [SerializeField] private bool flipLeftFromRight = false;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (downSprite != null && spriteRenderer != null && spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = downSprite;
        }
    }

    private void Update()
    {
        rb.velocity = moveInput * moveSpeed;
        UpdateDirectionalSprite();
    }

    public void gerak(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void UpdateDirectionalSprite()
    {
        if (spriteRenderer == null || moveInput.sqrMagnitude < 0.0001f)
            return;

        // Choose primary axis based on whichever direction has stronger input
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            if (moveInput.x > 0f && rightSprite != null)
            {
                spriteRenderer.flipX = false;
                spriteRenderer.sprite = rightSprite;
            }
            else if (moveInput.x < 0f)
            {
                if (flipLeftFromRight && rightSprite != null)
                {
                    spriteRenderer.flipX = true;
                    spriteRenderer.sprite = rightSprite;
                }
                else if (leftSprite != null)
                {
                    spriteRenderer.flipX = false;
                    spriteRenderer.sprite = leftSprite;
                }
            }
        }
        else
        {
            if (moveInput.y > 0f && upSprite != null)
            {
                spriteRenderer.flipX = false;
                spriteRenderer.sprite = upSprite;
            }
            else if (moveInput.y < 0f && downSprite != null)
            {
                spriteRenderer.flipX = false;
                spriteRenderer.sprite = downSprite;
            }
        }
    }
}