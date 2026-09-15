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

    public static Vector3 GetHorizontalDistance(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        return direction;
    }
}
