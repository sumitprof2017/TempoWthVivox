using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using UnityEngine.Windows;
using Kart;

public class InputHandler : NetworkBehaviour
{

    //netcode 
    // Netcode general
    NetworkTimer networkTimer;
    const float k_serverTickRate = 30f; // 60 FPS
    const int k_bufferSize = 1024;

    // Netcode client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    ClientNetworkTransform clientNetworkTransform;

    // Netcode server specific
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    internal enum Driver
    {
        AI,
        Keyboard
    }
    [SerializeField] private Driver driverType;

    public float steerInput;
    public float driveInput;
    public bool brakeInput;
    public bool turboInput;
    public bool escapeInput;
    Rigidbody rb;
    private void Start()
    {
    }
    public override void OnNetworkSpawn()
    {
        Invoke(nameof(SetPositionbool), 5f);
        Application.targetFrameRate = 30;
    }
     void SetPositionbool()
    {
        isPositionSet = true;
    }

    void Update()
    {
        networkTimer.Update(Time.deltaTime);
      
       
    }
    bool isPositionSet = false;
    private void FixedUpdate()
    {

        while (networkTimer.ShouldTick() && isPositionSet)
        {
            HandleClientTick();
            HandleServerTick();
        }

        /* if (IsOwner && InputDataAvailable())
         {

             var data = CollectInput();

             safaTempoController.LocalMovement(data);
          //   SendInputToServerServerRpc(data, transform.position);


         }*/
    }

    void HandleClientTick()
    {
        if (!IsClient || !IsOwner) return;

        var currentTick = networkTimer.CurrentTick;
        var bufferIndex = currentTick % k_bufferSize;
      float driveInput = UnityEngine.Input.GetAxis("Vertical");
      float  steerInput = UnityEngine.Input.GetAxis("Horizontal");
        InputPayload inputPayload = new InputPayload()
        {
            tick = currentTick,
            timestamp = DateTime.Now,
            networkObjectId = NetworkObjectId,
            inputVector = new Vector3(UnityEngine.Input.GetAxis("Vertical"), 0, UnityEngine.Input.GetAxis("Horizontal")),
            position = transform.position,
            driveInput = driveInput,
            steerInput = steerInput
        };
       
        clientInputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);

        StatePayload statePayload = ProcessMovement(inputPayload);
        clientStateBuffer.Add(statePayload, bufferIndex);

        HandleServerReconciliation();
    }
    float  reconciliationThreshold = 10f;
    void HandleServerReconciliation()
    {
        if (!ShouldReconcile()) return;

        float positionError;
        int bufferIndex;

        bufferIndex = lastServerState.tick % k_bufferSize;
        if (bufferIndex - 1 < 0) return; // Not enough information to reconcile

        StatePayload rewindState = IsHost ? serverStateBuffer.Get(bufferIndex - 1) : lastServerState; // Host RPCs execute immediately, so we can use the last server state
        StatePayload clientState = IsHost ? clientStateBuffer.Get(bufferIndex - 1) : clientStateBuffer.Get(bufferIndex);
        positionError = Vector3.Distance(rewindState.position, clientState.position);

        if (positionError > reconciliationThreshold)
        {
            print("reconsile is called");
            ReconcileState(rewindState);
/*            reconciliationTimer.Start();
*/        }

        lastProcessedState = rewindState;
    }
    bool ShouldReconcile()
    {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default)
                                               || !lastProcessedState.Equals(lastServerState);

        return isNewServerState && isLastStateUndefinedOrDifferent;
    }

    void ReconcileState(StatePayload rewindState)
    {
        transform.position = rewindState.position;
        transform.rotation = rewindState.rotation;
        rb.velocity = rewindState.velocity;
        rb.angularVelocity = rewindState.angularVelocity;

        if (!rewindState.Equals(lastServerState)) return;

        clientStateBuffer.Add(rewindState, rewindState.tick % k_bufferSize);

        // Replay all inputs from the rewind state to the current state
        int tickToReplay = lastServerState.tick;

        while (tickToReplay < networkTimer.CurrentTick)
        {
            int bufferIndex = tickToReplay % k_bufferSize;
            StatePayload statePayload = ProcessMovement(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(statePayload, bufferIndex);
            tickToReplay++;
        }
    }

    void HandleServerTick()
    {
        if (!IsServer) return;

        int bufferIndex = -1;
        InputPayload inputPayload = default;

        // Limit the number of payloads processed per tick
        const int maxPayloadsPerTick = 10;
        int payloadsProcessed = 0;

        while (serverInputQueue.Count > 0 && payloadsProcessed < maxPayloadsPerTick)
        {
            inputPayload = serverInputQueue.Dequeue();

            bufferIndex = inputPayload.tick % k_bufferSize;

            float distance = Vector3.Distance(transform.position , inputPayload.position);
            if (distance < 10) {
                return;
            }
            StatePayload statePayload = ProcessMovement(inputPayload);
            serverStateBuffer.Add(statePayload, bufferIndex);

            payloadsProcessed++;
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
        /*        HandleExtrapolation(serverStateBuffer.Get(bufferIndex), CalculateLatencyInMillis(inputPayload));
        */
    }

    [ClientRpc]
    void SendToClientRpc(StatePayload statePayload)
    {
        if (!IsOwner) return;
        Vector3 newPosition = new Vector3(statePayload.position.x, 2.0f, statePayload.position.z);
        gameObject.transform.position = newPosition;
       
        lastServerState = statePayload;
    }

    [ServerRpc]
    void SendToServerRpc(InputPayload input)
    {
        Vector3 newPosition = new Vector3(input.position.x, 4, input.position.z);
        gameObject.transform.position = newPosition;
        serverInputQueue.Enqueue(input);

    }

    StatePayload ProcessMovement(InputPayload input)
    {
        safaTempoController.LocalMovementNew(input);

        return new StatePayload()
        {
            tick = input.tick,
            networkObjectId = NetworkObjectId,
            position = transform.position,
            rotation = transform.rotation,
            velocity = rb.velocity,
            angularVelocity = rb.angularVelocity
        };
    }

    [Range(0, 5)] public float steerForce;
   

    //access player and ai
    //private BikramTempoAI AIData;
    public GameObject AI;
    public GameObject player;

    SafaTempoController safaTempoController;

   

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        networkTimer = new NetworkTimer(k_serverTickRate);
        clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);

        serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        serverInputQueue = new Queue<InputPayload>();
        player = GameObject.FindGameObjectWithTag("Mala");
        AI = GameObject.FindGameObjectWithTag("Nagas");
        safaTempoController = GetComponent<SafaTempoController>();
        lastServerSequenceNumber.OnValueChanged += OnDataSent;

    }
    private void OnDataSent(int previous, int current)
    {
        print("change is called");
        safaTempoController.StartTempoOverAllMovement();

    }
    private NetworkVariable<int> lastServerSequenceNumber = new NetworkVariable<int>();
    // Update is called once per frame
 /*   void FixedUpdate()
    {
       
        if (driverType == Driver.AI)
        {
            AIDrive();

        }
        else
        {
         
            if (IsOwner && InputDataAvailable())
            {
              
               var data = CollectInput();
             
                safaTempoController.LocalMovement(data);
                SendInputToServerServerRpc(data,transform.position);
              

            }
            else
            {*//*
                if (!IsOwner)
                {

                    transform.position = Vector3.Lerp(transform.position, netPosition.Value, Time.fixedDeltaTime * 10);
                    transform.rotation = Quaternion.Lerp(transform.rotation, netRotation.Value, Time.fixedDeltaTime * 10);
                    return;

                }
            
                var data = CollectInput();
                data.isDataSent = false;*//*
           }

        }
   

        
       
    }*/

    [ServerRpc(RequireOwnership = false)]
    private void SendInputToServerServerRpc(DriveData data,Vector3 currentLocalPosition)
    {
/*        safaTempoController.ReceiveInputFromClient(data, currentLocalPosition);
*/    }
  
    private int localSequenceNumber = 0;

    [ServerRpc]
    public void MoveTempo_ServerRpc(DriveData driverdata, int sequenceNumber)
    {
        this.driveData = driverdata;
        if (sequenceNumber > lastServerSequenceNumber.Value)
        {
            print("driver data received" + driveData);
            print("driver data horzintal axia" + driveData.driveInput);
            print("driver data steering axia" + driveData.steerInput);
/*            safaTempoController.StartTempoOverAllMovement();
*/            lastServerSequenceNumber.Value = sequenceNumber;
        }
         
    }
    public DriveData driveData;
    private DriveData CollectInput()
    {
        return new DriveData
        {
            driveInput = UnityEngine.Input.GetAxis("Vertical"),
            steerInput = UnityEngine.Input.GetAxis("Horizontal"),
            brakeInput = UnityEngine.Input.GetButton("Jump"),
            turboInput = UnityEngine.Input.GetKey(KeyCode.LeftShift),
            escapeInput = UnityEngine.Input.GetKey(KeyCode.Escape),
            isDataSent = true
        };
    }
   



    private bool InputDataAvailable()
    {
        // Check if any input data is available
        return UnityEngine.Input.GetAxis("Vertical") != 0 ||
               UnityEngine.Input.GetAxis("Horizontal") != 0 ||
               UnityEngine.Input.GetButton("Jump") ||
               UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
               UnityEngine.Input.GetButton("Cancel");
    }
    private void AIDrive()
    {
        //AIData = AI.GetComponent<BikramTempoAI>();
        AISteer();
        driveInput = UnityEngine.Random.Range(0.3f,1.0f);
        //driveInput = 0;
        /*
        if (sensorData.hitDetect && !AIData.stuck)
        {
            brakeInput = true;
        }
        else 
        {
            brakeInput = false;
        }
        */
    }

    private void KeyboardDrive()
    {
        driveInput = UnityEngine.Input.GetAxis("Vertical");
        steerInput = UnityEngine.Input.GetAxis("Horizontal");
        brakeInput = (UnityEngine.Input.GetAxis("Jump") != 0) ? true : false;
        turboInput = (UnityEngine.Input.GetKey(KeyCode.LeftShift) ? true : false);
        escapeInput = (UnityEngine.Input.GetAxis("Cancel") != 0) ? true : false;
    }

    private void AISteer()
    {
        /*
        if (!AIData.playerInvisionRadius)
        {
            if (AIData.stuck)
            {
                Vector3 relative = transform.InverseTransformPoint(currentWaypoint.previousWaypoint);
                relative /= relative.magnitude;
                steerInput = (relative.x / relative.magnitude) * steerForce;
            }
            
           
            if (sensorData.hitDetect)
            {
                steerInput = sensorData.avoidMultiplier * steerForce;
                //steerInput = Mathf.Lerp(0.3f, sensorData.avoidMultiplier, 1.0f * Time.deltaTime) * steerForce;
                //steerInput = Mathf.Clamp(sensorData.avoidMultiplier * steerForce,-1.2f,1.2f);
            }
            else
            {
            Vector3 relative = transform.InverseTransformPoint(currentWaypoint.currentWaypoint);
                relative /= relative.magnitude;
                steerInput = (relative.x / relative.magnitude) * steerForce;
            }
        }
        else
        {
            Vector3 relative = transform.InverseTransformPoint(player.transform.position);
            relative /= relative.magnitude;
            steerInput = (relative.x / relative.magnitude) * steerForce;
        }
        */
    }



}
public struct DriveData : INetworkSerializeByMemcpy
{
    public float driveInput;
    public float steerInput;
    public bool brakeInput;
    public bool turboInput;
    public bool escapeInput;
    public bool isDataSent;
    
}
public struct InputPayload : INetworkSerializable
{
    public int tick;
    public DateTime timestamp;
    public ulong networkObjectId;
    public Vector3 inputVector;
    public Vector3 position;
    public bool brakeInput;
    public float driveInput;
    public float steerInput;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref timestamp);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref inputVector);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref brakeInput);
    }
}

public struct StatePayload : INetworkSerializable
{
    public int tick;
    public ulong networkObjectId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref networkObjectId);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref angularVelocity);
    }
}