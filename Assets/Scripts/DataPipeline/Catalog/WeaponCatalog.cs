using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class WeaponCatalog
    {

        private Dictionary<int, RuntimeWeapon> weapons = new();

        public int Count => weapons.Count;

        // 런타임에서 사용할 유효한 무기 목록을 생성하거나 갱신한다.
        public void Publish(IReadOnlyList<RuntimeWeapon> candidates, DataReport report)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            if (report == null) throw new ArgumentNullException(nameof(report));

            if (report.HasErrors)
                return;

            Dictionary<int, RuntimeWeapon> nextWeapons = new(candidates.Count);

            foreach (RuntimeWeapon candidate in candidates)
            {
                if (candidate == null)
                {
                    report.Add(new DataIssue
                    {
                        Record = 0,
                        Column = string.Empty,
                        RawValue = string.Empty,
                        Message = "RuntimeWeapon 후보가 null입니다."
                    });

                    continue;
                }

                int weaponId = candidate.WeaponId;

                if (weaponId <= 0)
                {
                    report.Add(new DataIssue
                    {
                        Record = 0,
                        Column = nameof(WeaponDefinition.weaponId),
                        RawValue = weaponId.ToString(
                            CultureInfo.InvariantCulture),
                        Message = "무기 ID는 0보다 커야 합니다."
                    });

                    continue;
                }

                if (!nextWeapons.TryAdd(weaponId, candidate))
                {
                    report.Add(new DataIssue
                    {
                        Record = 0,
                        Column = nameof(WeaponDefinition.weaponId),
                        RawValue = weaponId.ToString(
                            CultureInfo.InvariantCulture),
                        Message = "무기 ID가 중복되었습니다."
                    });
                    continue;
                }
            }

            if (report.HasErrors)
                return;

            weapons = nextWeapons;
        }
        public bool TryGet(int weaponId, out RuntimeWeapon weapon)
        {
            return weapons.TryGetValue(weaponId, out weapon);
        }

        public void Clear()
        {
            weapons.Clear();
        }
    }
}
