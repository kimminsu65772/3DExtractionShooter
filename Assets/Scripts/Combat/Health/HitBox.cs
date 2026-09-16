using UnityEngine;

public class HitBox : MonoBehaviour
{
    [SerializeField] private HitPart hitPart = HitPart.Body;

    public HitPart HitPart => hitPart;
}
