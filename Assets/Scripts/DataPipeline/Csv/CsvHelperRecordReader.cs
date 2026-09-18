using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using System.Globalization;

namespace ExtractionShooter.DataPipeline
{
    public sealed class CsvHelperRecordReader : ICsvRecordReader
    {
        /// <summary>
        /// CSVReader를 통해 CSV 파일을 읽고 CsvDocument를 생성한다.
        /// </summary>
        /// <param name="reader">CSV 글자를 순서대로 읽어 주는 입력을 의미한다.</param>
        /// <param name="source">소스파일의 파일명 또는 주소를 의미한다.</param>
        /// <param name="report">읽는 과정에서 발견한 오류를 모을 곳을 의미한다.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public CsvDocument Read(TextReader reader, string source, DataReport report)
        {
            // CsvReader를 사용하여 CSV 파일을 읽는다.
            // 이때 CultureInfo.InvariantCulture를 사용하여 문화권에 상관없이 CSV를 읽도록 한다.
            // leaveOpen: true를 설정하여 TextReader가 닫히지 않게 하여 호출자가 TextReader를 관리할 수 있도록 한다.
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture, leaveOpen: true);
            if (!csv.Read())
            {
                // 파일이 비어있거나 읽을 수 없는 경우
                report.Add(new DataIssue
                {
                    Source = source,
                    Record = 0,
                    Column = "",
                    RawValue = "",
                    Message = "CSV에 헤더를 포함한 레코드가 없습니다."
                });
                return null;
            }
            csv.ReadHeader(); // 현재 레코드를 헤더로 읽는다.

            List<CsvRecord> records = new();

            CsvDocument document = new CsvDocument
            {
                Source = source, // 어느 파일에서 읽었는가?
                Headers = (string[])csv.HeaderRecord.Clone(), // 헤더를 복사하여 저장함으로써, 결과가 별도의 객체로 존재하도록 한다.
                Records = records
            };

            // CSV 파일의 각 레코드(행)를 읽어 CsvRecord 객체로 변환하여 records 리스트에 추가한다.
            // 이때, 파싱 과정은 csv.Read()의 내부 메서드에 의해 수행되며, 각 레코드의 셀 값은 csv.Parser.Record를 통해 접근할 수 있다.
            while (csv.Read())
            {
                CsvRecord record = new CsvRecord
                {
                    Number = records.Count + 2, // 현재 레코드의 행 번호를 저장한다. 이때 csv에서 1행은 header이므로 실제 데이터는 2행부터 시작한다.
                    Cells = (string[])csv.Parser.Record.Clone() // 현재 레코드의 파싱된 셀 값을 복사하여 저장한다.
                };

                records.Add(record);
            }

            return document;
        }
    }
}
