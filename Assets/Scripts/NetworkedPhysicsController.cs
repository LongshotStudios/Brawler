using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

public class NetworkedPhysicsController : MonoBehaviour
{
    public delegate void Update(int tick);

    public event Update startofNetworkTick;
    public event Update endofNetworkTick;
    public event Update beforeSimulate;
    public event Update afterSimulate;
    public event Update beforeRewindSim;
    public event Update afterRewindSim;
    public int rewindTick { get; private set; }
    
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

    public bool ReplayRequested 
    {
        get {
            return rewindTick < NetworkManager.Singleton.LocalTime.Tick;
            // return rewindTick != int.MaxValue;
        }
    }   

    private void Awake()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkTick;
        rewindTick = NetworkManager.Singleton.LocalTime.Tick + 1;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton) {
            NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkTick;
        }
        Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
    }

    private void NetworkTick()
    {
        var now = NetworkManager.Singleton.LocalTime.Tick;
        // Debug.Log("Ticking the physics via network at tick " + now + " realtime " + Time.realtimeSinceStartup);

        startofNetworkTick?.Invoke(now);

        if (rewindTick < now) {
            Debug.LogWarning("Rewinding tick from " + rewindTick + " to " + now);
        }

        while (rewindTick < now) {
            beforeRewindSim?.Invoke(rewindTick);
            Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
            afterRewindSim?.Invoke(rewindTick);
            rewindTick++;
        }
        
        // Debug.Log("Simulating tick " + now);
        beforeSimulate?.Invoke(now);
        Physics2D.Simulate(NetworkManager.Singleton.LocalTime.FixedDeltaTime);
        afterSimulate?.Invoke(now);

        endofNetworkTick?.Invoke(now);
        
        if (rewindTick == now) {
            // if we're caught up, undo the rewind now
            rewindTick++;
        }
    }

    public void RequestReplayFromTick(int tick)
    {
        // replay the earliest request, helps to align the histories
        if (tick < rewindTick) {
            rewindTick = tick;
        }
    }
}