using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerControl2D : NetworkBehaviour
{
    public float walkSpeed = 1.0f;
    private Vector2 lastInput;
    private bool lastQuick = false;
    private bool lastStrong = false;
    private bool lastRoll = false;
        
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    public float rollSpeed = 1.0f;
    private bool isRolling = false;

    private class CommandSet
    {
        public int tick;
        public Vector2 lastInput;
        public bool quick;
        public bool strong;
        public bool roll;
    }
    private List<CommandSet> commandHistory = new List<CommandSet>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        NetworkedPhysicsController.instance.beforeSimulate += Tick;
    }

    public override void OnNetworkDespawn()
    {
        NetworkedPhysicsController.instance.beforeSimulate -= Tick;
    }

    private void Tick(int tick)
    {
        Debug.Log("Network server tick " + NetworkManager.ServerTime.Tick + " local tick " + NetworkManager.LocalTime.Tick);
        // store and send so that we can replay these commands

        if (IsOwner)
        {
            StoreCommandSetLocal(tick, lastInput, lastQuick, lastStrong, lastRoll);
            StoreCommandSetServerRpc(tick, lastInput, lastQuick, lastStrong, lastRoll);
        }
        else
        {
            var commandSet = FindOrExtrapolateCommandSet(tick);
            lastInput = commandSet.lastInput;
            lastQuick = commandSet.quick;
            lastStrong = commandSet.strong;
            lastRoll = commandSet.roll;
        }
        
        // sets up the simulation even just before its called by the network physics
        var speed = isRolling ? rollSpeed : walkSpeed;
        rb.MovePosition(rb.position + lastInput * speed * Time.fixedDeltaTime);
        
        if (lastQuick) {
            animator.SetTrigger("AttackQuick");
        }
        if (lastStrong) {
            animator.SetTrigger("AttackStrong");
        }
        if (lastRoll) {
            animator.SetTrigger("Roll");
        }
        lastQuick = lastStrong = lastRoll = false;
    }

    private void StoreCommandSetLocal(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        var commands = new CommandSet();
        commands.tick = tick;
        commands.lastInput = lastInput;
        commands.quick = quick;
        commands.strong = strong;
        commands.roll = roll;
        commandHistory.Add(commands);
    }

    private CommandSet FindOrExtrapolateCommandSet(int tick)
    {
        int last = commandHistory.Count - 1;
        while (last > 0 && tick < commandHistory[last].tick) {
            last--;
        }
        return commandHistory[last];
    }

    [ServerRpc]
    private void StoreCommandSetServerRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        StoreCommandSetClientRpc(tick, lastInput, quick, strong, roll);
        StoreCommandSetLocal(tick, lastInput, quick, strong, roll);
    }

    [ClientRpc]
    private void StoreCommandSetClientRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        if (!IsOwner && !IsServer)
        {
            StoreCommandSetLocal(tick, lastInput, quick, strong, roll);
        }
    }

    private void Update()
    {
        animator.SetFloat("Speed", lastInput.magnitude);
        if (lastInput.x != 0) {
            spriteRenderer.flipX = lastInput.x < 0;
        }
    }

    public void OnMovementVector(Vector2 value)
    {
        lastInput = value;
    }

    public void OnAttackQuick()
    {
        lastQuick = true;
    }
    
    public void OnAttackStrong()
    {
        lastStrong = true;
    }
    
    public void OnRoll()
    {
        lastRoll = true;
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
