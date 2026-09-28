using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtractionShooter.DataPipeline
{
    public class AmmoCatalog
    {
        private static readonly IReadOnlyList<AmmoDefinition> EmptyAmmoList = Array.Empty<AmmoDefinition>();

        private Dictionary<int, AmmoDefinition> ammoById = new();
        // 총에 장전할 수 있는 탄약 종류별로 AmmoDefinition을 조회할 수 있도록 Dictionary를 사용한다.
        // 예를 들어 라이플에 장착하는 탄약 종류를 조화하고자 할 때, ammoByType[AmmoType.Rifle]을 통해 해당 종류의 AmmoDefinition 목록을 얻을 수 있다.
        private Dictionary<AmmoType, IReadOnlyList<AmmoDefinition>> ammoByType = new();

        public int Count => ammoById.Count;

        public void Publish(IReadOnlyList<AmmoDefinition> candidates, DataReport report)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.HasErrors) return;

            Dictionary<int, AmmoDefinition> nextById = new(candidates.Count);
            Dictionary<AmmoType, List<AmmoDefinition>> nextListsByType = new();

            foreach (AmmoDefinition candidate in candidates)
            {
                if (candidate == null)
                {
                    report.Add(new DataIssue { Record = 0, Column = string.Empty, RawValue = string.Empty, Message = "AmmoDefinition 후보가 null입니다." });
                    continue;
                }

                if (!nextById.TryAdd(candidate.ammoId, candidate))
                {
                    report.Add(new DataIssue 
                    { 
                        Record = 0, 
                        Column = nameof(AmmoDefinition.ammoId), 
                        RawValue = candidate.ammoId.ToString(CultureInfo.InvariantCulture), 
                        Message = "탄약 ID가 중복되었습니다." }
                    );
                    continue;
                }

                if (!nextListsByType.TryGetValue(candidate.ammoType, out List<AmmoDefinition> ammoList))
                {
                    ammoList = new List<AmmoDefinition>();
                    nextListsByType.Add(candidate.ammoType, ammoList);
                }

                ammoList.Add(candidate);
            }

            if (report.HasErrors) return;

            Dictionary<AmmoType, IReadOnlyList<AmmoDefinition>> nextByType = new(nextListsByType.Count);
            foreach (KeyValuePair<AmmoType, List<AmmoDefinition>> pair in nextListsByType)
                nextByType.Add(pair.Key, pair.Value.AsReadOnly());

            ammoById = nextById;
            ammoByType = nextByType;
        }

        public bool TryGet(int ammoId, out AmmoDefinition ammo) => ammoById.TryGetValue(ammoId, out ammo);
        public IReadOnlyList<AmmoDefinition> GetByType(AmmoType ammoType) => ammoByType.TryGetValue(ammoType, out IReadOnlyList<AmmoDefinition> ammoList) ? ammoList : EmptyAmmoList;

        public void Clear()
        {
            ammoById.Clear();
            ammoByType.Clear();
        }
    }
}
