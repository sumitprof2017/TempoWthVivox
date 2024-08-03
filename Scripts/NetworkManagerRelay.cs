/*using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

public class NetworkManagerRelay : NetworkBehaviour
{
    public int maxNumberOfPlayers = 6;

    void Start()
    {
        InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        StartRelay();
    }

    private async void StartRelay()
    {
        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxNumberOfPlayers);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Configure the UnityTransport component
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationId,      // This is a string, used directly
                allocation.Key,               // This is a byte array, no conversion needed
                allocation.ConnectionData);   // This is a byte array, no conversion needed

            NetworkManager.Singleton.StartServer();
            Debug.Log($"Relay server started with join code: {relayJoinCode}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Relay could not be started: {e.Message}");
        }
    }

    // Add other methods as needed...
}
*/