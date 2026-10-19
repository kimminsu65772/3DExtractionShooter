using UnityEngine;

public class AimPointProvider : MonoBehaviour
{
    [SerializeField] private Transform headAimPoint;
    [SerializeField] private Transform bodyAimPoint;

    public Transform BodyAimPoint => bodyAimPoint;

    public Vector3 GetAimPosition(HitPart hitPart)
    {
        return hitPart switch
        {
            HitPart.Head => headAimPoint != null ? headAimPoint.position : transform.position,
            HitPart.Body => bodyAimPoint != null ? bodyAimPoint.position : transform.position,
            _ => transform.position,
        };
    }


}
