using System;
using UnityEngine;
using ExtractionShooter.DataPipeline;

public class CsvValueConverterTest : MonoBehaviour
{
    private enum TestWeaponType
    {
        Rifle = 1,
        Pistol = 2
    }

    private void Start()
    {
        RunTests();
    }

    [ContextMenu("Run Converter Tests")]
    private void RunTests()
    {
        Check("정수 변환", "123", typeof(int), true, 123);
        Check("잘못된 정수", "abc", typeof(int), false);
        Check("빈 정수", "", typeof(int), false);
        Check("정수 범위 초과", "2147483648", typeof(int), false);

        Check("실수 변환", "1.5", typeof(float), true, 1.5f);
        Check("쉼표 소수점 거부", "1,5", typeof(float), false);
        Check("NaN 거부", "NaN", typeof(float), false);
        Check("무한대 거부", "Infinity", typeof(float), false);

        Check("문자열 유지", "AK47", typeof(string), true, "AK47");

        Check("enum 이름 변환", "Rifle",
            typeof(TestWeaponType), true, TestWeaponType.Rifle);

        Check("enum 대소문자 무시", "rifle",
            typeof(TestWeaponType), true, TestWeaponType.Rifle);

        Check("없는 enum 이름", "Shotgun",
            typeof(TestWeaponType), false);

        Check("없는 enum 숫자", "999",
            typeof(TestWeaponType), false);

        // 현재 정책에서는 선언된 값에 해당하는 숫자 문자열도 허용합니다.
        Check("선언된 enum 숫자", "1",
            typeof(TestWeaponType), true, TestWeaponType.Rifle);

        Check("지원하지 않는 타입", "true", typeof(bool), false);

        // TryConvert와 별도로 제네릭 메서드도 확인합니다.
        bool enumSuccess = CsvValueConverter.TryEnum<TestWeaponType>(
            "pistol", out TestWeaponType enumValue);

        LogResult(
            "제네릭 enum 변환",
            enumSuccess && enumValue == TestWeaponType.Pistol);
    }

    private void Check(
        string testName,
        string raw,
        Type targetType,
        bool expectedSuccess,
        object expectedValue = null)
    {
        bool success = CsvValueConverter.TryConvert(
            raw, targetType, out object actualValue);

        bool passed =
            success == expectedSuccess &&
            (!expectedSuccess || object.Equals(actualValue, expectedValue));

        LogResult(testName, passed);
    }

    private void LogResult(string testName, bool passed)
    {
        if (passed)
            Debug.Log($"[PASS] {testName}", this);
        else
            Debug.LogError($"[FAIL] {testName}", this);
    }
}