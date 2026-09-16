using UnityEngine;

[DisallowMultipleComponent]
public class WeaponView : MonoBehaviour
{
    [SerializeField] private Transform muzzleTransform;

    public Transform MuzzleTransform => muzzleTransform;
    public bool HasMuzzle => muzzleTransform != null;

    private void Reset()
    {
        muzzleTransform = transform.Find("MuzzlePoint");
    }
}
