using System.Transactions;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;
using Unity.Services.Core;

public class Startup : MonoBehaviour
{
    private string _joinCode;
    public string joinCode
    {
        // Accessible from ui events
        set => _joinCode = value;
        get => _joinCode;
    }

    public Button hostButton;
    public TextMeshProUGUI hostCode;
    public Button joinButton;
    public TMP_InputField joinField;
    
    private string playerId;
    private Allocation allocation;
    private JoinAllocation joinAllocation;
    
    private async void Start()
    {
        await UnityServices.InitializeAsync();
        
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        playerId = AuthenticationService.Instance.PlayerId;
    } 
    
    private void Update()
    {
        
    }
    public async void OnHost()
    {
        joinButton.gameObject.SetActive(false); 
        joinField.gameObject.SetActive(false);
        
        allocation = await RelayService.Instance.CreateAllocationAsync(2, null);
        var relayServerData = new RelayServerData(allocation, "udp");
        var unityTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        unityTransport.SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartHost();

        var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        hostCode.text = "Code: " + joinCode;
    }

    public async void OnJoin()
    {
        hostButton.gameObject.SetActive(false); 
        hostCode.gameObject.SetActive(false);
        
        joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var relayServerData = new RelayServerData(joinAllocation, "udp");
        var unityTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        unityTransport.SetRelayServerData(relayServerData);
        NetworkManager.Singleton.StartClient();
    }

}
