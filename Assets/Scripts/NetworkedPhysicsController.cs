using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Netcode;

public class NetworkedPhysicsController : MonoBehaviour
{
    public delegate void Update(int tick);

    public event Update startofNetworkTick;
    public event Update endofNetworkTick;
    public event Update beforeSimulate;
    public event Update afterSimulate;
    public event Update beforeRewindSim;
    public event Update afterRewindSim;

    public int maxStateHistory = 24;
    private int rewindTick = Int32.MaxValue;
    
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
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkTick;
        Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
    }

    private void NetworkTick()
    {
        var now = NetworkManager.Singleton.LocalTime.Tick;
        Debug.Log("Ticking the physics via network at tick " + now + " realtime " + Time.realtimeSinceStartup);

        startofNetworkTick?.Invoke(now);

        if (rewindTick < now) {
            Debug.Log("Rewinding tick from " + rewindTick + " to " + now);
        }

        while (rewindTick < now) {
            beforeRewindSim?.Invoke(rewindTick);
            Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
            afterRewindSim?.Invoke(rewindTick);
            rewindTick++;
        }
        
        Debug.Log("Simulating tick " + now);
        beforeSimulate?.Invoke(now);
        Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        afterSimulate?.Invoke(now);
        
        // set it to the next tick so that we don't rewind, unless requested
        rewindTick = int.MaxValue;
        
        endofNetworkTick?.Invoke(now);
    }

    public void RequestReplayFromTick(int tick)
    {
        // Only replay from the latest request (it means we have a recent server state)
        if (rewindTick >= int.MaxValue || tick > rewindTick) {
            rewindTick = tick;
        }
    }
}