using System;
using Cysharp.Threading.Tasks;
using ExtractionShooter.DataPipeline;

// 플레이어나 적이 실제로 소유한 무기 한 자루의 변경 가능한 상태다.
public class WeaponInstance
{
    public RuntimeWeapon RuntimeWeapon { get; private set; }
    public WeaponAmmoState AmmoState { get; private set; }
    public WeaponDefinition Definition => RuntimeWeapon.Definition;
    public int WeaponId => RuntimeWeapon.WeaponId;

    public WeaponInstance(RuntimeWeapon runtimeWeapon)
    {
        RuntimeWeapon = runtimeWeapon ?? throw new ArgumentNullException(nameof(runtimeWeapon));

        WeaponDefinition definition = runtimeWeapon.Definition;
        if (definition.weaponType != WeaponType.Ranged)
            throw new ArgumentException("현재 WeaponInstance 탄창 구조는 원거리 무기만 지원합니다.", nameof(runtimeWeapon));

        AmmoState = new WeaponAmmoState(definition.ammoType, definition.magazineCapacity);
    }
}
