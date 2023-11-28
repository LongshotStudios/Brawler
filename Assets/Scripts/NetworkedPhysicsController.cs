using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Netcode;

public class NetworkedPhysicsController : MonoBehaviour
{
    public delegate void Update(int tick);

    public event Update initialNetworkTick;
    public event Update finalNetworkTick;
    public event Update beforeSimulate;
    public event Update afterSimulate;
    public event Update beforeRewindSim;
    public event Update afterRewindSim;

    public int maxStateHistory = 24;
    private int rewindTick = -1;
    
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
        Physics2D.simulationMode = SimulationMode2D.Script;
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkTick;
        rewindTick = NetworkManager.Singleton.LocalTime.Tick;
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkTick;
        Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
    }

    private void NetworkTick()
    {
        Debug.Log("Ticking the physics via network");
        var now = NetworkManager.Singleton.LocalTime.Tick;

        initialNetworkTick?.Invoke(now);
        
        while (rewindTick < now) {
            Debug.Log("Rewinding tick " + rewindTick);
            beforeRewindSim?.Invoke(rewindTick);
            Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
            afterRewindSim?.Invoke(rewindTick);
            rewindTick++;
        }
        
        Debug.Log("Simulating tick " + rewindTick);
        beforeSimulate?.Invoke(now);
        Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        afterSimulate?.Invoke(now);
        
        // set it to the next tick so that we don't rewind, unless requested
        rewindTick++;
        
        finalNetworkTick?.Invoke(now);
    }

    public void RequestReplayFromTick(int tick)
    {
        if (tick < rewindTick) {
            rewindTick = tick;
        }
    }
}