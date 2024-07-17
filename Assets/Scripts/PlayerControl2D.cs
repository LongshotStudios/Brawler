using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Netcode;
using UnityEngine;

public class PlayerControl2D : NetworkBehaviour
{
    public float walkSpeed = 1.0f;
    private int lastTick;
    private Vector2 lastInput;
    private bool lastQuick = false;
    private bool lastStrong = false;
    private bool lastRoll = false;
        
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    public float rollSpeed = 1.0f;
    private bool isRolling = false;

    private struct StateSet
    {
        public int tick;
        public Vector2 position;
        public Vector2 velocity;
        public bool flipX;

        public void Replace(StateSet other) {
            tick = other.tick;
            position = other.position;
            velocity = other.velocity;
            flipX = other.flipX;
        }
        public bool Approximately(StateSet other) {
            return (position - other.position).sqrMagnitude < 0.01f
                && (velocity - other.velocity).sqrMagnitude < 0.01f
                && flipX == other.flipX;
        }

        public string ToString()
        {
            return "t:" + tick + ", p:" + position + ", v:" + velocity + ", f:" + flipX;
        }
    }

    private RingBuffer<StateSet> stateHistory;
    private RingBuffer<StateSet> serverHistory;

    private struct CommandSet
    {
        public int tick;
        public Vector2 lastInput;
        public bool quick;
        public bool strong;
        public bool roll;
    }
    private RingBuffer<CommandSet> commandHistory;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    
        stateHistory = new RingBuffer<StateSet>(NetworkManager.Singleton.NetworkTickSystem.TickRate * 10);
        serverHistory = new RingBuffer<StateSet>(NetworkManager.Singleton.NetworkTickSystem.TickRate * 10);
        commandHistory = new RingBuffer<CommandSet>(NetworkManager.Singleton.NetworkTickSystem.TickRate * 10);
    }

    public override void OnNetworkSpawn()
    {
        NetworkedPhysicsController.instance.startofNetworkTick += StartofTick;
        NetworkedPhysicsController.instance.beforeSimulate += BeforeSimulate;
        NetworkedPhysicsController.instance.afterSimulate += AfterSimulate;
        NetworkedPhysicsController.instance.beforeRewindSim += BeforeRewind;
        NetworkedPhysicsController.instance.afterRewindSim += AfterRewind;
        
        var tick = NetworkManager.Singleton.ServerTime.Tick;
        // Debug.Log(gameObject.name + ": storing local state at start tick " + tick);
        StoreStateSetLocal(tick, rb.position, rb.velocity, spriteRenderer.flipX);
        PrintState("after initial tick " + tick);
    }

    public override void OnNetworkDespawn()
    {
        NetworkedPhysicsController.instance.startofNetworkTick -= StartofTick;
        NetworkedPhysicsController.instance.beforeSimulate -= BeforeSimulate;
        NetworkedPhysicsController.instance.afterSimulate -= AfterSimulate;
        NetworkedPhysicsController.instance.beforeRewindSim -= BeforeRewind;
        NetworkedPhysicsController.instance.afterRewindSim -= AfterRewind;
    }

    private void LocalSim()
    {
        // sets up the simulation even just before its called by the network physics
        var speed = isRolling ? rollSpeed : walkSpeed;
        var delta = lastInput * speed * NetworkManager.Singleton.ServerTime.FixedDeltaTime;
        // Debug.Log(gameObject.name + ": Moving delta " + delta + " lastInput " + lastInput + " speed " + speed + " dt " + NetworkManager.Singleton.ServerTime.FixedDeltaTime);
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
        
        animator.SetFloat("Speed", lastInput.magnitude);
        // If stopped, don't flip otherwise we always stop in one direction (right in this case)
        if (lastInput.x != 0) {
            spriteRenderer.flipX = lastInput.x < 0;
        }
    }

    private void StartofTick(int tick)
    {
        if (!IsOwner) {
            return;
        }
        // save/send out our current command state here before any rewinds etc
        StoreCommandSetLocal(tick, lastInput, lastQuick, lastStrong, lastRoll);
        if (NetworkManager.IsServer) {
            StoreCommandSetClientRpc(tick, lastInput, lastQuick, lastStrong, lastRoll);
        } else {
            StoreCommandSetServerRpc(tick, lastInput, lastQuick, lastStrong, lastRoll);
        }
        // Debug.Log(gameObject.name + ": local storing command for tick " + tick + " " + lastInput +
                  // " " + lastQuick + " " + lastStrong + " " + lastRoll);
        if (NetworkedPhysicsController.instance.ReplayRequested) {
            PrintState("State for rewind ");
        }
    }

    private void BeforeSimulate(int tick)
    {
        var commandSet = FindOrExtrapolateCommandSet(tick);
        if (commandSet.tick < -1) {
            Debug.LogWarning("Simulating too early I guess, no command history let's wait");
            return;
        }
        
        // Resimulate from the most recent command set we know (what if we already did? What about if we missed an older one?)
        lastTick = commandSet.tick;
        lastInput = commandSet.lastInput;
        lastQuick = commandSet.quick;
        lastStrong = commandSet.strong;
        lastRoll = commandSet.roll;

        var state = FindOrExtrapolateStateSet(lastTick);
        rb.position = state.position;
        rb.velocity = state.velocity;
        spriteRenderer.flipX = state.flipX;

        if (state.tick < 0) {
            Debug.LogError(gameObject.name + ": There is no state stored for we can't simulate!");
            return;
        }

        // Debug.Log(gameObject.name + ": Before sim " + lastTick + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
            
        LocalSim();
        
        // Debug.Log(gameObject.name + ": After local sim " + lastTick + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
    }

    private void AfterSimulate(int tick)
    {
        // Debug.Log(gameObject.name + ": After sim " + (lastTick + 1) + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
        // store the state for later rewind
        StoreStateSetLocal(lastTick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        CheckAndClearStateHistory();
        if (NetworkManager.IsServer) {
            // Send the latest state from the server so the clients can update
            UpdateStateSetClientRpc(lastTick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        }
        PrintState("after sim tick " + (lastTick + 1));
    }

    private void BeforeRewind(int tick)
    {
        CommandSet commandSet = FindOrExtrapolateCommandSet(tick);
        lastTick = commandSet.tick;
        if (lastTick != tick) {
            // no point in rewinding this if we don't have the state anyway
            Debug.LogWarning(gameObject.name + ": ignoring rewind at tick " + tick + " because no command set");
            return;
        }
        lastInput = commandSet.lastInput;
        lastQuick = commandSet.quick;
        lastStrong = commandSet.strong;
        lastRoll = commandSet.roll;
        
        StateSet state = FindOrExtrapolateStateSet(tick);
        if (state.tick != tick) {
            Debug.LogWarning(gameObject.name + ": ignoring rewind at tick " + tick + " because no state available");
            lastTick = state.tick;
            return;
        }
        rb.position = state.position;
        rb.velocity = state.velocity;
        spriteRenderer.flipX = state.flipX;

        LocalSim();
    }

    private void AfterRewind(int tick)
    {
        // replace state history at tick with the new info
        if (lastTick != tick) {
            return;
        }
        // Debug.Log(gameObject.name + ": After rewind " + (lastTick + 1) + " pos " + rb.position + " vel " + rb.velocity + " input " + lastInput);
        StoreStateSetLocal(lastTick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        if (NetworkManager.IsServer) {
            // Send the latest state from the server so the clients can update
            UpdateStateSetClientRpc(lastTick + 1, rb.position, rb.velocity, spriteRenderer.flipX);
        }
    }

    private void StoreStateSetLocal(int tick, Vector2 pos,  Vector2 vel, bool flipX)
    {
        StateSet state = new StateSet();
        state.tick = tick;
        state.position = pos;
        state.velocity = vel;
        state.flipX = flipX;
        // Better optimization?
        for (int i = 0; i < stateHistory.Count; i++) {
            if (stateHistory[i].tick == tick) {
                stateHistory[i].Replace(state);
                return;
            }
        }

        if (!stateHistory.Empty) {
            var last = stateHistory[stateHistory.Count - 1];
            if (tick != last.tick + 1) {
                Debug.LogWarning(gameObject.name + ": Adding out of sequence tick " + tick + " last known " +
                                 last.tick);
            }
        }

        if (!stateHistory.Push(state)) {
            Debug.LogError("Ran out of room for history info");
        }
    }

    private void StoreServerSetLocal(int tick, Vector2 pos, Vector2 vel, bool flipX)
    {
        StateSet state = new StateSet();
        state.tick = tick;
        state.position = pos;
        state.velocity = vel;
        state.flipX = flipX;

        // Better optimization?
        for (int i = 0; i < serverHistory.Count; i++) {
            if (serverHistory[i].tick == tick) {
                serverHistory[i].Replace(state);
                return;
            }
        }

        if (!serverHistory.Empty) {
            var last = serverHistory[serverHistory.Count - 1];
            if (tick != last.tick + 1) {
                Debug.LogWarning(gameObject.name + ": Adding out of sequence tick " + tick + " last known " +
                                 last.tick);
            }
        }

        if (!serverHistory.Push(state)) {
            Debug.LogError("Ran out of room for history info");
        }
    }

    [ClientRpc]
    private void UpdateStateSetClientRpc(int tick, Vector2 pos, Vector2 vel, bool flipX)
    {
        if (!serverHistory.Empty && tick < serverHistory.Front.tick)
        {
            // it's old news, ignore it
            return;
        }

        // Debug.Log(gameObject.name + ": client receiving tick update " + tick);
        StoreServerSetLocal(tick, pos, vel, flipX);
        CheckAndClearStateHistory();
        PrintState("after server tick " + tick);
    }
    
    private void CheckAndClearStateHistory() {
        // no state history yet to compare with, wait till we simulate one
        if (stateHistory.Empty) {
            return;
        }

        while (serverHistory.Count > 0 && serverHistory.Front.tick < stateHistory.Front.tick) {
            serverHistory.Pop();
        }

        if (serverHistory.Empty) {
            return;
        }
        
        // if our local history is somehow was behind the server, move till we catch up
        while (stateHistory.Count > 1 && stateHistory.Front.tick != serverHistory.Front.tick) {
            stateHistory.Pop();
        }
        
        while (commandHistory.Count > 0 && commandHistory.Front.tick < stateHistory.Front.tick - 1) {
            commandHistory.Pop();
        }

        while (serverHistory.Count > 0 && stateHistory.Count > 1) {
            if (stateHistory.Front.tick != serverHistory.Front.tick) {
                break;
            }
            if (stateHistory.Front.tick >= NetworkedPhysicsController.instance.rewindTick) {
                break;
            }
            if (stateHistory.Front.Approximately(serverHistory.Front)) {
                stateHistory.Pop();
                serverHistory.Pop();
            } else {
                Debug.LogWarning(gameObject.name + ": state mismatch tick " + stateHistory.Front.tick 
                                 + "(local): " + stateHistory.Front.ToString() + " " + serverHistory.Front.ToString());
                stateHistory.Front.Replace(serverHistory.Front);
                NetworkedPhysicsController.instance.RequestReplayFromTick(stateHistory.Front.tick);
                break;
            }
        }
    }

    private void PrintState(string str) {
        var stateTicks = "";
        for (int i = 0; i < stateHistory.Count; i ++) {
            stateTicks += " " + stateHistory[i].tick;
        }
        var serverTicks = "";
        for (int i = 0; i < serverHistory.Count; i ++) {
            serverTicks += " " + serverHistory[i].tick;
        }
        var commandTicks = "";
        for (int i = 0; i < commandHistory.Count; i++) {
            commandTicks += " " + commandHistory[i].tick;
        }

        Debug.Log(gameObject.name + ": history sequence " + str + " state: " + stateTicks + " server: " 
                  + serverTicks + " command: " + commandTicks);
    }

    private void StoreCommandSetLocal(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        var commands = new CommandSet();
        commands.tick = tick;
        commands.lastInput = lastInput;
        commands.quick = quick;
        commands.strong = strong;
        commands.roll = roll;
        if (!commandHistory.Push(commands)) {
            Debug.LogError(gameObject.name + ": Ran out of room for history info");
        } else {
            Debug.Log(gameObject.name + ": storing command set at tick " + tick);
        }
    }

    private CommandSet FindOrExtrapolateCommandSet(int tick)
    {
        if (commandHistory.Count == 0) {
            Debug.LogWarning(gameObject.name + ": No command history by the time we're looking for one");
            var cmd = new CommandSet();
            cmd.tick = -1;
            return cmd;
        } 
        int last = commandHistory.Count - 1;
        while (last > 0 && tick < commandHistory[last].tick) {
            last--;
        }
        // Debug.Log(gameObject.name + ": Moved through cmds " + (commandHistory.Count - last - 1) + " found tick " + commandHistory[last].tick + " compared with " + tick);
        return commandHistory[last];
    }
    
    private StateSet FindOrExtrapolateStateSet(int tick)
    {
        if (stateHistory.Count == 0) {
            Debug.LogWarning(gameObject.name + ": No state history to work with when searching for one");
            var state = new StateSet();
            state.tick = -1;
            return state;
        }
        int last = stateHistory.Count - 1;
        while (last > 0 && tick < stateHistory[last].tick) {
            last--;
        }
        // Debug.Log(gameObject.name + ": Moved through sts " + (stateHistory.Count - last - 1) + " found tick " + stateHistory[last].tick + " compared with " + tick);
        return stateHistory[last];
    }

    [ServerRpc]
    private void StoreCommandSetServerRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        StoreCommandSetClientRpc(tick, lastInput, quick, strong, roll);
        // TODO If this tick is older than we've simulated we need to rewind on the server to correct the commands
        if (!IsOwner) {
            // Debug.Log(gameObject.name + ": server storing/rebroadcasting command for tick " + tick + " " + lastInput + " " + quick + " " + strong + " " + roll);
            StoreCommandSetLocal(tick, lastInput, quick, strong, roll);
            // We're getting a new client command lets replay back to this tick and play the corrected state
            NetworkedPhysicsController.instance.RequestReplayFromTick(tick);
        }
    }

    [ClientRpc]
    private void StoreCommandSetClientRpc(int tick, Vector2 lastInput, bool quick, bool strong, bool roll)
    {
        if (!IsOwner && !NetworkManager.IsServer)
        {
            // Debug.Log(gameObject.name + ": client storing command for tick " + tick);
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
        isRolling = true;
        gameObject.layer = LayerMask.NameToLayer("RollInteraction");
    }

    public void RollFrameFinished()
    {
        isRolling = false;
        gameObject.layer = LayerMask.NameToLayer("Default");
    }
}
