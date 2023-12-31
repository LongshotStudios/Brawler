using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

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

    private class StateSet
    {
        public int tick;
        public Vector2 position;
        public Vector2 velocity;
        public bool flipX;
    }
    private List<StateSet> stateHistory = new List<StateSet>();

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
        NetworkedPhysicsController.instance.initialNetworkTick += InitialTick;
        NetworkedPhysicsController.instance.beforeSimulate += BeforeSimulate;
        NetworkedPhysicsController.instance.afterSimulate += AfterSimulate;
        NetworkedPhysicsController.instance.beforeRewindSim += BeforeRewind;
        NetworkedPhysicsController.instance.afterRewindSim += AfterRewind;
        
        var tick = NetworkManager.Singleton.LocalTime.Tick;
        Debug.Log(gameObject.name + ": storing local state at start tick " + tick);
        StoreStateSetLocal(tick, rb.position, rb.velocity, spriteRenderer.flipX);
    }

    public override void OnNetworkDespawn()
    {
        NetworkedPhysicsController.instance.initialNetworkTick -= InitialTick;
        NetworkedPhysicsController.instance.beforeSimulate -= BeforeSimulate;
        NetworkedPhysicsController.instance.afterSimulate -= AfterSimulate;
        NetworkedPhysicsController.instance.beforeRewindSim -= BeforeRewind;
        NetworkedPhysicsController.instance.afterRewindSim -= AfterRewind;
    }

    private void Update()
    {
        animator.SetFloat("Speed", lastInput.magnitude);
        // If stopped, don't flip otherwise we always stop in one direction (right in this case)
        if (lastInput.x != 0) {
            spriteRenderer.flipX = lastInput.x < 0;
        }
    }

    private void LocalSim()
    {
        // sets up the simulation even just before its called by the network physics
        var speed = isRolling ? rollSpeed : walkSpeed;
        var delta = lastInput * speed * NetworkManager.Singleton.LocalTime.FixedDeltaTime;
        Debug.Log(gameObject.name + ": Moving delta " + delta + " lastInput " + lastInput + " speed " + speed + " dt " + NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        rb.MovePosition(rb.position + delta);
        
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

    private void InitialTick(int tick)
    {
        if (IsOwner)
        {
            // save/send out our current command state here before any rewinds etc
            StoreCommandSetLocal(tick, lastInput, lastQuick, lastStrong, lastRoll);
            StoreCommandSetServerRpc(tick, lastInput, lastQuick, lastStrong, lastRoll);
            Debug.Log(gameObject.name + ": client storing/rebroadcasting command for tick " + tick + " " + lastInput +
                      " " + lastQuick + " " + lastStrong + " " + lastRoll);
        }
    }

    private void BeforeSimulate(int tick)
    {
        var state = FindOrExtrapolateStateSet(tick);
        rb.position = state.position;
        rb.velocity = state.velocity;
        spriteRenderer.flipX = state.flipX;

        var commandSet = FindOrExtrapolateCommandSet(tick);
        lastInput = commandSet.lastInput;
        lastQuick = commandSet.quick;
        lastStrong = commandSet.strong;
        lastRoll = commandSet.roll;
        
        Debug.Log(gameObject.name + ": Before sim " + tick + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
            
        LocalSim();
        
        Debug.Log(gameObject.name + ": After local sim " + tick + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
    }

    private void AfterSimulate(int tick)
    {
        Debug.Log(gameObject.name + ": After sim " + tick + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
        // store the state for later rewind
        StoreStateSetLocal(tick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        if (IsServer) {
            // Send the latest state from the server so the clients can update
            UpdateStateSetClientRpc(tick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        }
    }

    private void BeforeRewind(int tick)
    {
        StateSet state = FindOrExtrapolateStateSet(tick);
        rb.position = state.position;
        rb.velocity = state.velocity;
        spriteRenderer.flipX = state.flipX;

        CommandSet commandSet = FindOrExtrapolateCommandSet(tick);
        lastInput = commandSet.lastInput;
        lastQuick = commandSet.quick;
        lastStrong = commandSet.strong;
        lastRoll = commandSet.roll;
        
        LocalSim();
    }

    private void AfterRewind(int tick)
    {
        // replace state history at tick with the new info
        StoreStateSetLocal(tick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        /*  Not sure we want to do this, but the server will rewind if it gets old commands
        if (IsServer) {
            // Send the latest state from the server so the clients can update
            UpdateStateSetClientRpc(tick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        }
        */
    }

    private void StoreStateSetLocal(int tick, Vector2 pos,  Vector2 vel, bool flipX)
    {
        StateSet state = null;
        // Better optimization?
        for (int i = 0; i < stateHistory.Count; i++) {
            if (stateHistory[i].tick == tick) {
                state = stateHistory[i];
            }
        }
        if (state == null) {
            state = new StateSet();
        }
        state.tick = tick;
        state.position = pos;
        state.velocity = vel;
        state.flipX = flipX;
        stateHistory.Add(state);
    }

    [ClientRpc]
    private void UpdateStateSetClientRpc(int tick, Vector2 pos, Vector2 vel, bool flipX)
    {
        if (tick < stateHistory[0].tick)
        {
            // it's old news, ignore it
            return;
        } 
        
        Debug.Log(gameObject.name + " receiving tick update " + tick);
        
        // remove any state older than this, this tick will be matched with index 0
        RemoveOldHistory(tick);
        
        // compare with our local copy for position delta, request rewind
        if (stateHistory.Count == 0 || stateHistory[0].tick != tick)
        {
            Debug.Log(gameObject.name + ": state missing: " + tick);
            var state = new StateSet();
            // leave it empty force the mismatch, unless it really is 0 then that's probably fine
            stateHistory.Insert(0, state);
        }
        if ((stateHistory[0].position - pos).sqrMagnitude > 0.25f
            || (stateHistory[0].velocity - vel).sqrMagnitude > 0.25f
            || stateHistory[0].flipX != flipX) 
        {
            Debug.Log(gameObject.name + ": state mismatch: (local) " + " " + stateHistory[0].position + " " + stateHistory[0].velocity 
                     + " (remote) " + " " + pos + " " + vel);
            stateHistory[0].position = pos;
            stateHistory[0].velocity = vel;
            stateHistory[0].flipX = flipX;
            // NetworkedPhysicsController.instance.RequestReplayFromTick(tick);
        }
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
        Debug.Log(gameObject.name + ": Moved through cmds " + (commandHistory.Count - last - 1) + " found tick " + commandHistory[last].tick + " compared with " + tick);
        return commandHistory[last];
    }
    
    private StateSet FindOrExtrapolateStateSet(int tick)
    {
        int last = stateHistory.Count - 1;
        while (last > 0 && tick < stateHistory[last].tick) {
            last--;
        }
        Debug.Log(gameObject.name + ": Moved through sts " + (stateHistory.Count - last - 1) + " found tick " + stateHistory[last].tick + " compared with " + tick);
        return stateHistory[last];
    }

    private void RemoveOldHistory(int tick)
    {
        while (stateHistory.Count > 0 && stateHistory[0].tick < tick) {
            stateHistory.RemoveAt(0);
        } 
        
        while (commandHistory.Count > 0 && commandHistory[0].tick < tick) {
            commandHistory.RemoveAt(0);
        }
    }

    [ServerRpc]
    private void StoreCommandSetServerRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        StoreCommandSetClientRpc(tick, lastInput, quick, strong, roll);
        // TODO If this tick is older than we've simulated we need to rewind on the server to correct the commands
        if (!IsOwner) {
            Debug.Log(gameObject.name + ": server storing/rebroadcasting command for tick " + tick + " " + lastInput + " " + quick + " " + strong + " " + roll);
            StoreCommandSetLocal(tick, lastInput, quick, strong, roll);
        }
    }

    [ClientRpc]
    private void StoreCommandSetClientRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        if (!IsOwner && !IsServer)
        {
            StoreCommandSetLocal(tick, lastInput, quick, strong, roll);
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
