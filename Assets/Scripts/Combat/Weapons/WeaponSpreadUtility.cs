using UnityEngine;

public static class WeaponSpreadUtility
{
    // 무기의 기본 탄퍼짐과 현재 누적값에 공격 주체의 보정 배율을 적용한다.
    public static float GetEffectiveSpreadAngle(WeaponDefinition weapon, float currentSpreadAngle, float spreadMultiplier)
    {
        if (weapon == null)
            return 0f;

        float spreadAngle = (weapon.baseSpreadAngle + Mathf.Max(0f, currentSpreadAngle)) * Mathf.Max(0f, spreadMultiplier);

        if (weapon.maxSpreadAngle > 0f)
            spreadAngle = Mathf.Min(spreadAngle, weapon.maxSpreadAngle);

        return Mathf.Max(0f, spreadAngle);
    }

    // 기준 발사 방향을 중심으로 spreadAngle 크기의 원 안에서 무작위 발사 방향을 만든다.
    public static Vector3 ApplySpread(Vector3 baseDirection, float spreadAngle)
    {
        if (baseDirection.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 normalizedDirection = baseDirection.normalized;

        if (spreadAngle <= 0f)
            return normalizedDirection;

        Vector2 randomPoint = Random.insideUnitCircle * spreadAngle;
        Vector3 rightAxis = Vector3.Cross(Vector3.up, normalizedDirection).normalized;

        if (rightAxis.sqrMagnitude <= 0.0001f)
            rightAxis = Vector3.right;

        Quaternion yawRotation = Quaternion.AngleAxis(randomPoint.x, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(randomPoint.y, rightAxis);

        return (yawRotation * pitchRotation * normalizedDirection).normalized;
    }

    // 한 발을 발사한 뒤 무기의 증가량만큼 누적 탄퍼짐을 올린다.
    public static float IncreaseSpread(float currentSpreadAngle, WeaponDefinition weapon)
    {
        if (weapon == null)
            return Mathf.Max(0f, currentSpreadAngle);

        float nextSpreadAngle = Mathf.Max(0f, currentSpreadAngle) + Mathf.Max(0f, weapon.spreadIncreasePerShot);

        if (weapon.maxSpreadAngle > 0f)
            nextSpreadAngle = Mathf.Min(nextSpreadAngle, weapon.maxSpreadAngle);

        return nextSpreadAngle;
    }

    // 발사하지 않는 동안 누적 탄퍼짐을 0을 향해 회복시킨다.
    public static float RecoverSpread(float currentSpreadAngle, WeaponDefinition weapon, float deltaTime)
    {
        if (weapon == null || weapon.spreadRecoverySpeed <= 0f || deltaTime <= 0f)
            return Mathf.Max(0f, currentSpreadAngle);

        return Mathf.MoveTowards(currentSpreadAngle, 0f, weapon.spreadRecoverySpeed * deltaTime);
    }
}
