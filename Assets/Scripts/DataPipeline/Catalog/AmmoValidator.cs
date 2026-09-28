using System;
using System.Globalization;

namespace ExtractionShooter.DataPipeline
{
    public class AmmoValidator : IRowValidator<AmmoDefinition>
    {
        public void Validate(AmmoDefinition row, int record, DataReport report)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (report == null) throw new ArgumentNullException(nameof(report));

            Validate(row.ammoId > 0, nameof(row.ammoId), row.ammoId.ToString(CultureInfo.InvariantCulture), "탄약 ID는 0보다 커야 합니다.", record, report);
            Validate(!string.IsNullOrWhiteSpace(row.displayName), nameof(row.displayName), row.displayName ?? string.Empty, "탄약 이름이 비어 있습니다.", record, report);
            Validate(row.ammoType != AmmoType.None, nameof(row.ammoType), row.ammoType.ToString(), "탄약 종류가 지정되지 않았습니다.", record, report);
            Validate(row.armorPenetrationLevel >= 0, nameof(row.armorPenetrationLevel), row.armorPenetrationLevel.ToString(CultureInfo.InvariantCulture), "관통 등급은 음수일 수 없습니다.", record, report);
            Validate(IsPositiveFinite(row.damageMultiplier), nameof(row.damageMultiplier), row.damageMultiplier.ToString(CultureInfo.InvariantCulture), "데미지 배율은 유한한 수이며 0보다 커야 합니다.", record, report);
            Validate(row.maxStackCount > 0, nameof(row.maxStackCount), row.maxStackCount.ToString(CultureInfo.InvariantCulture), "최대 중첩 수량은 0보다 커야 합니다.", record, report);
            Validate(IsPositiveFinite(row.unitWeight), nameof(row.unitWeight), row.unitWeight.ToString(CultureInfo.InvariantCulture), "탄약 무게는 유한한 수이며 0보다 커야 합니다.", record, report);
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Validate(bool condition, string column, string rawValue, string message, int record, DataReport report)
        {
            if (!condition)
                report.Add(new DataIssue { Record = record, Column = column, RawValue = rawValue, Message = message });
        }
    }
}
