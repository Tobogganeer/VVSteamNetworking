using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VirtualVoid.Networking.Steam;

public class CameraFollow : MonoBehaviour
{
    public static Transform target;
    private Vector3 offset = new Vector3(0, 5f, -15f);
    public float speed = 5;

    public bool defaultWhenNull = false;

    void LateUpdate()
    {
        if (target != null)
        {
            //FollowPlayer();
            OutsideIn();
            //TopDown();
        }
        else
        {
            if (Client.LocalClient != null)
                target = Client.LocalClient.transform;
            if (defaultWhenNull) transform.position = Vector3.Lerp(transform.position, offset, Time.deltaTime * speed);
        }
    }

    private void FollowPlayer()
    {
        transform.position = Vector3.Lerp(transform.position, target.position + offset, Time.deltaTime * speed);
    }

    private void OutsideIn()
    {
        Vector3 directionOut = target.position.normalized;
        transform.position = Vector3.Lerp(transform.position, target.position + directionOut * 8 + Vector3.up * 4, Time.deltaTime * speed);
        transform.LookAt(Vector3.Lerp(target.position, Vector3.up * 4, 0.5f));
    }

    private void TopDown()
    {
        transform.position = Vector3.Lerp(transform.position, Vector3.up * 15 + target.position * 0.8f, Time.deltaTime * speed);
        transform.rotation = Quaternion.Euler(89, 0, 0);
    }
}
