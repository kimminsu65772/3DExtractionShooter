# Assets 폴더 정리

2026-10-09. 테스트 파일은 모두 보존하고 삭제 없이 분류한다.

| 위치 | 역할 |
| --- | --- |
| Scripts, Editor | 프로젝트 코드와 제작 도구 |
| Prefabs, Scenes, Characters | 게임 프리팹·씬·조립한 캐릭터 |
| Art/UI/DuckovReference | 선별한 UI 이미지 |
| Third_Party_Assets/DuckovImported | 원작 추출본의 리소스 종류별 폴더 22개 |
| Third_Party_Assets, SoStylized | 외부 에셋 |
| Test | 기존 시험 스크립트·씬·자료·백업 모두 보존 |
| AddressableAssetsData, GameData | 로딩 설정과 게임 데이터 |
| Resources, StreamingAssets | 경로 기반 로딩에 영향을 주므로 이동하지 않음 |
| Packages, TextMesh Pro, URPDefaultResources 및 루트 설정 파일 | 패키지·렌더링 설정으로 보존 |
| Shader | include 경로·외부 셰이더 사용 여부를 별도로 확인하기 위해 보존 |

추출 리소스 폴더 이동은 Unity AssetDatabase.MoveAsset을 사용해 GUID를 유지한다. GameObject·AnimationClip 경로를 직접 읽는 캐릭터 생성기와 테스트 생성기도 새 경로로 수정한다. 프로젝트의 Animations/AnimationClip, Materials/Material, Textures/Texture2D처럼 의미가 다른 폴더는 이름이 비슷하다고 합치지 않는다.

미사용 리소스 삭제와 Resources/StreamingAssets 정리는 아직 수행하지 않았다. 에디터에서 직접 로드하는 파일, 주소 기반 리소스, 테스트에서만 쓰는 파일도 있으므로 일반 씬 의존성에 없다는 이유만으로 삭제하지 않는다.
