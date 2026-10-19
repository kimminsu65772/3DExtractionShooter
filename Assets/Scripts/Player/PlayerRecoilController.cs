using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRecoilController : MonoBehaviour
{
    [SerializeField] private CharacterStats characterStats;

    private WeaponDefinition currentWeapon;
    private Vector2 currentRecoilOffset;
    private float currentSpreadAngle;
    // 방아쇠 입력은 기존 탄퍼짐 회복만 막고, 반동 회복은 마지막 실제 발사 시각으로 판단한다.
    private bool isSpreadRecoveryBlocked;
    private bool currentIsAiming;
    private PlayerAimController aimController;
    // 무기를 바꾸거나 빈손이 되었을 때 이전 총의 반동을 지우기 위한 참조다.
    private PlayerWeaponHolder weaponHolder;
    // 같은 종류의 총이라도 다른 물건이면 반동을 넘기지 않도록 실제 인스턴스를 기억한다.
    private WeaponInstance currentWeaponInstance;
    // 이번 발사에서 최종적으로 더할 반동 벡터를 기준 화면 픽셀 단위로 보관한다.
    private Vector2 shotRecoil;
    // 이번 발사의 반동을 펼쳐서 적용하는 시간이다.
    private float shotRecoilDuration;
    // 마지막 발사 이후 지난 시간이며, 발사가 없을 때는 Infinity로 둔다.
    private float recoilElapsed = float.PositiveInfinity;
    // Update와 발사가 같은 프레임에 실행되어도 같은 시간 구간을 두 번 계산하지 않는다.
    private float lastRecoilUpdateTime;
    // 화면 높이가 달라도 화면에서 차지하는 반동의 비율을 동일하게 유지한다.
    private float ScreenScale => Screen.height / 1440f;

    public Vector2 CurrentRecoilOffset => currentRecoilOffset;
    public float CurrentSpreadAngle => currentSpreadAngle;

    public Vector2 CurrentScreenRecoilOffset
    {
        get
        {
            // 내부 값은 기준 화면 픽셀이고, 실제 커서에 표시할 때만 해상도를 반영한다.
            return currentRecoilOffset * ScreenScale;
        }
    }

    private void Awake()
    {
        aimController = GetComponent<PlayerAimController>();
        // 매 프레임 컴포넌트를 다시 찾지 않고 실제 장착 무기만 확인한다.
        weaponHolder = GetComponent<PlayerWeaponHolder>();
        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();
        }
    }

    private void Update()
    {
        // 같은 종류의 다른 총으로 교체한 경우도 실제 인스턴스 참조로 구분한다.
        if (weaponHolder != null && (!ReferenceEquals(currentWeaponInstance, weaponHolder.CurrentWeaponInstance) || !ReferenceEquals(currentWeapon, weaponHolder.CurrentWeapon?.Definition)))
            SyncSpreadMode(weaponHolder.CurrentWeapon?.Definition, aimController != null && aimController.IsAdsReady);
        // 이번 프레임까지의 반동 적용과 회복을 한 번만 진행한다.
        AdvanceRecoilToNow();
        RecoverSpread();
    }

    private void OnEnable()
    {
        // 비활성화되어 있던 시간을 반동의 진행 시간에 더하지 않는다.
        lastRecoilUpdateTime = Time.time;
    }

    private void OnDisable()
    {
        // 재활성화할 때 이전 플레이 상태의 반동과 미적용 반동을 남기지 않는다.
        ResetRecoil();
    }

    // 스프레드가 적용된 발사 방향을 반환한다.
    // 반동은 화면 조준점 오프셋에 반영하고, 여기서는 탄퍼짐만 적용한다.
    public Vector3 GetFireDirection(Vector3 baseDirection, WeaponDefinition weapon, bool isAiming, bool isMoving)
    {
        if (weapon == null)
            return baseDirection.normalized;

        SyncSpreadMode(weapon, isAiming);

        float effectiveSpreadAngle = GetEffectiveSpreadAngle(weapon, isAiming, isMoving);

        return WeaponSpreadUtility.ApplySpread(baseDirection, effectiveSpreadAngle);
    }

    // 무기 발사 시 반동과 스프레드를 적용한다.
    public void AddRecoil(WeaponDefinition weapon, bool isAiming, Vector2 recoilDirection)
    {
        if (weapon == null)
            return;

        SyncSpreadMode(weapon, isAiming);

        // 직전 Update 이후부터 실제 발사 직전까지의 시간도 먼저 반영한다.
        AdvanceRecoilToNow();
        // 낮은 프레임률에서 다음 발사가 먼저 들어오더라도 이전 발의 남은 양을 잃지 않는다.
        if (recoilElapsed < shotRecoilDuration)
            currentRecoilOffset += shotRecoil * (1f - recoilElapsed / shotRecoilDuration);
        // 원본 강도에 기존 ADS 배율과 캐릭터 반동 제어 배율을 곱한다.
        float recoilMultiplier = GetEffectiveRecoilMultiplier(weapon, isAiming) * GetStatMultiplier(StatType.RecoilControlMultiplier);
        // 좌우 배율 범위를 그대로 사용하여 오른쪽이나 왼쪽으로 치우친 총도 표현한다.
        float horizontalRecoil = Random.Range(weapon.recoilHorizontalMin, weapon.recoilHorizontalMax) * weapon.horizontalRecoilPerShot * recoilMultiplier;
        // 수직도 고정값이 아니라 원본 범위 안에서 매 발 다시 뽑는다.
        float verticalRecoil = Random.Range(weapon.recoilVerticalMin, weapon.recoilVerticalMax) * weapon.verticalRecoilPerShot * recoilMultiplier;
        // 화면에서 캐릭터가 조준하는 방향을 반동의 앞쪽 방향으로 삼는다.
        Vector2 forward = recoilDirection.normalized;
        // 조준 방향을 시계 방향으로 90도 돌리면 화면상의 오른쪽 방향이다.
        Vector2 right = new Vector2(forward.y, -forward.x);
        // 벡터를 예약해 두고 Update에서 시간에 비례하는 일부씩 더한다.
        shotRecoil = forward * verticalRecoil + right * horizontalRecoil;
        // 연사할 때 한 발의 적용 시간이 다음 발사 시점까지 넘어가지 않게 한다.
        shotRecoilDuration = Mathf.Min(weapon.recoilDuration, 1f / weapon.fireRate);
        // 회복 지연의 기준은 입력을 누른 시점이 아니라 성공적으로 발사한 지금이다.
        recoilElapsed = 0f;
        // 적용 시간이 0인 데이터는 나눗셈 없이 즉시 적용한다.
        if (shotRecoilDuration <= 0f)
            currentRecoilOffset += shotRecoil;
        // 탄퍼짐 증가는 기존 사격 규칙을 그대로 사용한다.
        currentSpreadAngle = WeaponSpreadUtility.IncreaseSpread(currentSpreadAngle, weapon, currentIsAiming);
    }

    // 마우스 입력이 현재 반동 방향과 반대라면, 실제 조준 커서를 이동시키기 전에 반동 오프셋을 먼저 상쇄한다.
    public Vector2 ConsumeScreenRecoil(Vector2 screenDelta)
    {
        // 화면 좌표를 내부 기준 픽셀로 되돌리려면 유효한 화면 높이가 필요하다.
        if (ScreenScale <= 0f)
            return screenDelta;

        screenDelta.x = ConsumeScreenRecoilAxis(screenDelta.x, ref currentRecoilOffset.x);
        screenDelta.y = ConsumeScreenRecoilAxis(screenDelta.y, ref currentRecoilOffset.y);

        return screenDelta;
    }

    // 반동은 발사 시각을 기준으로 회복하므로 이 입력은 탄퍼짐에만 적용한다.
    public void SetSpreadRecoveryBlocked(bool isAttacking)
    {
        // 기존의 방아쇠를 누르는 동안 탄퍼짐 회복을 멈추는 동작을 유지한다.
        isSpreadRecoveryBlocked = isAttacking;
    }

    private void AdvanceRecoilToNow()
    {
        // 발사와 Update 중 무엇이 먼저 호출되어도 마지막 계산 이후 시간만 진행한다.
        float elapsed = Mathf.Max(0f, Time.time - lastRecoilUpdateTime);
        // 다음 호출이 같은 구간을 다시 처리하지 않도록 기준 시각을 갱신한다.
        lastRecoilUpdateTime = Time.time;
        // 시간 계산과 반동 계산을 분리하여 프레임 간격별 결과도 검증할 수 있게 한다.
        AdvanceRecoil(elapsed);
    }

    private void AdvanceRecoil(float deltaTime)
    {
        // 아직 발사하지 않았거나 무기가 해제된 상태에서는 계산할 반동이 없다.
        if (currentWeapon == null || float.IsPositiveInfinity(recoilElapsed))
            return;
        // 이번 프레임의 끝이 마지막 발사로부터 몇 초 뒤인지 구한다.
        float nextElapsed = recoilElapsed + deltaTime;
        // 적용 시간과 겹치는 구간만 잘라내어 한 발의 총량을 정확히 나누어 적용한다.
        float appliedTime = Mathf.Max(0f, Mathf.Min(nextElapsed, shotRecoilDuration) - Mathf.Min(recoilElapsed, shotRecoilDuration));
        // 적용 시간이 끝난 뒤에는 더 이상 이번 발의 반동을 더하지 않는다.
        if (appliedTime > 0f)
            currentRecoilOffset += shotRecoil * (appliedTime / shotRecoilDuration);
        // 회복 지연이 짧더라도 반동 적용이 끝나기 전에는 회복을 시작하지 않는다.
        float recoveryStart = Mathf.Max(shotRecoilDuration, currentWeapon.recoilRecoveryDelay);
        // 지연 경계를 넘는 프레임에서도 경계 이후 시간만큼만 회복한다.
        float recoveryTime = Mathf.Max(0f, nextElapsed - Mathf.Max(recoilElapsed, recoveryStart));
        // 남은 반동에서 회복 속도와 유효 회복 시간을 곱한 양만큼 원점으로 돌아간다.
        currentRecoilOffset = Vector2.MoveTowards(currentRecoilOffset, Vector2.zero, currentWeapon.recoilRecoverySpeed * recoveryTime);
        // 다음 프레임의 시작점으로 사용할 누적 시간을 저장한다.
        recoilElapsed = nextElapsed;
    }

    private void ResetRecoil()
    {
        // 화면에 이미 적용된 반동을 제거한다.
        currentRecoilOffset = Vector2.zero;
        // 아직 적용하지 않은 발사 반동도 함께 제거한다.
        shotRecoil = Vector2.zero;
        // 이전 발사의 시간 구간이 새 발사에 영향을 주지 않게 한다.
        shotRecoilDuration = 0f;
        // 새 발사가 들어오기 전까지 시간 진행을 중단한다.
        recoilElapsed = float.PositiveInfinity;
    }

    private void RecoverSpread()
    {
        if (currentWeapon == null)
            return;
        SyncSpreadMode(currentWeapon, aimController != null && aimController.IsAdsReady);
        if (isSpreadRecoveryBlocked) return;
        currentSpreadAngle = WeaponSpreadUtility.RecoverSpread(currentSpreadAngle, currentWeapon, Time.deltaTime, currentIsAiming);
    }

    // 조준 전환 시 누적 퍼짐을 기존 조준 배율로 환산하여 쌓인 퍼짐이 갑자기 사라지지 않게 한다.
    private void SyncSpreadMode(WeaponDefinition weapon, bool isAiming)
    {
        // 같은 정의를 공유하는 총 두 자루도 실제 장착한 물건이 바뀌면 교체로 판단한다.
        bool instanceChanged = weaponHolder != null && !ReferenceEquals(currentWeaponInstance, weaponHolder.CurrentWeaponInstance);
        // Update보다 실제 발사가 먼저 호출된 프레임에도 교체를 먼저 반영한다.
        if (instanceChanged || !ReferenceEquals(currentWeapon, weapon))
        {
            // 교체 전 총의 누적 반동과 미적용 반동을 모두 버린다.
            ResetRecoil();
            currentSpreadAngle = 0f;
            currentIsAiming = false;
        }
        // 다음 발사나 Update가 같은 교체를 다시 처리하지 않도록 실제 물건을 기록한다.
        if (weaponHolder != null)
            // 빈손이면 null을 저장하여 다시 같은 총을 들어도 새 장착으로 인식한다.
            currentWeaponInstance = weaponHolder.CurrentWeaponInstance;
        // 빈손으로 전환한 경우에는 조준 모드를 계산할 무기가 없다.
        if (weapon == null)
        {
            // 다음 장착 시 다른 총으로 인식할 수 있도록 현재 정의도 비운다.
            currentWeapon = null;
            // null인 무기의 ADS 설정을 읽지 않는다.
            return;
        }
        bool independentAds = isAiming && weapon.adsBaseSpreadAngle >= 0f;
        if (currentIsAiming != independentAds)
        {
            float ratio = Mathf.Max(0.0001f, weapon.aimSpreadMultiplier);
            currentSpreadAngle *= independentAds ? ratio : 1f / ratio;
            currentIsAiming = independentAds;
        }
        currentWeapon = weapon;
    }

    private float ConsumeScreenRecoilAxis(float screenDelta, ref float recoilOffsetAxis)
    {
        if (Mathf.Approximately(screenDelta, 0f) || Mathf.Approximately(recoilOffsetAxis, 0f))
            return screenDelta;

        // 기준 픽셀을 실제 화면 픽셀로 바꾸어 마우스 입력과 같은 단위로 비교한다.
        float recoilScreenOffset = recoilOffsetAxis * ScreenScale;

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
            // 상쇄한 화면 픽셀을 내부 기준 픽셀 단위로 되돌린다.
            : recoilScreenOffset / ScreenScale;

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
        float spreadMultiplier = 1f;

        if (isAiming && weapon.adsBaseSpreadAngle < 0f)
            spreadMultiplier *= weapon.aimSpreadMultiplier;

        if (isMoving)
            spreadMultiplier *= weapon.moveSpreadMultiplier;

        spreadMultiplier *= GetStatMultiplier(StatType.SpreadControlMultiplier);

        return WeaponSpreadUtility.GetEffectiveSpreadAngle(weapon, currentSpreadAngle, spreadMultiplier, currentIsAiming);
    }

    private float GetStatMultiplier(StatType statType)
    {
        return characterStats != null ? Mathf.Max(0f, characterStats.GetValue(statType, 1f)) : 1f;
    }

}
