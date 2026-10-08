using System;

// 조준을 시작한 실제 무기와 준비 완료 시각만 관리한다. 입력과 Unity 프레임은 알지 않는다.
public class WeaponAdsTiming
{
    private WeaponInstance aimingWeapon;
    private float readyTime;
    public bool Evaluate(WeaponInstance weapon, bool requested, float now)
    {
        if (!requested || weapon == null)
        {
            aimingWeapon = null;
            readyTime = 0f;
            return false;
        }
        // 같은 총을 계속 조준할 때는 시간을 다시 시작하지 않는다.
        if (!ReferenceEquals(aimingWeapon, weapon))
        {
            aimingWeapon = weapon;
            readyTime = now + Math.Max(0f, weapon.Definition.adsDuration);
        }
        return now >= readyTime;
    }
}
