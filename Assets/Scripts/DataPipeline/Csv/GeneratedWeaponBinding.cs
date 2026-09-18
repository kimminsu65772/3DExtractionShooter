using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // TODO S02: 첫 생성 완료 시 이 파일을 생성기로 대체하고 auto-generated 헤더를 붙인다.
    // 이 파일만 손으로 31개 대입문을 채우면 반복 유지보수 문제가 해결되지 않는다.
    public sealed class GeneratedWeaponBinding : ICsvBinding<WeaponDefinition>
    {
        public IReadOnlyList<string> ExpectedHeaders => throw new NotImplementedException("TODO S02");
        public bool TryBind(CsvRecord row, IReadOnlyDictionary<string, int> header,
            DataReport report, out WeaponDefinition result)
        {
            // TODO S02: CsvBindingGenerator가 타입별 TryParse와 직접 필드 대입을 출력한다.
            // TODO: 변환 실패 위치를 DataReport에 기록. 필수 값에 코드 기본값을 조용히 사용하지 않는다.
            // TODO: 무기 생성은 임시 객체에서 하고 행 성공 시에만 result 반환.
            throw new NotImplementedException();
        }
    }
}
