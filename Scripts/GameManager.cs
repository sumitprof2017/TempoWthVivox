using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;

using Unity.Services.Multiplay;

using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Unity.Services.Matchmaker.Models.MultiplayAssignment;
using Unity.Netcode.Transports.UTP;
using TMPro;
public class GameManager : NetworkBehaviour
{
    public TMP_InputField inputfield;
    public string port;

    public bool isDedicatedServer;
    // Start is called before the first frame update
    void Start()
    {
        if (isDedicatedServer)
        {
            GetComponent<MatchmakingServer>().enabled = true;
        }
        else
        {
            GetComponent<MetchmakerClient>().enabled = true;

        }
    }
    private void Update()
    {
       
    }


    //Client Side code ended


    public GameObject canvas;
    public void StartGame(bool isServerBool)
    {
        if (isServerBool)
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777,"0.0.0.0");

            NetworkManager.Singleton.StartServer();
            canvas.SetActive(false);
        }
        else
        {
            string ip = inputfield.text;
            print("ip is" + ip);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ip, 7777);

            NetworkManager.Singleton.StartClient();
            canvas.SetActive(false);
           
           

        }
    }

    public List<GameObject> listOfTransforms = new List<GameObject>();
    public CameraFollow cameraFollow;

    public GameObject parentForAllPositions;
    public override void OnNetworkSpawn()
    {


        if (IsServer)
        {


            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerJoined;
        foreach(Transform child in parentForAllPositions.transform)
        {
            listOfTransforms.Add(child.gameObject);
        }
        }

    }

    // Update is called once per frame
    private void OnPlayerJoined(ulong clientId)
    {
        Debug.Log("Player joined: " + clientId);

        // Pick a random transform from the list
        int randomNumber = UnityEngine.Random.Range(0, listOfTransforms.Count);
        Vector3 chosenTransform = listOfTransforms[randomNumber].transform.position;
        listOfTransforms.RemoveAt(randomNumber);
        // Find the player's NetworkObject
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            // Assuming the player object is the first one in the list of owned objects
            NetworkObject playerNetworkObject = client.PlayerObject;
            ulong networkId = playerNetworkObject.NetworkObjectId;
            SetPositionForEachClientRpc(clientId, networkId, chosenTransform);
            // Move the player's object to the chosen position
            if (playerNetworkObject != null)
            {
                Debug.LogError(" player object found for client: " + clientId);
                Rigidbody rb = playerNetworkObject.gameObject.GetComponent<Rigidbody>();

                // Check if the component exists
                if (rb != null)
                {
                    // Remove the component
                    Destroy(rb);
                }
                playerNetworkObject.transform.position = chosenTransform;
                playerNetworkObject.transform.rotation = transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, 255f, transform.rotation.eulerAngles.z);
                if (IsServer)
                {
                    SendRpcToFollowCameraClientRpc(clientId, networkId,randomNumber);
                }
            }
            else
            {
                Debug.LogError("No player object found for client: " + clientId);
            }
        }
        else
        {
            Debug.LogError("Client not found: " + clientId);
        }

        // Additional code here for when a player joins
    }

    [ClientRpc]
    private void SetPositionForEachClientRpc(ulong clientId, ulong networkId,Vector3 chosenTransform)
    {
      /*  if (NetworkManager.Singleton.LocalClientId == clientId)
        {*/

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject networkObject))
            {
                Debug.Log("Found NetworkObject: " + networkObject.gameObject.name);
            if (!IsOwner)
            {
                Rigidbody rb = networkObject.gameObject.GetComponent<Rigidbody>();

                // Check if the component exists
                if (rb != null)
                {
                    // Remove the component
                    Destroy(rb);
                }
               
            }
                networkObject.gameObject.transform.position = chosenTransform;
                networkObject.gameObject.transform.rotation = transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, 255f, transform.rotation.eulerAngles.z);
                // Perform actions on the NetworkObject
            }
            else
            {
                Debug.LogError("No NetworkObject found with ID: " + networkId);
            }
       /* }*/
    }



    [ClientRpc]
    private void SendRpcToFollowCameraClientRpc(ulong clientId, ulong networkId,int randomNumber)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject networkObject))
            {
                Debug.Log("Found NetworkObject: " + networkObject.gameObject.name);
                cameraFollow.SetCameraToFollowPlayer(networkObject.gameObject);

                // Perform actions on the NetworkObject
            }
            else
            {
                Debug.LogError("No NetworkObject found with ID: " + networkId);
            }
        }
    }
}
