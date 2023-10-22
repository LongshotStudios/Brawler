using System.Linq;
using System.Net.Mime;
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
using UnityEngine.SceneManagement;

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
    public TextMeshProUGUI connections;
    public Button startButton;

    public string nextScene;
    
    private string playerId;
    private Allocation allocation;
    private JoinAllocation joinAllocation;
    
    private async void Start()
    {
        startButton.gameObject.SetActive(false);
        await UnityServices.InitializeAsync();
        
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        playerId = AuthenticationService.Instance.PlayerId;
    }

    private void Update()
    {
        connections.text = "";
        if (!NetworkManager.Singleton.IsHost)
        {
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                connections.text = "Connected to host!";
            }
        }
        else
        {
            var clients = NetworkManager.Singleton.ConnectedClients.Count;
            connections.text = "Connections: " + clients;
            startButton.gameObject.SetActive(true);
        }
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

    public void OnStartGame()
    {
        NetworkManager.Singleton.SceneManager.LoadScene(nextScene, LoadSceneMode.Single);
    }
}
