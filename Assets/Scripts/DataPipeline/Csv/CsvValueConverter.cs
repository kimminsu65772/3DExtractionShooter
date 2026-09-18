using System;
using System.Globalization;

namespace ExtractionShooter.DataPipeline
{
    public static class CsvValueConverter
    {
        public static bool TryConvert(string raw, Type targetType, out object value)
        {
            // TODO S02: 생성기/에디터 검증용. int,float,string,enum 지원 목록과 오류 정책 명시.
            // TODO: 런타임 생성 코드에서는 아래의 타입별 함수를 직접 사용해 박싱/리플렉션을 피한다.

            value = null;

            if (targetType == typeof(string))
            {
                value = raw;
                return true;
            }
            
            switch (targetType)
            {
                case Type t when t == typeof(int):
                    if (TryInt(raw, out int intValue))
                    {
                        value = intValue;
                        return true;
                    }
                    break;
                case Type t when t == typeof(float):
                    if (TryFloat(raw, out float floatValue))
                    {
                        value = floatValue;
                        return true;
                    }
                    break;
                case Type t when t.IsEnum:
                    if (Enum.TryParse(t, raw, true, out object enumValue) && Enum.IsDefined(t, enumValue))
                    {
                        value = enumValue;
                        return true;
                    }

                    value = default;
                    return false;
            }

            return false;
        }
        public static bool TryInt(string raw, out int value)
        {
            // TODO S02: InvariantCulture, overflow/빈 값 실패.
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
        }
        public static bool TryFloat(string raw, out float value)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }
                return true;
            }

            return false;
        }
        public static bool TryEnum<T>(string raw, out T value) where T : struct, Enum
        {
            // ignoreCase: 대소문자 구분 없이 변환을 시도할 지 설정한다. Enum.IsDefined은 value가 정의된 타입인지 확인한다.
            if (Enum.TryParse<T>(raw, ignoreCase: true, out value) && Enum.IsDefined(typeof(T), value))
            {
                return true;
            }

            value = default;
            return false;
        }
    }
}
