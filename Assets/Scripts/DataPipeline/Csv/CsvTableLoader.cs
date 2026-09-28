using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public class CsvTableLoader<T> where T : class
    {

        public IReadOnlyList<T> Load(CsvDocument document, ICsvBinding<T> binding,
            IRowValidator<T> validator, DataReport report)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            if (report == null)
                throw new ArgumentNullException(nameof(report));

            Dictionary<string, int> headerMap =
                BuildHeaderMap(document, report);

            ValidateHeaders(document, binding, headerMap, report);

            // 헤더 구조가 잘못되었다면 행을 정상적으로 바인딩할 수 없다.
            if (report.HasErrors)
                return Array.Empty<T>();

            if (document.Records == null)
            {
                report.Add(new DataIssue
                {
                    Source = document.Source,
                    Record = 0,
                    Column = string.Empty,
                    RawValue = string.Empty,
                    Message = "CSV 레코드 목록이 없습니다."
                });

                return Array.Empty<T>();
            }

            List<T> results = new(document.Records.Count);

            foreach (CsvRecord record in document.Records)
            {
                if (!IsValidRecordShape(document, record, report))
                    continue;
                if (!binding.TryBind(record, headerMap, report, out T result))
                    continue;

                if (result == null)
                {
                    report.Add(new DataIssue
                    {
                        Source = document.Source,
                        Record = record.Number,
                        Column = string.Empty,
                        RawValue = string.Empty,
                        Message = "바인딩 결과가 null입니다."
                    });

                    continue;
                }

                validator.Validate(result, record.Number, report);
                results.Add(result);
            }

            // 하나라도 오류가 있으면 데이터 전체를 외부에 공개하지 않는다.
            if (report.HasErrors)
                return Array.Empty<T>();

            return results;
        }

        private static void ValidateHeaders(CsvDocument document, ICsvBinding<T> binding, IReadOnlyDictionary<string, int> headerMap, DataReport report)
        {
            // csv 파일에 존재할 것으로 예상되는 헤더(필수, 선택 헤더 전부)와 반드시 존재해야할 헤더(필수만)를 HashSet으로 변환
            HashSet<string> expectedHeaderSet = new(binding.ExpectedHeaders ,StringComparer.OrdinalIgnoreCase);
            HashSet<string> requiredHeaderSet = new(binding.RequiredHeaders ,StringComparer.OrdinalIgnoreCase);

            // 반드시 존재해야할 헤더가 실제 csv에도 존재하는지 확인
            foreach (string requiredHeader in requiredHeaderSet)
            {
                if (headerMap.ContainsKey(requiredHeader))
                    continue;
                else
                {
                    report.Add(new DataIssue
                    {
                        Source = document.Source,
                        Record = 1,
                        Column = requiredHeader,
                        RawValue = string.Empty,
                        Message = $"CSV 필수 헤더 '{requiredHeader}'이(가) 존재하지 않습니다."
                    });
                }
            }

            // 실제 csv에 데이터 모델이 모르는 헤더가 존재하는지 확인. (특히 오타로 인한 문제로 필드명이 불일치하는 문제를 방지하기 위해)
            foreach (string actualHeader in headerMap.Keys)
            {
                if (expectedHeaderSet.Contains(actualHeader))
                    continue;
                else
                {
                    report.Add(new DataIssue
                    {
                        Source = document.Source,
                        Record = 1,
                        Column = actualHeader,
                        RawValue = string.Empty,
                        Message = $"CSV 헤더 '{actualHeader}'이(가) 데이터 모델에 정의되지 않았습니다."
                    });
                }
            }
        }

        private static bool IsValidRecordShape(CsvDocument document, CsvRecord record, DataReport report)
        {
            if (record == null)
            {
                report.Add(new DataIssue
                {
                    Source = document.Source,
                    Record = 0, // record가 null이므로 레코드 번호를 알 수 없음
                    Column = string.Empty,
                    RawValue = string.Empty,
                    Message = $"CSV 레코드가 null입니다."
                });

                return false;
            }

            if (record.Cells == null)
            {
                report.Add(new DataIssue
                {
                    Source = document.Source,
                    Record = record.Number,
                    Column = string.Empty,
                    RawValue = string.Empty,
                    Message = $"CSV 레코드의 셀 배열이 null입니다."
                });

                return false;
            }

            if (record.Cells.Length != document.Headers.Length)
            {
                report.Add(new DataIssue
                {
                    Source = document.Source,
                    Record = record.Number,
                    Column = string.Empty,
                    RawValue = string.Empty,
                    Message =
                        $"헤더 수와 셀 수가 일치하지 않습니다. " +
                        $"헤더: {document.Headers.Length}, 셀: {record.Cells.Length}"
                });

                return false;
            }

            return true;
        }

        private static Dictionary<string, int> BuildHeaderMap(CsvDocument document, DataReport report)
        {
            Dictionary<string, int> headerMap = new(StringComparer.OrdinalIgnoreCase);

            if (document.Headers == null || document.Headers.Length == 0)
            {
                report.Add(new DataIssue
                {
                    Source = document.Source,
                    Record = 1,
                    Column = string.Empty,
                    RawValue = string.Empty,
                    Message = "CSV 헤더가 존재하지 않습니다."
                });
                return headerMap;
            }

            // 각 헤더를 순회하며 인덱스를 매핑
            for (int i = 0; i < document.Headers.Length; i++)
            {
                string headerName = document.Headers[i];

                if (headerName != null)
                {
                    headerName = headerName.Trim(); // 공백 제거

                    if (i == 0)
                    {
                        headerName = headerName.TrimStart('\uFEFF'); // 인코딩 표시용 숨은 문자 BOM을 제거하여 문자열이 같은데도 다르게 인식하는 문제를 방지
                    }
                }

                if (string.IsNullOrWhiteSpace(headerName))
                {
                    report.Add(new DataIssue
                    {
                        Source = document.Source,
                        Record = 1,
                        Column = string.Empty,
                        RawValue = string.Empty,
                        Message = $"CSV 헤더의 {i}번째 열이 비어 있습니다."
                    });

                    continue;
                }

                if (!headerMap.TryAdd(headerName, i))
                {
                    report.Add(new DataIssue
                    {
                        Source = document.Source,
                        Record = 1,
                        Column = headerName,
                        RawValue = string.Empty,
                        Message = $"CSV 헤더 '{headerName}'이(가) 중복되었습니다."
                    });
                }
            }

            return headerMap;
        }
    }
}
