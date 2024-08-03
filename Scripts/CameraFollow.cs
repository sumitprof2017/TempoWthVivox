using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    internal enum SelectPlayer
    {
        safaTempo,
        bikramTempo,
        tractor
    }
    [SerializeField] private SelectPlayer selectedPlayer;

    private InputHandler inputMgr;
    public GameObject focalPlayer;
    public SafaTempoController safaTempo;


    public GameObject camFocalPoint;
    private Rigidbody playerRb;
    public Vector3 offset;
    private float camSpeed = 20.0f;
    private float focalPlayerSpeed;
    public float defaultFoV = 0, desiredFoV = 0;
    [Range(0, 10)] public float smoothTime = 0;
    // Start is called before the first frame update
    void Awake()
    {

        /*  focalPlayer = GameObject.FindGameObjectWithTag("Mala");
          safaTempo = focalPlayer.GetComponent<SafaTempoController>();

          camFocalPoint = focalPlayer.transform.Find("CamFocalPoint").gameObject;
          playerRb = focalPlayer.GetComponent<Rigidbody>();

          defaultFoV = Camera.main.fieldOfView;*/
    }

    private void Start()
    {
    }
    bool startCameraFollowAfterPlayerJoin = false;


    public void SetCameraToFollowPlayer(GameObject safaTempoGameObject)
    {


        focalPlayer = safaTempoGameObject.gameObject;
        safaTempo = focalPlayer.GetComponent<SafaTempoController>();
        //tempo mathi ko position end point not setting int he middle
        camFocalPoint = focalPlayer.transform.Find("CamFocalPoint").gameObject;
        transform.position = Vector3.Lerp(transform.position, camFocalPoint.transform.position + camFocalPoint.transform.TransformVector(offset), Time.deltaTime * 0.01f);

        playerRb = focalPlayer.GetComponent<Rigidbody>();

        defaultFoV = Camera.main.fieldOfView;
        startCameraFollowAfterPlayerJoin = true;
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        if (startCameraFollowAfterPlayerJoin)
            FollowPlayer();
    }
    public Vector3 offsetPosition; // Offset position relative to the car
    public Vector3 offsetRotation; // Offset rotation relative to the car
    void FollowPlayer()
    {
     

        transform.position = safaTempo.gameObject.transform.position + safaTempo.gameObject.transform.TransformDirection(offsetPosition);
        transform.rotation = Quaternion.Euler(safaTempo.gameObject.transform.eulerAngles + offsetRotation);
    }
}