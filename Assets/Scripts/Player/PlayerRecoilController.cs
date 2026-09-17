using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRecoilController : MonoBehaviour
{
    private WeaponDefinition currentWeapon;
    private Vector2 currentRecoilOffset;
    private float currentSpreadAngle;

    public Vector2 CurrentRecoilOffset => currentRecoilOffset;
    public float CurrentSpreadAngle => currentSpreadAngle;

    private void Update()
    {
        RecoverRecoil();
        RecoverSpread();
    }

    // 반동이 적용된 발사 방향을 반환한다.
    // baseDirection은 플레이어가 바라보는 방향이며, weapon은 현재 사용 중인 무기 정의, isAiming은 조준 여부, isMoving은 이동 여부를 나타낸다.
    public Vector3 GetFireDirection(Vector3 baseDirection, WeaponDefinition weapon, bool isAiming, bool isMoving)
    {
        if (weapon == null)
            return baseDirection.normalized;

        currentWeapon = weapon;

        Vector3 recoilDirection = ApplyRecoil(baseDirection.normalized);
        float effectiveSpreadAngle = GetEffectiveSpreadAngle(weapon, isAiming, isMoving);

        return ApplySpread(recoilDirection, effectiveSpreadAngle);
    }

    // 무기 발사 시 반동과 스프레드를 적용한다.
    public void AddRecoil(WeaponDefinition weapon)
    {
        if (weapon == null)
            return;

        currentWeapon = weapon;

        // 무기의 수직 및 수평 반동 값을 범위 내에서 랜덤하게 적용하여 현재 반동 오프셋을 업데이트한다.
        currentRecoilOffset += new Vector2(
            Random.Range(-weapon.horizontalRecoilPerShot, weapon.horizontalRecoilPerShot),
            weapon.verticalRecoilPerShot
        );

        // 현재 반동 오프셋이 무기의 최대 반동 오프셋을 초과하지 않도록 제한한다.
        if (weapon.maxRecoilOffset > 0f)
        {
            currentRecoilOffset = Vector2.ClampMagnitude(currentRecoilOffset, weapon.maxRecoilOffset);
        }

        // 현재 스프레드 각도를 무기의 스프레드 증가량만큼 증가시키고, 최대 스프레드 각도를 초과하지 않도록 제한한다.
        currentSpreadAngle += weapon.spreadIncreasePerShot;

        if (weapon.maxSpreadAngle > 0f)
        {
            currentSpreadAngle = Mathf.Min(currentSpreadAngle, weapon.maxSpreadAngle);
        }
    }

    // 반동과 스프레드가 시간이 지남에 따라 회복되도록 처리한다.
    private void RecoverRecoil()
    {
        if (currentWeapon == null || currentWeapon.recoilRecoverySpeed <= 0f)
            return;

        currentRecoilOffset = Vector2.MoveTowards(
            currentRecoilOffset,
            Vector2.zero,
            currentWeapon.recoilRecoverySpeed * Time.deltaTime
        );
    }

    private void RecoverSpread()
    {
        if (currentWeapon == null || currentWeapon.spreadRecoverySpeed <= 0f)
            return;

        currentSpreadAngle = Mathf.MoveTowards(
            currentSpreadAngle,
            0f,
            currentWeapon.spreadRecoverySpeed * Time.deltaTime
        );
    }

    private Vector3 ApplyRecoil(Vector3 baseDirection)
    {
        if (currentRecoilOffset.sqrMagnitude <= 0f)
            return baseDirection;

        // y축과 발사 방향의 외적을 계산하여 오른쪽 축을 구한다. 만약 외적의 크기가 거의 0이면, 오른쪽 축을 Vector3.right로 설정한다.
        Vector3 rightAxis = Vector3.Cross(Vector3.up, baseDirection).normalized;

        if (rightAxis.sqrMagnitude <= 0.0001f)
        {
            rightAxis = Vector3.right;
        }

        // y축을 기준으로 수평 반동을 적용한다.
        Quaternion yawRotation = Quaternion.AngleAxis(currentRecoilOffset.x, Vector3.up);

        // 오른쪽 축을 기준으로 수직 반동을 적용한다.
        Quaternion pitchRotation = Quaternion.AngleAxis(-currentRecoilOffset.y, rightAxis);

        return (yawRotation * pitchRotation * baseDirection).normalized;
    }

    private float GetEffectiveSpreadAngle(WeaponDefinition weapon, bool isAiming, bool isMoving)
    {
        float spreadAngle = weapon.baseSpreadAngle + currentSpreadAngle;

        if (isAiming)
        {
            spreadAngle *= weapon.aimSpreadMultiplier;
        }

        if (isMoving)
        {
            spreadAngle *= weapon.moveSpreadMultiplier;
        }

        if (weapon.maxSpreadAngle > 0f)
        {
            spreadAngle = Mathf.Min(spreadAngle, weapon.maxSpreadAngle);
        }

        return Mathf.Max(0f, spreadAngle);
    }

    private Vector3 ApplySpread(Vector3 baseDirection, float spreadAngle)
    {
        if (spreadAngle <= 0f)
            return baseDirection;

        // 일정 범위 내에서 스프레드 각도에 따라 랜덤한 점을 생성한다.
        Vector2 randomPoint = Random.insideUnitCircle * spreadAngle;
        Vector3 rightAxis = Vector3.Cross(Vector3.up, baseDirection).normalized;

        if (rightAxis.sqrMagnitude <= 0.0001f)
        {
            rightAxis = Vector3.right;
        }

        // Y축을 기준으로 수평 스프레드를 적용하고, 오른쪽 축을 기준으로 수직 스프레드를 적용한다.
        Quaternion yawRotation = Quaternion.AngleAxis(randomPoint.x, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(randomPoint.y, rightAxis);

        return (yawRotation * pitchRotation * baseDirection).normalized;
    }
}
