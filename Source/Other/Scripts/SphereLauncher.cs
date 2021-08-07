using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereLauncher : MonoBehaviour
{
    public float force = 100f;
    public float verticalMult = 3f;
    public VirtualVoid.Networking.Steam.NetworkAnimator animator;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Rigidbody rb))
        {
            rb.AddForce((rb.position - transform.position + Vector3.up * verticalMult) * rb.velocity.magnitude * force, ForceMode.VelocityChange);
            animator.Play("Bounce");
        }
    }
}
