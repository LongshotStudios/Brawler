using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour
{
    public float walkSpeed = 1.0f;
    private Vector2 lastInput;
    private Animator animator;
    
    public Transform animatedChild;
    private Vector3 originalScale;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        originalScale = animatedChild.localScale;
    }
    private void Update()
    {
        animator.SetFloat("Speed", lastInput.magnitude);
        var dist = walkSpeed * Time.deltaTime;
        transform.position += new Vector3(lastInput.x * dist, 0, lastInput.y * dist);

        if (lastInput.x != 0) { 
            var set = originalScale;
            set.x *= Mathf.Sign(lastInput.x);
            animatedChild.localScale = set;
        }
    }
    
    private void OnMovement(InputValue value)
    {
       lastInput = value.Get<Vector2>();
    }

    private void OnAttack()
    {
        animator.SetTrigger("Attack");
    }

}
