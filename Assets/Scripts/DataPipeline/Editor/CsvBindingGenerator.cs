using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // Editor 폴더: 생성 시에만 reflection 사용. 런타임 DLL에 포함시키지 않는다.
    public static class CsvBindingGenerator
    {
        public static string Generate(Type modelType)
        {
            // TODO S02: public instance field 목록 + CsvColumn/CsvIgnore. static/readonly/NonSerialized 제외.
            // TODO: int,float,string,enum 이외는 진단. 순서를 Ordinal 정렬해 재현 가능한 출력.
            // TODO: ExpectedHeaders, optional 정보, 타입별 TryParse와 직접 대입을 텍스트로 생성.
            // TODO: 이름/문자열 escaping, keyword 이름 처리, 스키마 지문 출력.
            throw new NotImplementedException();
        }
        public static void WriteIfChanged(Type modelType, string destination)
        {
            // TODO S02: 허용된 Generated 경로만 사용. 내용 동일하면 미쓰기(재컴파일 루프 방지).
            // TODO: 임시 파일 완성 후 교체. AssetDatabase.Refresh는 변경 있을 때 한 번.
            throw new NotImplementedException();
        }
        public static void ValidateGeneratedCode(Type modelType, string destination)
        {
            // TODO S02: 빌드 전 재생성 결과 비교. 낡은 스키마면 BuildFailedException으로 막는다.
            // TODO: Editor 메뉴 및 IPreprocessBuildWithReport 연결을 이 단계에서 추가한다.
            throw new NotImplementedException();
        }
    }
}
