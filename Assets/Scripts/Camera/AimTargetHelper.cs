using UnityEngine;

public static class AimTargetHelper
{
    public static Vector3 GetMouseWorldPositionOnPlane(Camera targetCamera, Vector3 screenPosition, Vector3 planePoint)
    {
        if (targetCamera == null)
        {
            return planePoint;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.up, planePoint);

        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return planePoint;
    }

    public static bool TryGetMouseWorldHit(Camera targetCamera, Vector3 screenPosition, RaycastHit[] hitBuffer, LayerMask targetMask, out RaycastHit closestHit)
    {
        if (targetCamera == null || hitBuffer == null || hitBuffer.Length == 0)
        {
            closestHit = default;
            return false;
        }

        closestHit = default;
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);

        int hitCount = Physics.RaycastNonAlloc(ray, hitBuffer, Mathf.Infinity, targetMask, QueryTriggerInteraction.Collide);

        if (hitCount <= 0)
            return false;

        int closestIndex = -1;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            if(hitBuffer[i].collider == null)
                continue;

            float distance = hitBuffer[i].distance;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (closestIndex >= 0)
        {
            closestHit = hitBuffer[closestIndex];
            return true;
        }

        return false;
    }

    public static Vector3 GetHorizontalDistance(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        return direction;
    }
}
