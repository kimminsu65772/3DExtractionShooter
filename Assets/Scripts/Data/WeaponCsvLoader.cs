using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class WeaponCsvLoader
{
    private const string DefaultCsvRelativePath = "Data/weapons.csv";

    // 필드명을 미리 정의하여 오타를 방지한다.
    private static class Columns
    {
        public const string WeaponId = "weaponId";
        public const string DisplayName = "displayName";
        public const string WeaponType = "weaponType";
        public const string FireRate = "fireRate";

        public const string VerticalRecoilPerShot = "verticalRecoilPerShot";
        public const string HorizontalRecoilPerShot = "horizontalRecoilPerShot";
        public const string MaxRecoilOffset = "maxRecoilOffset";
        public const string RecoilRecoverySpeed = "recoilRecoverySpeed";
        public const string AimRecoilMultiplier = "aimRecoilMultiplier";

        public const string BaseSpreadAngle = "baseSpreadAngle";
        public const string MaxSpreadAngle = "maxSpreadAngle";
        public const string SpreadIncreasePerShot = "spreadIncreasePerShot";
        public const string SpreadRecoverySpeed = "spreadRecoverySpeed";
        public const string AimSpreadMultiplier = "aimSpreadMultiplier";
        public const string MoveSpreadMultiplier = "moveSpreadMultiplier";

        public const string BaseDamage = "baseDamage";
        public const string HeadMultiplier = "headMultiplier";
        public const string BodyMultiplier = "bodyMultiplier";

        public const string OptimalRange = "optimalRange";
        public const string EffectiveRange = "effectiveRange";
        public const string MinDamageRange = "minDamageRange";
        public const string OptimalRangeMultiplier = "optimalRangeMultiplier";
        public const string EffectiveRangeMultiplier = "effectiveRangeMultiplier";
        public const string MinDamageMultiplier = "minDamageMultiplier";

        public const string ProjectileSpeed = "projectileSpeed";
        public const string ProjectileRadius = "projectileRadius";
        public const string ProjectileAddress = "projectileAddress";
        public const string ProjectilePrewarmSize = "projectilePrewarmSize";

        public const string MuzzleVfxAddress = "muzzleVfxAddress";
        public const string HitVfxAddress = "hitVfxAddress";
        public const string FireSoundAddress = "fireSoundAddress";

        // 무기 데이터를 구성하는 필수 컬럼 목록
        public static readonly string[] Required =
        {
            WeaponId,
            DisplayName,
            WeaponType,
            FireRate,
            VerticalRecoilPerShot,
            HorizontalRecoilPerShot,
            MaxRecoilOffset,
            RecoilRecoverySpeed,
            AimRecoilMultiplier,
            BaseSpreadAngle,
            MaxSpreadAngle,
            SpreadIncreasePerShot,
            SpreadRecoverySpeed,
            AimSpreadMultiplier,
            MoveSpreadMultiplier,
            BaseDamage,
            HeadMultiplier,
            BodyMultiplier,
            OptimalRange,
            EffectiveRange,
            MinDamageRange,
            OptimalRangeMultiplier,
            EffectiveRangeMultiplier,
            MinDamageMultiplier,
            ProjectileSpeed,
            ProjectileRadius,
            ProjectileAddress,
            ProjectilePrewarmSize,
            MuzzleVfxAddress,
            HitVfxAddress,
            FireSoundAddress
        };
    }

    public sealed class CsvTable
    {
        public IReadOnlyDictionary<string, int> HeaderMap { get; }
        public IReadOnlyList<string[]> Rows { get; }
        public IReadOnlyList<int> LineNumbers { get; }

        public CsvTable(
            IReadOnlyDictionary<string, int> headerMap,
            IReadOnlyList<string[]> rows,
            IReadOnlyList<int> lineNumbers)
        {
            HeaderMap = headerMap;
            Rows = rows;
            LineNumbers = lineNumbers;
        }
    }

    public string LoadRawCsv()
    {
        string csvPath = Path.Combine(Application.streamingAssetsPath, DefaultCsvRelativePath);

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"Weapon CSV 파일을 찾을 수 없습니다: {csvPath}");
            return string.Empty;
        }

        return File.ReadAllText(csvPath);
    }

    public List<string> LoadDataRows()
    {
        CsvTable table = LoadTable();
        List<string> dataRows = new List<string>(table.Rows.Count);

        foreach (string[] row in table.Rows)
        {
            dataRows.Add(string.Join(",", row));
        }

        return dataRows;
    }

    public CsvTable LoadTable()
    {
        string csvText = LoadRawCsv();

        if (string.IsNullOrWhiteSpace(csvText))
        {
            return new CsvTable(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new List<string[]>(),
                new List<int>()
            );
        }

        string[] lines = csvText.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.None
        );

        Dictionary<string, int> headerMap = null;
        List<string[]> rows = new List<string[]>();
        List<int> lineNumbers = new List<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int lineNumber = i + 1;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] columns = SplitRow(line);

            if (headerMap == null)
            {
                headerMap = BuildHeaderMap(columns);
                ValidateRequiredHeaders(headerMap);
                continue;
            }

            rows.Add(columns);
            lineNumbers.Add(lineNumber);
        }

        if (headerMap == null)
        {
            Debug.LogError("CSV Header를 찾지 못했습니다.");
            headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        return new CsvTable(headerMap, rows, lineNumbers);
    }

    public Dictionary<int, WeaponDefinition> LoadWeapons()
    {
        CsvTable table = LoadTable();
        Dictionary<int, WeaponDefinition> weapons = new Dictionary<int, WeaponDefinition>();

        for (int i = 0; i < table.Rows.Count; i++)
        {
            string[] row = table.Rows[i];
            int lineNumber = table.LineNumbers[i];

            if (!TryParseWeaponRow(row, table.HeaderMap, lineNumber, out WeaponDefinition weapon))
                continue;

            if (weapons.ContainsKey(weapon.weaponId))
            {
                Debug.LogError($"{lineNumber}번째 줄 weaponId 중복: {weapon.weaponId}");
                continue;
            }

            weapons.Add(weapon.weaponId, weapon);
        }

        return weapons;
    }

    public string[] SplitRow(string row)
    {
        if (string.IsNullOrWhiteSpace(row))
            return Array.Empty<string>();

        string[] columns = row.Split(',');

        for (int i = 0; i < columns.Length; i++)
        {
            columns[i] = columns[i].Trim();
        }

        return columns;
    }

    public bool TryParseWeaponRow(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        int lineNumber,
        out WeaponDefinition weapon)
    {
        weapon = null;

        if (!TryGetInt(columns, headerMap, Columns.WeaponId, lineNumber, out int weaponId))
            return false;

        if (!TryGetString(columns, headerMap, Columns.DisplayName, lineNumber, out string displayName))
            return false;

        if (!TryGetEnum(columns, headerMap, Columns.WeaponType, lineNumber, out WeaponType weaponType))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.FireRate, lineNumber, out float fireRate))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.VerticalRecoilPerShot, lineNumber, out float verticalRecoilPerShot))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.HorizontalRecoilPerShot, lineNumber, out float horizontalRecoilPerShot))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.MaxRecoilOffset, lineNumber, out float maxRecoilOffset))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.RecoilRecoverySpeed, lineNumber, out float recoilRecoverySpeed))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.AimRecoilMultiplier, lineNumber, out float aimRecoilMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.BaseSpreadAngle, lineNumber, out float baseSpreadAngle))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.MaxSpreadAngle, lineNumber, out float maxSpreadAngle))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.SpreadIncreasePerShot, lineNumber, out float spreadIncreasePerShot))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.SpreadRecoverySpeed, lineNumber, out float spreadRecoverySpeed))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.AimSpreadMultiplier, lineNumber, out float aimSpreadMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.MoveSpreadMultiplier, lineNumber, out float moveSpreadMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.BaseDamage, lineNumber, out float baseDamage))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.HeadMultiplier, lineNumber, out float headMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.BodyMultiplier, lineNumber, out float bodyMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.OptimalRange, lineNumber, out float optimalRange))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.EffectiveRange, lineNumber, out float effectiveRange))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.MinDamageRange, lineNumber, out float minDamageRange))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.OptimalRangeMultiplier, lineNumber, out float optimalRangeMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.EffectiveRangeMultiplier, lineNumber, out float effectiveRangeMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.MinDamageMultiplier, lineNumber, out float minDamageMultiplier))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.ProjectileSpeed, lineNumber, out float projectileSpeed))
            return false;

        if (!TryGetFloat(columns, headerMap, Columns.ProjectileRadius, lineNumber, out float projectileRadius))
            return false;

        if (!TryGetString(columns, headerMap, Columns.ProjectileAddress, lineNumber, out string projectileAddress))
            return false;

        if (!TryGetInt(columns, headerMap, Columns.ProjectilePrewarmSize, lineNumber, out int projectilePrewarmSize))
            return false;

        if (!TryGetString(columns, headerMap, Columns.MuzzleVfxAddress, lineNumber, out string muzzleVfxAddress))
            return false;

        if (!TryGetString(columns, headerMap, Columns.HitVfxAddress, lineNumber, out string hitVfxAddress))
            return false;

        if (!TryGetString(columns, headerMap, Columns.FireSoundAddress, lineNumber, out string fireSoundAddress))
            return false;

        weapon = new WeaponDefinition
        {
            weaponId = weaponId,
            displayName = displayName,
            weaponType = weaponType,
            fireRate = fireRate,

            verticalRecoilPerShot = verticalRecoilPerShot,
            horizontalRecoilPerShot = horizontalRecoilPerShot,
            maxRecoilOffset = maxRecoilOffset,
            recoilRecoverySpeed = recoilRecoverySpeed,
            aimRecoilMultiplier = aimRecoilMultiplier,

            baseSpreadAngle = baseSpreadAngle,
            maxSpreadAngle = maxSpreadAngle,
            spreadIncreasePerShot = spreadIncreasePerShot,
            spreadRecoverySpeed = spreadRecoverySpeed,
            aimSpreadMultiplier = aimSpreadMultiplier,
            moveSpreadMultiplier = moveSpreadMultiplier,

            baseDamage = baseDamage,
            headMultiplier = headMultiplier,
            bodyMultiplier = bodyMultiplier,

            optimalRange = optimalRange,
            effectiveRange = effectiveRange,
            minDamageRange = minDamageRange,
            optimalRangeMultiplier = optimalRangeMultiplier,
            effectiveRangeMultiplier = effectiveRangeMultiplier,
            minDamageMultiplier = minDamageMultiplier,

            projectileSpeed = projectileSpeed,
            projectileRadius = projectileRadius,
            projectileAddress = projectileAddress,
            projectilePrewarmSize = projectilePrewarmSize,

            muzzleVfxAddress = muzzleVfxAddress,
            hitVfxAddress = hitVfxAddress,
            fireSoundAddress = fireSoundAddress
        };

        return true;
    }

    // CSV Header를 Key, 컬럼 Index를 Value로 하는 Dictionary를 생성한다.
    // 이를 통해 columns 순서에 상관없이 필드명을 기반으로 데이터를 가져올 수 있도록 하낟.
    private Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        Dictionary<string, int> headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim();

            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (headerMap.ContainsKey(header))
            {
                Debug.LogError($"CSV Header 중복: {header}");
                continue;
            }

            headerMap.Add(header, i);
        }

        return headerMap;
    }

    // CSV 필수 Header가 모두 존재하는지 검증한다.
    private void ValidateRequiredHeaders(IReadOnlyDictionary<string, int> headerMap)
    {
        foreach (string requiredHeader in Columns.Required)
        {
            if (!headerMap.ContainsKey(requiredHeader))
            {
                Debug.LogError($"CSV 필수 Header 누락: {requiredHeader}");
            }
        }
    }

    // 타입에 맞게 CSV 데이터를 파싱하는 메서드들.
    private bool TryGetString(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        string fieldName,
        int lineNumber,
        out string value)
    {
        value = string.Empty;

        if (!TryGetRawValue(columns, headerMap, fieldName, lineNumber, out string rawValue))
            return false;

        value = rawValue;
        return true;
    }

    private bool TryGetInt(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        string fieldName,
        int lineNumber,
        out int result)
    {
        result = 0;

        if (!TryGetRawValue(columns, headerMap, fieldName, lineNumber, out string value))
            return false;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            Debug.LogError($"{lineNumber}번째 줄 {fieldName} 파싱 실패: {value}");
            return false;
        }

        return true;
    }

    private bool TryGetFloat(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        string fieldName,
        int lineNumber,
        out float result)
    {
        result = 0f;

        if (!TryGetRawValue(columns, headerMap, fieldName, lineNumber, out string value))
            return false;

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            Debug.LogError($"{lineNumber}번째 줄 {fieldName} 파싱 실패: {value}");
            return false;
        }

        return true;
    }

    private bool TryGetEnum<TEnum>(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        string fieldName,
        int lineNumber,
        out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;

        if (!TryGetRawValue(columns, headerMap, fieldName, lineNumber, out string value))
            return false;

        if (!Enum.TryParse(value, true, out result))
        {
            Debug.LogError($"{lineNumber}번째 줄 {fieldName} 파싱 실패: {value}");
            return false;
        }

        return true;
    }

    private bool TryGetRawValue(
        string[] columns,
        IReadOnlyDictionary<string, int> headerMap,
        string fieldName,
        int lineNumber,
        out string value)
    {
        value = string.Empty;

        if (!headerMap.TryGetValue(fieldName, out int columnIndex))
        {
            Debug.LogError($"CSV Header를 찾을 수 없습니다: {fieldName}");
            return false;
        }

        if (columnIndex < 0 || columnIndex >= columns.Length)
        {
            Debug.LogError($"{lineNumber}번째 줄 {fieldName} 컬럼이 비어 있습니다. Index: {columnIndex}, ColumnCount: {columns.Length}");
            return false;
        }

        value = columns[columnIndex].Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            Debug.LogError($"{lineNumber}번째 줄 {fieldName} 값이 비어 있습니다.");
            return false;
        }

        return true;
    }
}
