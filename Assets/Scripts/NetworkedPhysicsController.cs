using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Netcode;

public class NetworkedPhysicsController : MonoBehaviour
{
    public delegate void Update(int tick);
    public event Update beforeSimulate;
    public event Update afterSimulate;
    public event Update beforeRewindSim;
    public event Update afterRewindSim;

    public int maxStateHistory = 24;
    
    private List<Rigidbody2D> registeredBodies = new List<Rigidbody2D>();

    private class PreservedState
    {
        public int tick;
        public Rigidbody2D[] bodies;
        public Vector2[] positions;
        public float[] rotations;
        public Vector2[] linearVelocity;
        public float[] angularVelocity;
    }
    private List<PreservedState> states = new List<PreservedState>();

    static private NetworkedPhysicsController _instance;

    static public NetworkedPhysicsController instance
    {
        get {
            if (_instance == null) {
                _instance = FindObjectOfType<NetworkedPhysicsController>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkTick;
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkTick;
        Physics.simulationMode = SimulationMode.FixedUpdate;
    }

    public void RegisterRigidbody(Rigidbody2D rb)
    {
        registeredBodies.Add(rb);
    }

    public void DeregisterRigidbody(Rigidbody2D rb)
    {
        Debug.LogError("You can't unregister!");
        // TODO need a way to recover this body for rewinds
        // registeredBodies.Remove(rb);
    }

    private void NetworkTick()
    {
        Debug.Log("Ticking the physics via network");
        var tick = NetworkManager.Singleton.ServerTime.Tick;
        beforeSimulate?.Invoke(tick);
        StoreState(tick);
        Physics.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        afterSimulate?.Invoke(tick);
        RemoveOldStates(tick - maxStateHistory);
    }

    private void StoreState(int tick)
    {
        PreservedState state = new PreservedState();
        int count =  registeredBodies.Count;
        state.bodies = new Rigidbody2D[count];
        state.positions = new Vector2[count];
        state.rotations = new float[count];
        state.linearVelocity = new Vector2[count];
        state.angularVelocity = new float[count];
        for (int i = 0; i < count; i++)
        {
            state.bodies[i] = registeredBodies[i];
            state.positions[i] = registeredBodies[i].position;
            state.rotations[i] = registeredBodies[i].rotation;
            state.linearVelocity[i] = registeredBodies[i].velocity;
            state.angularVelocity[i] = registeredBodies[i].angularVelocity;
        }
        states.Add(state);
    }

    private bool RestoreState(int tick)
    {
        foreach (var state in states)
        {
            if (state.tick == tick)
            {
                RestoreState(state);
                return true;
            }
        }
        Debug.LogError("Failed to find a state to restore at tick " + tick);
        return false;
    }

    private void RestoreState(PreservedState state)
    {
        for (int i = 0; i < state.bodies.Length; i++)
        {
            state.bodies[i].position = state.positions[i];
            state.bodies[i].rotation = state.rotations[i];
            state.bodies[i].velocity = state.linearVelocity[i];
            state.bodies[i].angularVelocity = state.angularVelocity[i];
        }
    }
    
    private void RemoveOldStates(int tick)
    {
        while (states.Count > 0 && states[0].tick < tick)
        {
            states.RemoveAt(0);
        }
    }

    public void RequestReplayFromTick(int tick)
    {
        var now = NetworkManager.Singleton.ServerTime.Tick;
        var dt = now - tick;
        if (dt <= 0 || dt > maxStateHistory)
        {
            Debug.LogError("Cannot rewind, giving up");
            return;
        }

        if (!RestoreState(tick))
        {
            return;
        }
        states.Clear();
        for (int i = tick; i <= now; i++)
        {
            beforeRewindSim?.Invoke(i);
            StoreState(i); // store the updated state resulting from rewind
            Physics.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
            afterRewindSim?.Invoke(i);
        }
    }
}