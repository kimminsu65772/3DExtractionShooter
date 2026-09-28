using System;
using System.Globalization;

namespace ExtractionShooter.DataPipeline
{
    public class WeaponValidator : IRowValidator<WeaponDefinition>
    {
        public void Validate(
            WeaponDefinition row,
            int record,
            DataReport report)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            ValidatePositiveInt(
                row.weaponId,
                nameof(row.weaponId),
                record,
                report);

            ValidateRequiredString(
                row.displayName,
                nameof(row.displayName),
                record,
                report);

            if (row.weaponType == WeaponType.None)
            {
                AddIssue(
                    record,
                    nameof(row.weaponType),
                    row.weaponType.ToString(),
                    "무기 종류가 지정되지 않았습니다.",
                    report);
            }

            ValidatePositiveFloat(
                row.fireRate,
                nameof(row.fireRate),
                record,
                report);

            ValidatePositiveFloat(
                row.baseDamage,
                nameof(row.baseDamage),
                record,
                report);

            ValidatePositiveFloat(
                row.headMultiplier,
                nameof(row.headMultiplier),
                record,
                report);

            ValidatePositiveFloat(
                row.bodyMultiplier,
                nameof(row.bodyMultiplier),
                record,
                report);

            if (row.weaponType == WeaponType.Ranged)
            {
                ValidateRangedWeapon(row, record, report);
            }
        }

        private static void ValidateRangedWeapon(
            WeaponDefinition row,
            int record,
            DataReport report)
        {
            if (row.ammoType == AmmoType.None)
            {
                AddIssue(record, nameof(row.ammoType), row.ammoType.ToString(), "원거리 무기에 사용할 탄약 종류가 지정되지 않았습니다.", report);
            }

            ValidatePositiveInt(row.magazineCapacity, nameof(row.magazineCapacity), record, report);
            ValidatePositiveFloat(row.reloadDuration, nameof(row.reloadDuration), record, report);

            ValidateNonNegativeFloat(
                row.verticalRecoilPerShot,
                nameof(row.verticalRecoilPerShot),
                record,
                report);

            ValidateNonNegativeFloat(
                row.horizontalRecoilPerShot,
                nameof(row.horizontalRecoilPerShot),
                record,
                report);

            ValidateNonNegativeFloat(
                row.maxRecoilOffset,
                nameof(row.maxRecoilOffset),
                record,
                report);

            ValidateNonNegativeFloat(
                row.recoilRecoverySpeed,
                nameof(row.recoilRecoverySpeed),
                record,
                report);

            ValidatePositiveFloat(
                row.aimRecoilMultiplier,
                nameof(row.aimRecoilMultiplier),
                record,
                report);

            ValidateNonNegativeFloat(
                row.baseSpreadAngle,
                nameof(row.baseSpreadAngle),
                record,
                report);

            ValidateNonNegativeFloat(
                row.maxSpreadAngle,
                nameof(row.maxSpreadAngle),
                record,
                report);

            ValidateNonNegativeFloat(
                row.spreadIncreasePerShot,
                nameof(row.spreadIncreasePerShot),
                record,
                report);

            ValidateNonNegativeFloat(
                row.spreadRecoverySpeed,
                nameof(row.spreadRecoverySpeed),
                record,
                report);

            ValidatePositiveFloat(
                row.aimSpreadMultiplier,
                nameof(row.aimSpreadMultiplier),
                record,
                report);

            ValidatePositiveFloat(
                row.moveSpreadMultiplier,
                nameof(row.moveSpreadMultiplier),
                record,
                report);

            ValidatePositiveFloat(
                row.projectileSpeed,
                nameof(row.projectileSpeed),
                record,
                report);

            ValidatePositiveFloat(
                row.projectileRadius,
                nameof(row.projectileRadius),
                record,
                report);

            ValidateNonNegativeInt(
                row.projectilePrewarmSize,
                nameof(row.projectilePrewarmSize),
                record,
                report);

            ValidateRequiredString(
                row.projectileAddress,
                nameof(row.projectileAddress),
                record,
                report);

            ValidateRangeOrder(row, record, report);
            ValidateRangeMultipliers(row, record, report);
            ValidateSpreadOrder(row, record, report);
        }

        private static void ValidateRangeOrder(
            WeaponDefinition row,
            int record,
            DataReport report)
        {
            bool isValid =
                IsFinite(row.optimalRange) &&
                IsFinite(row.effectiveRange) &&
                IsFinite(row.minDamageRange) &&
                0f < row.optimalRange &&
                row.optimalRange <= row.effectiveRange &&
                row.effectiveRange <= row.minDamageRange;

            if (isValid)
                return;

            AddIssue(
                record,
                "range",
                $"{row.optimalRange}, {row.effectiveRange}, {row.minDamageRange}",
                "사거리는 0 < optimalRange <= effectiveRange <= minDamageRange 순서여야 합니다.",
                report);
        }

        private static void ValidateRangeMultipliers(
            WeaponDefinition row,
            int record,
            DataReport report)
        {
            bool isValid =
                IsFinite(row.optimalRangeMultiplier) &&
                IsFinite(row.effectiveRangeMultiplier) &&
                IsFinite(row.minDamageMultiplier) &&
                row.optimalRangeMultiplier >= 0f &&
                row.effectiveRangeMultiplier >= 0f &&
                row.minDamageMultiplier >= 0f &&
                row.optimalRangeMultiplier >= row.effectiveRangeMultiplier &&
                row.effectiveRangeMultiplier >= row.minDamageMultiplier;

            if (isValid)
                return;

            AddIssue(
                record,
                "rangeMultiplier",
                $"{row.optimalRangeMultiplier}, " +
                $"{row.effectiveRangeMultiplier}, " +
                $"{row.minDamageMultiplier}",
                "사거리 배율은 음수가 아니며 가까운 구간부터 같거나 감소해야 합니다.",
                report);
        }

        private static void ValidateSpreadOrder(
            WeaponDefinition row,
            int record,
            DataReport report)
        {
            if (!IsFinite(row.baseSpreadAngle) ||
                !IsFinite(row.maxSpreadAngle))
            {
                return;
            }

            if (row.maxSpreadAngle >= row.baseSpreadAngle)
                return;

            AddIssue(
                record,
                nameof(row.maxSpreadAngle),
                row.maxSpreadAngle.ToString(CultureInfo.InvariantCulture),
                "최대 탄퍼짐 각도는 기본 탄퍼짐 각도보다 작을 수 없습니다.",
                report);
        }

        private static void ValidatePositiveInt(
            int value,
            string column,
            int record,
            DataReport report)
        {
            if (value > 0)
                return;

            AddIssue(
                record,
                column,
                value.ToString(CultureInfo.InvariantCulture),
                "값은 0보다 커야 합니다.",
                report);
        }

        private static void ValidateNonNegativeInt(
            int value,
            string column,
            int record,
            DataReport report)
        {
            if (value >= 0)
                return;

            AddIssue(
                record,
                column,
                value.ToString(CultureInfo.InvariantCulture),
                "값은 음수일 수 없습니다.",
                report);
        }

        private static void ValidatePositiveFloat(
            float value,
            string column,
            int record,
            DataReport report)
        {
            if (IsFinite(value) && value > 0f)
                return;

            AddIssue(
                record,
                column,
                value.ToString(CultureInfo.InvariantCulture),
                "값은 유한한 수이며 0보다 커야 합니다.",
                report);
        }

        private static void ValidateNonNegativeFloat(
            float value,
            string column,
            int record,
            DataReport report)
        {
            if (IsFinite(value) && value >= 0f)
                return;

            AddIssue(
                record,
                column,
                value.ToString(CultureInfo.InvariantCulture),
                "값은 유한한 수이며 음수일 수 없습니다.",
                report);
        }

        private static void ValidateRequiredString(
            string value,
            string column,
            int record,
            DataReport report)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            AddIssue(
                record,
                column,
                value ?? string.Empty,
                "필수 문자열 값이 비어 있습니다.",
                report);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static void AddIssue(
            int record,
            string column,
            string rawValue,
            string message,
            DataReport report)
        {
            report.Add(new DataIssue
            {
                Record = record,
                Column = column,
                RawValue = rawValue,
                Message = message
            });
        }
    }
}
