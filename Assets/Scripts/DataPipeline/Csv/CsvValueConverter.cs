using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public static class CsvValueConverter
    {
        public static bool TryConvert(string raw, Type targetType, out object value)
        {
            // TODO S02: 생성기/에디터 검증용. int,float,string,enum 지원 목록과 오류 정책 명시.
            // TODO: 런타임 생성 코드에서는 아래의 타입별 함수를 직접 사용해 박싱/리플렉션을 피한다.
            throw new NotImplementedException();
        }
        public static bool TryInt(string raw, out int value)
        {
            // TODO S02: InvariantCulture, overflow/빈 값 실패.
            throw new NotImplementedException();
        }
        public static bool TryFloat(string raw, out float value)
        {
            // TODO S02: InvariantCulture + NaN/Infinity 거부.
            throw new NotImplementedException();
        }
        public static bool TryEnum<T>(string raw, out T value) where T : struct, Enum
        {
            // TODO S02: 이름만 허용할지 수치도 허용할지 정책 고정. 미정의 enum 거부.
            throw new NotImplementedException();
        }
    }
}
