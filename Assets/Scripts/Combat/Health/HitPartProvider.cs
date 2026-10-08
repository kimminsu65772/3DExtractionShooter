using System.Collections.Generic;
using UnityEngine;

public class HitPartProvider : MonoBehaviour, IHitPartProvider
{
    [SerializeField] private HitBox[] hitBoxes;

    private readonly Dictionary<Collider, HitPart> hitPartMap = new ();

    private void Awake()
    {
        BuildHitPartMap();
    }

    public HitPart GetHitPart(Collider collider)
    {
        if (collider == null)
        {
            Debug.LogWarning("Collider가 null입니다.", this);
            return HitPart.None;
        }

        if (hitPartMap.TryGetValue(collider, out HitPart hitPart))
        {
            return hitPart;
        }

        return HitPart.None;
    }

    private void BuildHitPartMap()
    {
        hitPartMap.Clear();

        if (hitBoxes == null)
        {
            Debug.LogWarning("할당된 히트 박스가 없습니다.", this);
            return;
        }

        if (hitBoxes.Length == 0)
        {
            Debug.LogWarning("할당된 히트 박스 배열이 비어 있습니다.", this);
            return;
        }

        foreach (var hitBox in hitBoxes)
        {
            if (hitBox == null)
            {
                Debug.LogWarning("HitBox 배열에 null 값이 있습니다.", this);
                continue;
            }

            if (!hitBox.TryGetComponent(out Collider hitCollider))
            {
                Debug.LogWarning($"{hitBox.name}에 Collider가 없습니다.", hitBox);
                continue;
            }

            if (hitPartMap.ContainsKey(hitCollider))
            {
                Debug.LogWarning($"{hitCollider.name} Collider가 중복 등록되었습니다.", hitCollider);
            }

            hitPartMap[hitCollider] = hitBox.HitPart;
        }
    }

    public void InactiveHitBoxes()
    {
        // 히트박스를 본에 붙여도 사망 애니메이션이 유지되도록 오브젝트 대신 충돌체만 끈다.
        if (hitBoxes != null)
        {
            foreach (HitBox hitBox in hitBoxes)
            {
                if (hitBox == null) continue;
                foreach (Collider hitCollider in hitBox.GetComponents<Collider>())
                    hitCollider.enabled = false;
            }
        }
        hitPartMap.Clear();
    }
}
