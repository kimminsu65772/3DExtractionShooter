using System.Collections.Generic;
using UnityEngine;

public class CsvLoaderTest : MonoBehaviour
{
    private void Start()
    {
        WeaponCsvLoader loader = new WeaponCsvLoader();
        Dictionary<int, WeaponDefinition> weapons = loader.LoadWeapons();

        Debug.Log($"로드된 무기 개수: {weapons.Count}");

        foreach (var pair in weapons)
        {
            WeaponDefinition weapon = pair.Value;

            Debug.Log(
                $"ID: {weapon.weaponId}, 이름: {weapon.displayName}, 타입: {weapon.weaponType}, " +
                $"데미지: {weapon.baseDamage}, 연사: {weapon.fireRate}, " +
                $"사거리: {weapon.optimalRange}/{weapon.effectiveRange}/{weapon.minDamageRange}, " +
                $"투사체: {weapon.projectileAddress}, 프리웜: {weapon.projectilePrewarmSize}"
            );
        }
    }
}
