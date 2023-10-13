using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerPossess : NetworkBehaviour
{
    private PlayerControl2D possessed;

    public override void OnNetworkSpawn()
    {
        GetComponent<PlayerInput>().enabled = IsOwner;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Loaded scene " + " " + IsOwner);
        if (IsOwner)
        {
            RequestPossessionServerRpc();
        }
    }

    [ServerRpc]
    private void RequestPossessionServerRpc()
    {
        StartCoroutine(AsyncPosess());
    }
    
    private IEnumerator AsyncPosess()
    {
        int possessionNumber = PosessionManager.instance.GetNextPosessable();
        var player = PosessionManager.instance.posessables[possessionNumber];
        // Set the owner to this object's owner
        while (!player.NetworkObject.IsSpawned) {
            yield return null;
        }
        player.NetworkObject.ChangeOwnership(OwnerClientId);
        // tell them about their new player object!
        TellPossessionClientRpc(possessionNumber);
    }

    [ClientRpc]
    private void TellPossessionClientRpc(int possession)
    {
        if (IsOwner && possession >= 0) {
            Debug.Log("Attempting to access " + PosessionManager.instance + " with index " + possession);
            possessed = PosessionManager.instance.posessables[possession];
        }
    }
    
    private void OnMovement(InputValue value)
    {
        if (possessed)
            possessed.OnMovementVector(value.Get<Vector2>());
    }

    private void OnAttackQuick()
    {
        if (possessed)
            possessed.OnAttackQuick();
    }

    private void OnAttackStrong()
    {
        if (possessed)
            possessed.OnAttackStrong();
    }

    private void OnRoll()
    {
        if (possessed)
            possessed.OnRoll();
    }
}
