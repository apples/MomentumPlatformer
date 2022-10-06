using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class GroundSensor : MonoBehaviour
{
    private CapsuleCollider capsuleCollider;
    // Start is called before the first frame update
    void Start()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
        
    }

    // Update is called once per frame
    void Update()
    {
        // if (Physics.Raycast(transform.position, -transform.up, out var hit, 100f))
        if (CastCollider(transform.position, transform.rotation, -transform.up, 100f, out var hit))
        {
            Debug.DrawLine(transform.position, hit.point, Color.red);
            Debug.DrawLine(hit.point, hit.point + hit.normal, Color.green);

            if (hit.collider.gameObject.GetComponent<Terrain>() != null)
            {

                var terrain = hit.collider.gameObject.GetComponent<Terrain>();
                var terrainData = terrain.terrainData;
                var terrainPos = terrain.transform.position;
                var hitOK =
                    Vector3.Angle(Vector3.up, hit.normal) < 89f &&
                    ((hit.point.y - terrainPos.y) - 
                    terrainData.GetInterpolatedHeight((hit.point.x - terrainPos.x) / terrainData.size.x,
                        (hit.point.z - terrainPos.z) / terrainData.size.z)) < 0.001f;
                if (hitOK)
                {
                    var normal = terrainData.GetInterpolatedNormal((hit.point.x - terrainPos.x) / terrainData.size.x, (hit.point.z - terrainPos.z) / terrainData.size.z);
                    Debug.DrawLine(hit.point, hit.point + normal, Color.blue);
                }
            }
        }
    }

    private bool CastCollider(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit hit)
    {
        var halfHeight = Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
        var p1 = rotation * (capsuleCollider.center + halfHeight) + position;
        var p2 = rotation * (capsuleCollider.center - halfHeight) + position;

        var hits = Physics.CapsuleCastAll(p1, p2, capsuleCollider.radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);

        hit = hits.Where(h => h.collider.transform != transform).OrderBy(h => h.distance).FirstOrDefault();

        return hits.Any(h => h.collider.transform != transform);
    }

}
