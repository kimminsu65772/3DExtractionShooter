using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class CsvHelperRecordReader : ICsvRecordReader
    {
        public CsvDocument Read(System.IO.TextReader reader, string source, DataReport report)
        {
            // TODO S01: 검증한 CsvHelper DLL과 종속성을 설치한 후 CsvReader/Parser 사용.
            // TODO: UTF-8 BOM, RFC형 따옴표/쉼표/개행, 빈 마지막 셀 처리. Split(',') 금지.
            // TODO: 데이터 한 레코드의 Cells를 복사해 다음 Read의 버퍼 재사용으로부터 분리.
            // TODO: 파일/레코드/필드 크기 제한, 문법 오류 보고. 전달받은 TextReader의 소유권은 호출자.
            throw new NotImplementedException();
        }
    }
}
