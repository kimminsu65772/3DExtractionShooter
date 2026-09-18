using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class CsvTableLoader<T> where T : class
    {
        public IReadOnlyList<T> Load(CsvDocument document, ICsvBinding<T> binding,
            IRowValidator<T> validator, DataReport report)
        {
            // TODO S03: BOM/공백 처리 후 OrdinalIgnoreCase 헤더 맵. 중복/누락/미지정 헤더 검사.
            // TODO: 행별 binding→validator. 모든 오류를 수집하고 하나라도 있으면 공개용 결과 생성 거부.
            // TODO: 레코드 수/열 수 불일치 검사. 바인딩의 Optional 정책과 누락 검사를 일치시킨다.
            // TODO: Weapon 전용 타입/필드명을 여기에 넣지 않는다.
            throw new NotImplementedException();
        }
    }
}
