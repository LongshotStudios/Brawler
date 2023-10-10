using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl2D : MonoBehaviour
{
    public float walkSpeed = 1.0f;
    private Vector2 lastInput;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    public float rollSpeed = 1.0f;
    private bool isRolling = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        animator.SetFloat("Speed", lastInput.magnitude);
        if (lastInput.x != 0) {
            spriteRenderer.flipX = lastInput.x < 0;
        }
    }

    private void FixedUpdate()
    {
        var speed = isRolling ? rollSpeed : walkSpeed;
        rb.MovePosition(rb.position + lastInput * speed * Time.fixedDeltaTime);
    }

    private void OnMovement(InputValue value)
    {
       OnMovementVector(value.Get<Vector2>());
    }

    private void OnMovementVector(Vector2 value)
    {
        lastInput = value;
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

    public void RollFrameStart()
    {       
        Debug.Log("Roll frame start");
        isRolling = true;
        gameObject.layer = LayerMask.NameToLayer("RollInteraction");
    }

    public void RollFrameFinished()
    {
        Debug.Log("Roll frame finished");
        isRolling = false;
        gameObject.layer = LayerMask.NameToLayer("Default");
    }
}
