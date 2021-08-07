using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking.Steam;
using Steamworks;

public class Player : Client
{
    private Rigidbody rb;
    private Vector2 input;
    public SteamId steamID;
    public float speed = 5;
    public float maxSpeed = 10;
    public float groundCheckLength = 1;
    public NetworkAnimator animator;

    private const ushort INPUT_ID = 1;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!SteamManager.IsServer) Destroy(rb);

        if (IsLocalPlayer) CameraFollow.target = transform;

        if (IsServer)
            transform.position = Testing.SpawnPoints[Random.Range(0, Testing.SpawnPoints.Length)].position;
    }

    private void FixedUpdate()
    {
        //SteamManager.SendMessageToServer(Message.Create(P2PSend.UnreliableNoDelay, 1).Add(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"))));
        if (IsLocalPlayer)
            SendCommand(Message.Create(P2PSend.UnreliableNoDelay, INPUT_ID).Add(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"))));

        transform.forward = -transform.position.normalized;

        if (IsServer)
        {
            if (Physics.Raycast(transform.position, Vector2.down, groundCheckLength))
            {
                rb.AddForce((input.x * transform.right + input.y * transform.forward) * speed, ForceMode.VelocityChange);
                rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxSpeed);
            }
            else
            {
                rb.AddForce((input.x * transform.right + input.y * transform.forward) * speed * 3, ForceMode.Acceleration);
            }

            if (transform.position.y < -5)
            {
                transform.position = Testing.SpawnPoints[Random.Range(0, Testing.SpawnPoints.Length)].position;
                //transform.position = new Vector3(-5, 1);
                rb.velocity = Vector3.up * 10;
            }
        }
    }

    protected override void OnCommandReceived(Message message, ushort messageID)
    {
        if (messageID == INPUT_ID)
        {
            input = message.GetVector2();
            animator.SetFloat("x", -input.x);
            animator.SetFloat("y", -input.y);
        }
    }

    private void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, Vector3.down * groundCheckLength, Color.red);
    }

    private void OnDestroy()
    {
        if (IsLocalPlayer)
        CameraFollow.target = null;
    }
}
