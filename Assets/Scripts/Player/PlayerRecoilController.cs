using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRecoilController : MonoBehaviour
{
    [SerializeField] private float recoilScreenMultiplier = 12f;
    [SerializeField] private CharacterStats characterStats;

    private WeaponDefinition currentWeapon;
    private Vector2 currentRecoilOffset;
    private float currentSpreadAngle;
    private bool isRecoveryBlocked;

    public Vector2 CurrentRecoilOffset => currentRecoilOffset;
    public float CurrentSpreadAngle => currentSpreadAngle;

    public Vector2 CurrentScreenRecoilOffset
    {
        get
        {
            return new Vector2(currentRecoilOffset.x, currentRecoilOffset.y) * recoilScreenMultiplier;
        }
    }

    private void Awake()
    {
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }
    }

    private void Update()
    {
        RecoverRecoil();
        RecoverSpread();
    }

    // 스프레드가 적용된 발사 방향을 반환한다.
    // 반동은 화면 조준점 오프셋에 반영하고, 여기서는 탄퍼짐만 적용한다.
    public Vector3 GetFireDirection(Vector3 baseDirection, WeaponDefinition weapon, bool isAiming, bool isMoving)
    {
        if (weapon == null)
            return baseDirection.normalized;

        currentWeapon = weapon;

        float effectiveSpreadAngle = GetEffectiveSpreadAngle(weapon, isAiming, isMoving);

        return ApplySpread(baseDirection.normalized, effectiveSpreadAngle);
    }

    // 무기 발사 시 반동과 스프레드를 적용한다.
    public void AddRecoil(WeaponDefinition weapon, bool isAiming, Vector2 recoilDirection)
    {
        if (weapon == null)
            return;

        currentWeapon = weapon;

        float recoilMultiplier = GetEffectiveRecoilMultiplier(weapon, isAiming) * GetStatMultiplier(StatType.RecoilControlMultiplier);
        float horizontalRecoil = Random.Range(-weapon.horizontalRecoilPerShot, weapon.horizontalRecoilPerShot) * recoilMultiplier;

        Vector2 forward = recoilDirection.normalized;
        Vector2 right = new Vector2(forward.y, -forward.x);

        float verticalRecoil = weapon.verticalRecoilPerShot * recoilMultiplier;

        currentRecoilOffset += forward * verticalRecoil + right * horizontalRecoil;

        // 현재 스프레드 각도를 무기의 스프레드 증가량만큼 증가시키고, 최대 스프레드 각도를 초과하지 않도록 제한한다.
        currentSpreadAngle += weapon.spreadIncreasePerShot;

        if (weapon.maxSpreadAngle > 0f)
        {
            currentSpreadAngle = Mathf.Min(currentSpreadAngle, weapon.maxSpreadAngle);
        }
    }

    // 마우스 입력이 현재 반동 방향과 반대라면, 실제 조준 커서를 이동시키기 전에 반동 오프셋을 먼저 상쇄한다.
    public Vector2 ConsumeScreenRecoil(Vector2 screenDelta)
    {
        if (recoilScreenMultiplier <= 0f)
            return screenDelta;

        screenDelta.x = ConsumeScreenRecoilAxis(screenDelta.x, ref currentRecoilOffset.x);
        screenDelta.y = ConsumeScreenRecoilAxis(screenDelta.y, ref currentRecoilOffset.y);

        return screenDelta;
    }

    public void SetRecoveryBlocked(bool isAttacking)
    {
        if (isAttacking)
        {
            isRecoveryBlocked = true;
        }
        else
        {
            isRecoveryBlocked = false;
        }
    }

    // 반동과 스프레드가 시간이 지남에 따라 회복되도록 처리한다.
    private void RecoverRecoil()
    {
        if (currentWeapon == null || currentWeapon.recoilRecoverySpeed <= 0f || isRecoveryBlocked)
            return;

        currentRecoilOffset = Vector2.MoveTowards(
            currentRecoilOffset,
            Vector2.zero,
            currentWeapon.recoilRecoverySpeed * Time.deltaTime
        );
    }

    private void RecoverSpread()
    {
        if (currentWeapon == null || currentWeapon.spreadRecoverySpeed <= 0f || isRecoveryBlocked)
            return;

        currentSpreadAngle = Mathf.MoveTowards(
            currentSpreadAngle,
            0f,
            currentWeapon.spreadRecoverySpeed * Time.deltaTime
        );
    }

    private float ConsumeScreenRecoilAxis(float screenDelta, ref float recoilOffsetAxis)
    {
        if (Mathf.Approximately(screenDelta, 0f) || Mathf.Approximately(recoilOffsetAxis, 0f))
            return screenDelta;

        float recoilScreenOffset = recoilOffsetAxis * recoilScreenMultiplier;

        // 현재 반동과 같은 방향의 마우스 입력은 반동 제어가 아니므로 그대로 조준 커서 이동에 사용한다.
        if (Mathf.Sign(screenDelta) == Mathf.Sign(recoilScreenOffset))
            return screenDelta;

        float deltaMagnitude = Mathf.Abs(screenDelta);
        float recoilMagnitude = Mathf.Abs(recoilScreenOffset);
        float consumedMagnitude = Mathf.Min(deltaMagnitude, recoilMagnitude);
        float consumedDelta = consumedMagnitude * Mathf.Sign(screenDelta);

        recoilScreenOffset += consumedDelta;
        recoilOffsetAxis = Mathf.Approximately(recoilScreenOffset, 0f)
            ? 0f
            : recoilScreenOffset / recoilScreenMultiplier;

        if (deltaMagnitude <= consumedMagnitude)
            return 0f;

        return (deltaMagnitude - consumedMagnitude) * Mathf.Sign(screenDelta);
    }

    private float GetEffectiveRecoilMultiplier(WeaponDefinition weapon, bool isAiming)
    {
        if (!isAiming)
            return 1f;

        if (weapon.aimRecoilMultiplier <= 0f)
            return 1f;

        return weapon.aimRecoilMultiplier;
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

        spreadAngle *= GetStatMultiplier(StatType.SpreadControlMultiplier);

        if (weapon.maxSpreadAngle > 0f)
        {
            spreadAngle = Mathf.Min(spreadAngle, weapon.maxSpreadAngle);
        }

        return Mathf.Max(0f, spreadAngle);
    }

    private float GetStatMultiplier(StatType statType)
    {
        return characterStats != null ? Mathf.Max(0f, characterStats.GetValue(statType, 1f)) : 1f;
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
