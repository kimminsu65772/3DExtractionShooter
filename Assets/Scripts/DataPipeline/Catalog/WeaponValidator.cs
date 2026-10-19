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

            ValidateNonNegativeFloat(row.unitWeight, nameof(row.unitWeight), record, report);
            if (row.rarity < 0) AddIssue(record, nameof(row.rarity), row.rarity.ToString(), "레어도는 0 이상의 정수여야 한다.", report);
            if (row.maxStackCount != 1) AddIssue(record, nameof(row.maxStackCount), row.maxStackCount.ToString(), "무기는 한 칸에 한 자루만 보관한다.", report);
            ValidatePositiveFloat(row.movementSpeedMultiplier, nameof(row.movementSpeedMultiplier), record, report);
            ValidatePositiveFloat(row.adsMovementSpeedMultiplier, nameof(row.adsMovementSpeedMultiplier), record, report);
            ValidateNonNegativeFloat(row.adsDuration, nameof(row.adsDuration), record, report);
            if (!Enum.IsDefined(typeof(WeaponReloadMode), row.reloadMode)) AddIssue(record, nameof(row.reloadMode), row.reloadMode.ToString(), "알 수 없는 장전 방식이다.", report);

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
            ValidatePositiveInt(row.bulletsPerShot, nameof(row.bulletsPerShot), record, report);
            if (row.bulletsPerShot > 128) AddIssue(record, nameof(row.bulletsPerShot), row.bulletsPerShot.ToString(), "한 번에 발사할 탄환 수는 128을 초과할 수 없다.", report);
            ValidateNonNegativeFloat(row.singleShotAngle, nameof(row.singleShotAngle), record, report);
            if (row.singleShotAngle > 89f) AddIssue(record, nameof(row.singleShotAngle), row.singleShotAngle.ToString(), "중심에서 벗어나는 각도는 89도 이하여야 한다.", report);
            // 이전 CSV의 생략은 네 값이 모두 -1인 경우에만 허용한다. 일부만 정의된 ADS는 오류로 보고한다.
            bool hasAdsSettings = row.adsBaseSpreadAngle != -1f || row.adsMaxSpreadAngle != -1f || row.adsSpreadIncreasePerShot != -1f || row.adsSpreadRecoverySpeed != -1f;
            if (hasAdsSettings)
            {
                ValidateNonNegativeFloat(row.adsBaseSpreadAngle, nameof(row.adsBaseSpreadAngle), record, report);
                ValidateNonNegativeFloat(row.adsMaxSpreadAngle, nameof(row.adsMaxSpreadAngle), record, report);
                ValidateNonNegativeFloat(row.adsSpreadIncreasePerShot, nameof(row.adsSpreadIncreasePerShot), record, report);
                ValidateNonNegativeFloat(row.adsSpreadRecoverySpeed, nameof(row.adsSpreadRecoverySpeed), record, report);
                if (row.adsMaxSpreadAngle < row.adsBaseSpreadAngle) AddIssue(record, nameof(row.adsMaxSpreadAngle), row.adsMaxSpreadAngle.ToString(), "조준 최대 탄퍼짐은 기본 탄퍼짐 이상이어야 한다.", report);
            }

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

            // 적용 시간이 0이면 즉시 적용하고, 양수이면 시간에 나누어 적용한다.
            ValidateNonNegativeFloat(row.recoilDuration, nameof(row.recoilDuration), record, report);
            // 회복 지연은 마지막 실제 발사 이후의 시간이므로 음수일 수 없다.
            ValidateNonNegativeFloat(row.recoilRecoveryDelay, nameof(row.recoilRecoveryDelay), record, report);
            // 수직 반동 범위는 조준점이 앞으로 밀리는 배율을 나타낸다.
            ValidateNonNegativeFloat(row.recoilVerticalMin, nameof(row.recoilVerticalMin), record, report);
            // 수직 상한도 유한한 양수 또는 0이어야 한다.
            ValidateNonNegativeFloat(row.recoilVerticalMax, nameof(row.recoilVerticalMax), record, report);
            // 뒤집힌 무작위 범위를 CSV를 읽는 단계에서 거절한다.
            if (row.recoilVerticalMin > row.recoilVerticalMax)
                // 문제가 있는 열과 원래 값을 데이터 보고서에 남긴다.
                AddIssue(record, nameof(row.recoilVerticalMax), row.recoilVerticalMax.ToString(CultureInfo.InvariantCulture), "수직 반동 상한은 하한 이상이어야 한다.", report);
            // 수평 반동은 음수가 가능하지만 NaN, 무한대, 뒤집힌 범위는 허용하지 않는다.
            if (!IsFinite(row.recoilHorizontalMin) || !IsFinite(row.recoilHorizontalMax) || row.recoilHorizontalMin > row.recoilHorizontalMax)
                // 두 경계값을 함께 보여 주어 잘못된 방향 범위를 찾기 쉽게 한다.
                AddIssue(record, "recoilHorizontalRange", $"{row.recoilHorizontalMin}, {row.recoilHorizontalMax}", "수평 반동 범위는 유한하며 하한이 상한 이하여야 한다.", report);

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
