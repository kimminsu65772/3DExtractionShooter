using System;
using System.Collections.Generic;

namespace ExtractionShooter.DataPipeline
{
    // 종류별 카탈로그이므로 같은 숫자 ID라도 서로 다른 종류와 충돌하지 않는다.
    public class ItemDefinitionCatalog<T> where T : ItemDefinition
    {
        private Dictionary<int, T> items = new();
        public int Count => items.Count;
        public bool TryGet(int itemId, out T definition) => items.TryGetValue(itemId, out definition);

        public void Publish(IReadOnlyList<T> candidates, DataReport report)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (report.HasErrors) return;
            var next = new Dictionary<int, T>(candidates.Count);
            foreach (T definition in candidates)
            {
                if (definition == null || definition.itemId <= 0 || !next.TryAdd(definition.itemId, definition))
                    report.Add(new DataIssue { Column = "itemId", Message = "아이템 후보가 null이거나 ID가 잘못되었거나 중복되었다." });
            }
            // 하나라도 잘못되면 기존 목록을 유지하여 미완성 데이터를 공개하지 않는다.
            if (!report.HasErrors) items = next;
        }

        public void Clear() => items.Clear();
    }
}
