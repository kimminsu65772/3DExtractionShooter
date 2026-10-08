using UnityEngine;

public static class WeaponSpreadUtility
{
    // 무기의 기본 탄퍼짐과 현재 누적값에 공격 주체의 보정 배율을 적용한다.
    public static float GetEffectiveSpreadAngle(WeaponDefinition weapon, float currentSpreadAngle, float spreadMultiplier, bool isAiming = false)
    {
        if (weapon == null)
            return 0f;

        float baseAngle = isAiming && weapon.adsBaseSpreadAngle >= 0f ? weapon.adsBaseSpreadAngle : weapon.baseSpreadAngle;
        float maxAngle = isAiming && weapon.adsMaxSpreadAngle >= 0f ? weapon.adsMaxSpreadAngle : weapon.maxSpreadAngle;
        float spreadAngle = (baseAngle + Mathf.Max(0f, currentSpreadAngle)) * Mathf.Max(0f, spreadMultiplier);

        if (maxAngle > 0f)
            spreadAngle = Mathf.Min(spreadAngle, maxAngle);

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
    public static float IncreaseSpread(float currentSpreadAngle, WeaponDefinition weapon, bool isAiming = false)
    {
        if (weapon == null)
            return Mathf.Max(0f, currentSpreadAngle);

        float growth = isAiming && weapon.adsSpreadIncreasePerShot >= 0f ? weapon.adsSpreadIncreasePerShot : weapon.spreadIncreasePerShot;
        float maximum = isAiming && weapon.adsMaxSpreadAngle >= 0f ? weapon.adsMaxSpreadAngle : weapon.maxSpreadAngle;
        float nextSpreadAngle = Mathf.Max(0f, currentSpreadAngle) + Mathf.Max(0f, growth);

        if (maximum > 0f)
            nextSpreadAngle = Mathf.Min(nextSpreadAngle, maximum);

        return nextSpreadAngle;
    }

    // 발사하지 않는 동안 누적 탄퍼짐을 0을 향해 회복시킨다.
    public static float RecoverSpread(float currentSpreadAngle, WeaponDefinition weapon, float deltaTime, bool isAiming = false)
    {
        if (weapon == null || deltaTime <= 0f)
            return Mathf.Max(0f, currentSpreadAngle);
        float recovery = isAiming && weapon.adsSpreadRecoverySpeed >= 0f ? weapon.adsSpreadRecoverySpeed : weapon.spreadRecoverySpeed;
        return Mathf.MoveTowards(currentSpreadAngle, 0f, Mathf.Max(0f, recovery) * deltaTime);
    }

    // 산탄은 발사 방향을 중심으로 한 원뿔 안에 배치한다. 각도는 중심에서 한쪽까지의 범위다.
    public static Vector3 ApplyConeSpread(Vector3 direction, float halfAngleDegrees)
    {
        if (direction.sqrMagnitude < 0.0001f) return Vector3.zero;
        Vector3 forward = direction.normalized;
        if (halfAngleDegrees <= 0f) return forward;
        Vector3 right = Vector3.Cross(Mathf.Abs(forward.y) > 0.99f ? Vector3.right : Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right);
        // 면적이 넓은 바깥쪽도 고르게 뽑기 위해 각도 대신 각도의 코사인을 추첨한다.
        float cosine = Random.Range(Mathf.Cos(Mathf.Clamp(halfAngleDegrees, 0f, 89f) * Mathf.Deg2Rad), 1f);
        float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
        float rotation = Random.Range(0f, Mathf.PI * 2f);
        return (forward * cosine + (right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation)) * sine).normalized;
    }
}
