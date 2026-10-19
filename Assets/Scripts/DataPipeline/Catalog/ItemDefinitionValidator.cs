using System;

namespace ExtractionShooter.DataPipeline
{
    // 공통 정보와 종류별 필수 수치를 검증한다.
    public class ItemDefinitionValidator<T> : IRowValidator<T> where T : ItemDefinition
    {
        public void Validate(T row, int record, DataReport report)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (report == null) throw new ArgumentNullException(nameof(report));
            Check(row.itemId > 0, "itemId", row.itemId, record, report);
            Check(!string.IsNullOrWhiteSpace(row.displayName), "displayName", row.displayName, record, report);
            Check(Positive(row.unitWeight), "unitWeight", row.unitWeight, record, report);
            Check(row.maxStackCount > 0, "maxStackCount", row.maxStackCount, record, report);
            if (row is MedicineDefinition medicine)
            {
                Check(Positive(medicine.healAmount), "healAmount", medicine.healAmount, record, report);
                Check(Positive(medicine.useDuration), "useDuration", medicine.useDuration, record, report);
                if (medicine.useCostRatio.HasValue)
                {
                    Check(Positive(medicine.useCostRatio.Value) && medicine.useCostRatio.Value <= 1f, "useCostRatio", medicine.useCostRatio, record, report);
                    // 잔여량이 다른 키트는 한 칸에 하나씩 보관한다.
                    Check(medicine.maxStackCount == 1, "maxStackCount", medicine.maxStackCount, record, report);
                }
            }
            if (row is ProtectionDefinition protection)
            {
                Check(protection.rarity >= 0, "rarity", protection.rarity, record, report);
                Check(NonNegative(protection.armorLevel), "armorLevel", protection.armorLevel, record, report);
                Check(Positive(protection.maxDurability), "maxDurability", protection.maxDurability, record, report);
                Check(Positive(protection.durabilityDamageMultiplier), "durabilityDamageMultiplier", protection.durabilityDamageMultiplier, record, report);
                Check(protection.maxStackCount == 1, "maxStackCount", protection.maxStackCount, record, report);
            }
            if (row is ArmorDefinition armor)
            {
                Check(NonNegative(armor.armorLevel), "armorLevel", armor.armorLevel, record, report);
                Check(armor.backpackCapacityBonus >= 0, "backpackCapacityBonus", armor.backpackCapacityBonus, record, report);
                Check(armor.maxStackCount == 1, "maxStackCount", armor.maxStackCount, record, report);
            }
            if (row is HelmetDefinition helmet)
            {
                Check(NonNegative(helmet.armorLevel), "armorLevel", helmet.armorLevel, record, report);
                Check(helmet.maxStackCount == 1, "maxStackCount", helmet.maxStackCount, record, report);
            }
        }

        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool NonNegative(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Check(bool valid, string column, object raw, int record, DataReport report)
        {
            if (!valid) report.Add(new DataIssue { Record = record, Column = column, RawValue = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), Message = "아이템 정의 값이 허용 범위를 벗어나거나 비어 있다." });
        }
    }
}
