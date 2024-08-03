using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.Networking;

public class MatchmakingServer : NetworkBehaviour
{
    IMultiplayService _multiplayService;
    [SerializeField]
    int maxNumberOfPlayers = 6;
    const int _multiplayServiceTimeout = 20000;
    private string _allocationId;
    private MultiplayEventCallbacks _serverCallbacks;
    private IServerEvents _serverEvents;
    CreateBackfillTicketOptions _createBackfillTicketOptions;
    private string _externalServerIP = "0.0.0.0";
    private ushort _serverPort = 6666;
 
    private string _externalConnectionString => $"{_externalServerIP}:{_externalServerIP}";
    private bool backfilling = false;
    private Task currentBackfillTask = null;
    private bool IsBackFillTicketNeedUpdate = false;
    public int playerCount = 0;
    private MatchmakingResults matchmakerPayload;
    private BackfillTicket _localBackfillTicket;

    // Start is called before the first frame update
   async void Start()
    {
/*        StartCoroutine(GetPublicIP());
*/
        maxNumberOfPlayers = 6;
        print("matchmaker has started");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

        NetworkManager.Singleton.StartServer();

        await StartServerServices();
    }

    
    async Task StartServerServices()
    {
        print("start server sarted");


        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        try
        {
            print("start server services");

            _multiplayService = MultiplayService.Instance;
            await _multiplayService.StartServerQueryHandlerAsync((ushort)maxNumberOfPlayers, "TempoQueue", "n/a", "0", "n/a");

        }

        catch (Exception ex)
        {
            Debug.LogWarning("Something went wrong + \n" + ex);

        }

        try
        {
            matchmakerPayload = await GetMatchMakerPayload(_multiplayServiceTimeout);
            print("headless node in start game");

            //print("matchmakerpayload" + matchmakerPayload.MatchId);
            if (matchmakerPayload != null)
            {
                Debug.Log("matchid is" + matchmakerPayload.MatchId);
                await StartBackfill(matchmakerPayload);

              /*  NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

                NetworkManager.Singleton.StartServer();*/
            }
            else
            {

                Debug.LogWarning("paylioad timeout ,starting with defaults.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Something went wrong to set up allocation and backfill services + \n" + ex);

        }
    }

    private async Task StartBackfill(MatchmakingResults payload)
    {
        print("start backfill");
        var backfillProperties = new BackfillTicketProperties(payload.MatchProperties);
        _localBackfillTicket = new BackfillTicket { Id = payload.MatchProperties.BackfillTicketId, Properties = backfillProperties };
        await BeginBackfilling(payload);
    }
    private async Task BeginBackfilling(MatchmakingResults payload)
    {
        print("begin backfilling is called");
        var matchProperties = payload.MatchProperties;

        if (string.IsNullOrEmpty(_localBackfillTicket.Id))
        {
            _createBackfillTicketOptions = new CreateBackfillTicketOptions
            {
                Connection = _externalConnectionString,
                QueueName = payload.QueueName,
                Properties = new BackfillTicketProperties(matchProperties)
            };
            _localBackfillTicket.Id = await MatchmakerService.Instance.CreateBackfillTicketAsync(_createBackfillTicketOptions);
        }
        print("backfill after creating:" + _localBackfillTicket.Properties.MatchProperties.Players.Count);
        print("team player id count backfill after creating:" + _localBackfillTicket.Properties.MatchProperties.Teams[0].PlayerIds.Count);
        backfilling = true;
        currentBackfillTask = null;
#pragma warning disable 4014
        BackfillLoop();
#pragma warning restore 4014
    }
    private async Task BackfillLoop()
    {
        while (backfilling)
        {
            if (IsBackFillTicketNeedUpdate)
            {
                await MatchmakerService.Instance.UpdateBackfillTicketAsync(_localBackfillTicket.Id, _localBackfillTicket);
                print("backfill ticket update player count" + _localBackfillTicket.Properties.MatchProperties.Players.Count);
                for (int i = 0; i < _localBackfillTicket.Properties.MatchProperties.Teams[0].PlayerIds.Count; i++)
                {
                    print("player ids after update" + _localBackfillTicket.Properties.MatchProperties.Teams[0].PlayerIds[i]);
                }

                IsBackFillTicketNeedUpdate = false;
            }
            else
            {
                _localBackfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(_localBackfillTicket.Id);

            }
            if (!NeedsPlayers())
            {
                print("deleted backfill player");
                await MatchmakerService.Instance.DeleteBackfillTicketAsync(_localBackfillTicket.Id);
                _localBackfillTicket.Id = null;
                backfilling = false;
                return;
            }

            await Task.Delay(1000);
        }
        backfilling = false;
    }
    private bool NeedsPlayers()
    {
        return playerCount < maxNumberOfPlayers;
    }
    private async Task<MatchmakingResults> GetMatchMakerPayload(int timeout)
    {
        var matchmakerPayLoadTask = SubscribeAndAwaitMatchMakerAllocation();
        //either or
        if (await Task.WhenAny(matchmakerPayLoadTask, Task.Delay(timeout)) == matchmakerPayLoadTask)
        {
            return matchmakerPayLoadTask.Result;
        }
        return null;
    }

    private async Task<MatchmakingResults> SubscribeAndAwaitMatchMakerAllocation()
    {

        if (_multiplayService == null) return null;
        _allocationId = null;

        _serverCallbacks = new MultiplayEventCallbacks();
        _serverCallbacks.Allocate += OnMultiplayAllocation;
        await _multiplayService.SubscribeToServerEventsAsync(_serverCallbacks);
        _allocationId = await AwaitAllocationId();

        //allocation id to find  a match and created   

        //get information of match
        //match maker payload
        var mmPayLoad = await GetMatchmakerAllocationPayloadAsync();
        return mmPayLoad;

    }

    private async Task<MatchmakingResults> GetMatchmakerAllocationPayloadAsync()
    {

        try
        {
            var payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<MatchmakingResults>();

            var modelAsJson = JsonConvert.SerializeObject(payloadAllocation);

            Debug.Log(nameof(GetMatchmakerAllocationPayloadAsync) + "model" + modelAsJson);
            return payloadAllocation;
        }
        catch (Exception ex)
        {
            Debug.Log("something went wrong trying to get the match in GetMatchmakerAllocationPayloadAsync \n" + ex);
        }

        return null;
    }

    private async Task<string> AwaitAllocationId()
    {

        var config = _multiplayService.ServerConfig;
        Debug.Log("awaiting allocation server congif is: \n" + "ServerID" + "serverID:" + config.ServerId + "\n" + "-port" + config.Port + "\n");

        while (string.IsNullOrEmpty(_allocationId))
        {
            var configId = config.AllocationId;
            if (!string.IsNullOrEmpty(configId) && string.IsNullOrEmpty(_allocationId))
            {
                _allocationId = configId;
                break;
            }

            await Task.Delay(100);
        }
        return _allocationId;

    }
    private void OnMultiplayAllocation(MultiplayAllocation allocation)
    {
        Debug.Log("On Allocation" + allocation.AllocationId);
        if (string.IsNullOrEmpty(allocation.AllocationId)) return;
        _allocationId = allocation.AllocationId;
    }
    // Update is called once per frame
    
}
