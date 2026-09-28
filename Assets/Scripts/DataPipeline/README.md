# CSV → Addressables → Pool 학습용 코드 뼈대

가이드: [옵시디언 시작 페이지](<C:/Users/admin/Desktop/IndividualProject/프로젝트 개발 일지/Knowledge/Unity/CSV에서 Addressables 풀링까지 구현 가이드/00 가이드 시작과 완료 기준.md>).

이 폴더는 요청에 따라 만든 미구현 골격이다. 기존 게임의 로더/발사를 자동 교체하지 않는다.
namespace는 ExtractionShooter.DataPipeline이며 기존 WeaponDefinition을 데이터 모델로 참조한다.

- S01: CsvHelperRecordReader, Contracts의 보고서.
- S02: CsvValueConverter, Editor/CsvBindingGenerator, GeneratedWeaponBinding.
- S03: CsvTableLoader, WeaponValidator.
- S04: WeaponCatalog, RuntimeAssetRegistry.
- S05: PoolPlanBuilder.
- S06: AddressablesAssetStore, AssetLease.
- S07: PrefabPool, KeyedPoolService.
- S08: Integration의 Spawn/Feedback/세 Lifecycle.
- S09: CombatDataBootstrap 및 호출자 구성.

각 메서드의 TODO를 순서대로 구현한다. 예외는 미구현을 알리는 표시이므로 호출 전에 채운다.
모델/계약에 공개 필드를 둔 것은 작성 시작점이다. lease와 카탈로그는 해당 단계에서 외부 수정 불가로 완성한다.

Addressables/CsvHelper 미설치 상태에서도 골격 타입은 컴파일할 수 있게 외부 API는 TODO에 명시했다.
대체 리소스 로더를 구현한 것이 아니다. S01/S06에서 실제 패키지를 설치하고 명시된 API를 작성한다.
새로운 MonoBehaviour에는 Update/Awake 자동 실행 코드가 없어 씬에 붙이지 않는 한 동작하지 않는다.
기존 Projectile의 Initialize 활성화/RequestReturn 변경은 S08에서 수행한다.

## 산출물 검증

2026-09-18: 새 C# 20개와 기존 WeaponDefinition/Enums를 Unity 참조를 사용한 별도 검증 프로젝트에서 컴파일했다. 오류 0개, 경고 0개.
이는 골격의 문법·타입 검증이다. TODO의 기능 동작, 패키지 호환성, 씬/Player 실행과 메모리 검증을 통과했다는 뜻은 아니다.
