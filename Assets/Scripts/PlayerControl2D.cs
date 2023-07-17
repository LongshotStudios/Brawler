using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl2D : MonoBehaviour
{
    public float walkSpeed = 1.0f;
    private Vector2 lastInput;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void Update()
    {
        animator.SetFloat("Speed", lastInput.magnitude);
        var dist = walkSpeed * Time.deltaTime;
        transform.position += new Vector3(lastInput.x * dist, lastInput.y * dist, 0);
        if (lastInput.x != 0) {
            spriteRenderer.flipX = lastInput.x < 0;
        }
    }
    
    private void OnMovement(InputValue value)
    {
       lastInput = value.Get<Vector2>();
    }

    private void OnAttackQuick()
    {
        animator.SetTrigger("AttackQuick");
    }
    
    private void OnAttackStrong()
    {
        animator.SetTrigger("AttackStrong");
    }
    
    private void OnRoll()
    {  
        animator.SetTrigger("Roll");
    }
}
