using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class BreakableObject : MonoBehaviour
{
    public LayerMask breakerCollisionLayers;
    public List<Rigidbody> spawnedPieces;
    public VisualEffect poofEffect;

    public void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & breakerCollisionLayers.value) != 0)
        {
            var collidingBody = other.attachedRigidbody;

            if (collidingBody == null)
                return;

            var velocity = collidingBody.velocity;

            Poof(velocity);
        }
    }

    private void Poof(Vector3 velocity)
    {
        poofEffect.Play();

        Debug.Log("Poof");

        foreach (var part in spawnedPieces)
        {
            Debug.Log(part);
            part.transform.SetParent(transform.parent, worldPositionStays: true);
            part.velocity = velocity;
            part.gameObject.SetActive(true);
        }
    }
}
