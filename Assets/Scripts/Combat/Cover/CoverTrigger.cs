using UnityEngine;

public class CoverTrigger : MonoBehaviour
{
    [SerializeField] private DestructibleCover ownerCover;

    private void Awake()
    {
        if (ownerCover == null)
        {
            Debug.LogError("Owner cover is not assigned in CoverTrigger.", this);
            return;
        }
    }

    private void OnDisable()
    {
        ownerCover.ClearTargets();
    }

    private void OnTriggerEnter(Collider other)
    {
        ownerCover.RegisterTarget(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ownerCover.UnregisterTarget(other);
    }
}
