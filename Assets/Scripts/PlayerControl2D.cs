using Unity.Netcode;
using UnityEngine;

public class PlayerControl2D : NetworkBehaviour
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

    public override void OnNetworkSpawn()
    {
        NetworkManager.NetworkTickSystem.Tick += Tick;
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.NetworkTickSystem.Tick -= Tick;
    }

    private void Tick()
    {
        Debug.Log("Network server tick " + NetworkManager.ServerTime.Tick + " local tick " + NetworkManager.LocalTime.Tick);
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

    public void OnMovementVector(Vector2 value)
    {
        lastInput = value;
    }

    public void OnAttackQuick()
    {
        animator.SetTrigger("AttackQuick");
    }
    
    public void OnAttackStrong()
    {
        animator.SetTrigger("AttackStrong");
    }
    
    public void OnRoll()
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
