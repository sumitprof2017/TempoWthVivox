using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Netcode;
public class SafaTempoController : InputHandler
{
    internal enum driveType
    {
        frontWheelDrive,
        rearWheelDrive,
        allWheelDrive
    }

    [SerializeField] private driveType drive;

    internal enum gearBox
    {
        automatic,
        manual
    }

    [SerializeField] private gearBox gearChange;

  

    public float totalPower;
    public float maxRPM = 2500, minRPM = 1200;
    public float wheelsRPM;
    public AnimationCurve enginePower;
    public float engineRPM;
    public float[] gears = new float[5];
    public int gearNum = 0;
    public float smoothTime = 0.01f;
    public bool reverse;

    public float wheelTorque;
    public Rigidbody playerRb;
    public float KPH;
    private float conversionFactorKPH = 3.6f;
    public float brakePower = 9999999999999;

    private InputHandler inputMgr;
    //private KeyboardInput inputMgr;
    public GameObject wheelMeshes, wheelColliders;
    public WheelCollider[] wheelColl = new WheelCollider[3];
    public GameObject[] wheelMesh = new GameObject[3];

    public float downForceValue = 50.0f;
    public GameObject centerOfMass;




    public float steerAngle;
    public float steerMax = 25.0f;
    //public float radius = 0.24f;

    [SerializeField] int wheelsOnGround;
    private Queue<DriveData> inputQueue = new Queue<DriveData>();
    Rigidbody rb;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        GetObjects();
        inputMgr.driveInput = 0;
        inputMgr.steerInput = 0;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (IsOwner)
        {
           /* AnimateWheels();
            AddDownForce();

            DriveSafaTempo();
            SteerSafaTempo();
            CalculateEnginePower();
            GearShifter();*/
        }
    }

    public void StartTempoOverAllMovement()
    {
        AddDownForce();
        AnimateWheels();

        DriveSafaTempoFromServerToOtherClients();
        SteerSafaTempoFromServerToOtherClients();
        CalculateEnginePower();
        GearShifter();
    }

    private void CalculateEnginePower()
    {
        WheelRPM();

        totalPower = enginePower.Evaluate(engineRPM) * gears[gearNum];
        float velocity = 0.0f;
        engineRPM = Mathf.SmoothDamp(engineRPM, 600 + (Mathf.Abs(wheelsRPM) * 3.6f * (gears[gearNum])), ref velocity, smoothTime);
    }
    private void WheelRPM()
    {
        float sum = 0;
        int R = 0;
        for (int i = 0; i < wheelColl.Length; i++)
        {
            sum += wheelColl[i].rpm;
            R++;
            wheelsRPM = (R != 0) ? sum / R : 0;
        }
        
        if (wheelsRPM < 0 && !reverse)
        {
            reverse = true;
            //gameMgr.changeGear();
        }
        else if (wheelsRPM > 0 && reverse)
        {
            reverse = false;
            //gameMgr.changeGear();
        }
        
    }


    private void GearShifter()
    {

        if (!IsOnGround()) { return; }
        //Automatic Gear Change
        if (gearChange == gearBox.automatic)
        {
            if (engineRPM > maxRPM && gearNum < gears.Length - 1 && !reverse)
            {
                gearNum++;
                //gameMgr.changeGear();
            }
            if (engineRPM < minRPM && gearNum > 0)
            {
                gearNum--;
                //gameMgr.changeGear();
            }
        }

        //Manual Gear Change
        else
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                gearNum++;
               // gameManager.changeGear();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                gearNum--;
                //gameManager.changeGear();
            }
        }
    }

    public void LocalMovement(DriveData data)
    {
        if (!IsOwner) return;
        print("local movement data is" + data.driveInput);
        print("local movement steer in put is" + data.steerInput);
        ApplyTorque(data);  // Client-side prediction
        SteerSafaTempo(data);
        AddDownForce();

    }

    public void LocalMovementNew(InputPayload inputPayload)
    {
        print("local movement data new  is" + inputPayload.driveInput);
        print("local movement steer new  in put is" + inputPayload.steerInput);
        ApplyTorqueNew(inputPayload);  // Client-side prediction
        SteerSafaTempoNew(inputPayload);
        AddDownForce();

    }
    public void ReceiveInputFromClient(DriveData data,Vector3 currentClientPosition)
    {
/*        gameObject.transform.position = currentClientPosition;
*/        if (data.isDataSent)
        {

       
        ApplyTorque(data);  // Recalculate from server side
        SteerSafaTempo(data);
          
        }
        /*        serverPosition.Value = playerRb.position;  // Update server's authoritative position
        */
/*        UpdateClientPositionsClientRpc(playerRb.position);
*/    }

    [ClientRpc]
    void UpdateClientPositionsClientRpc(Vector3 authoritativePosition)
    {
        if (!IsOwner)
        {
            // Smoothly correct client prediction
            playerRb.position = Vector3.Lerp(playerRb.position, authoritativePosition, Time.fixedDeltaTime * 5);
        }
    }

    //for later purpose apptorque
    /* float torque = data.driveInput * (totalPower / wheelColliders.Length) * gears[currentGear];
     foreach (WheelCollider wheel in wheelColliders)
     {
         wheel.motorTorque = torque;
     }*/
    private void ApplyTorqueNew(InputPayload inputPayload)
    {
        //new code
        /*    float targetSpeed = inputPayload.driveInput * totalPower / 5;
            Vector3 forwardWithoutY = transform.forward;
            forwardWithoutY.y = 0;
            forwardWithoutY.Normalize();
            rb.velocity = Vector3.Lerp(rb.velocity, forwardWithoutY * targetSpeed, 0.016f);*/

        if (drive == driveType.allWheelDrive)
        {
            for (int i = 0; i < wheelColl.Length; i++)
            {
                wheelTorque = wheelColl[i].motorTorque = inputPayload.driveInput * (totalPower / 3);

            }
        }
        else if (drive == driveType.rearWheelDrive)
        {
            for (int i = 1; i < wheelColl.Length; i++)
            {
                wheelColl[i].motorTorque = inputPayload.driveInput * (totalPower / 2);
            }
        }
        else
        {
            wheelColl[0].motorTorque = inputPayload.driveInput * totalPower;
        }

        KPH = playerRb.velocity.magnitude * conversionFactorKPH;

        if (inputPayload.brakeInput)
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = brakePower;
        }
        else
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = 0;
        }

    }

    private void ApplyTorque(DriveData data)
    {
        //for later purpose
        /* float torque = data.driveInput * (totalPower / wheelColliders.Length) * gears[currentGear];
         foreach (WheelCollider wheel in wheelColliders)
         {
             wheel.motorTorque = torque;
         }*/
        if (drive == driveType.allWheelDrive)
        {
            for (int i = 0; i < wheelColl.Length; i++)
            {
                wheelTorque = wheelColl[i].motorTorque = data.driveInput * (totalPower / 3);

            }
        }
        else if (drive == driveType.rearWheelDrive)
        {
            for (int i = 1; i < wheelColl.Length; i++)
            {
                wheelColl[i].motorTorque = data.driveInput * (totalPower / 2);
            }
        }
        else
        {
            wheelColl[0].motorTorque = data.driveInput * totalPower;
        }

        KPH = playerRb.velocity.magnitude * conversionFactorKPH;

        if (data.brakeInput)
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = brakePower;
        }
        else
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = 0;
        }

    }
    void DriveSafaTempo()
    {
        print("this is calleed");

        if (drive == driveType.allWheelDrive)
        {
            for (int i = 0; i < wheelColl.Length; i++)
            {
                wheelTorque = wheelColl[i].motorTorque = inputMgr.driveInput * (totalPower / 3);

            }
        }
        else if (drive == driveType.rearWheelDrive)
        {
            for (int i = 1; i < wheelColl.Length; i++)
            {
                wheelColl[i].motorTorque = inputMgr.driveInput * (totalPower / 2);
            }
        }
        else
        {
            wheelColl[0].motorTorque = inputMgr.driveInput * totalPower;
        }

        KPH = playerRb.velocity.magnitude * conversionFactorKPH;

        if (inputMgr.brakeInput)
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = brakePower;
        }
        else
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = 0;
        }
    }

    void DriveSafaTempoFromServerToOtherClients()
    {
        print("this is calleed");

        if (drive == driveType.allWheelDrive)
        {
            for (int i = 0; i < wheelColl.Length; i++)
            {
                wheelTorque = wheelColl[i].motorTorque = inputMgr.driveData.driveInput * (totalPower / 3);

            }
        }
        else if (drive == driveType.rearWheelDrive)
        {
            for (int i = 1; i < wheelColl.Length; i++)
            {
                wheelColl[i].motorTorque = inputMgr.driveInput * (totalPower / 2);
            }
        }
        else
        {
            wheelColl[0].motorTorque = inputMgr.driveInput * totalPower;
        }

        KPH = playerRb.velocity.magnitude * conversionFactorKPH;

        if (inputMgr.brakeInput)
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = brakePower;
        }
        else
        {
            wheelColl[0].brakeTorque = wheelColl[1].brakeTorque = wheelColl[2].brakeTorque = 0;
        }
    }
    IEnumerator WaitToRotate()
    {
        yield return new WaitForSeconds(5);
    }
    void SteerSafaTempo(DriveData data)
    {
        print("steer safa tempo is called");
        float trackWidth = 0.23f; //two rear wheel separation distance (vehicle width)
        float radius = 0.405f; //distance between center of curvature and center of vehicle 2 rear wheel difference
        float trackLength = 0.46f; //front and rear wheel separation distance (vehicle length)

        if (data.steerInput > 0)
        {
            //Mathf.Atan returns an angle in radians which is further converted into degree by Mathf.Rad2Deg
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius + (trackWidth / 2))) * data.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius - (0.028f / 2))) * inputMgr.steerInput;
        }
        else if (data.steerInput < 0)
        {
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius - (trackWidth / 2))) * data.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (0.028f / 2))) * inputMgr.steerInput;
        }
        else
        {
            wheelColl[0].steerAngle = 0;
            //wheelColl[1].steerAngle = 0;

        }
    }

    void SteerSafaTempoNew(InputPayload inputPayload)
    {
        print("steer safa tempo is called");
        float trackWidth = 0.23f; //two rear wheel separation distance (vehicle width)
        float radius = 0.405f; //distance between center of curvature and center of vehicle 2 rear wheel difference
        float trackLength = 0.46f; //front and rear wheel separation distance (vehicle length)
        //inputPayload.inputVector.y = steerinput
        if (inputPayload.steerInput > 0)
        {
            //Mathf.Atan returns an angle in radians which is further converted into degree by Mathf.Rad2Deg
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius + (trackWidth / 2))) * inputPayload.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius - (0.028f / 2))) * inputMgr.steerInput;
        }
        else if (inputPayload.steerInput < 0)
        {
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius - (trackWidth / 2))) * inputPayload.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (0.028f / 2))) * inputMgr.steerInput;
        }
        else
        {
            wheelColl[0].steerAngle = 0;
            //wheelColl[1].steerAngle = 0;

        }
    }
    void SteerSafaTempoFromServerToOtherClients()
    {

        float trackWidth = 0.23f; //two rear wheel separation distance (vehicle width)
        float radius = 0.405f; //distance between center of curvature and center of vehicle 2 rear wheel difference
        float trackLength = 0.46f; //front and rear wheel separation distance (vehicle length)

        if (inputMgr.steerInput > 0)
        {
            //Mathf.Atan returns an angle in radians which is further converted into degree by Mathf.Rad2Deg
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius + (trackWidth / 2))) * inputMgr.driveData.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius - (0.028f / 2))) * inputMgr.steerInput;
        }
        else if (inputMgr.steerInput < 0)
        {
            wheelColl[0].steerAngle = Mathf.Rad2Deg * Mathf.Atan(trackLength / (radius - (trackWidth / 2))) * inputMgr.driveData.steerInput;
            //wheelColl[1].steerAngle = Mathf.Rad2Deg * Mathf.Atan(2.55f / (radius + (0.028f / 2))) * inputMgr.steerInput;
        }
        else
        {
            wheelColl[0].steerAngle = 0;
            //wheelColl[1].steerAngle = 0;
        }
        // }

        //steerAngle = steerMax * inputMgr.steerInput;
        //wheelColl[0].steerAngle = steerAngle;
    }

    void AnimateWheels()
    {
        Vector3 wheelPosition = Vector3.zero;
        Quaternion wheelRotation = Quaternion.identity;

        for (int i = 0; i < wheelMesh.Length; i++)
        {
            wheelColl[i].GetWorldPose(out wheelPosition, out wheelRotation);
            wheelMesh[i].transform.position = wheelPosition;
            wheelMesh[i].transform.rotation = wheelRotation;
        }
    }
    //Do not let the tempo loose traction in higher speed
    private void AddDownForce()
    {
        playerRb.AddForce(-transform.up * downForceValue * playerRb.velocity.magnitude);
    }
    /*
    private void CenterOfMassShift()
    {
        
    }
    */
    private void GetObjects()
    {
        //inputMgr = GetComponent<KeyboardInput>();
        inputMgr = GetComponent<InputHandler>();
        playerRb = GetComponent<Rigidbody>();
        centerOfMass = GameObject.Find("CoM");
        playerRb.centerOfMass = centerOfMass.transform.localPosition;

    }

    bool IsOnGround()
    {
        wheelsOnGround = 0;
        for (int i = 0; i < wheelColl.Length; i++)
        {
            if (wheelColl[i].isGrounded)
            {
                wheelsOnGround++;
            }
        }
        if (wheelsOnGround >= 1)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    //code from gpt


    
}

