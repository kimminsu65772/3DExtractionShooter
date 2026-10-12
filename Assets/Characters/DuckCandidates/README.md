# 오리 캐릭터 후보 프리팹

ENEMY-02(적 종류 확장)의 외형 후보를 고정 프리팹으로 조립했다.

## 구성

- `Prefabs/Duck_Default.prefab`
- `Prefabs/Duck_YellowDuck.prefab`
- `Prefabs/Duck_Solider.prefab` — 원본의 철자를 그대로 사용한다.
- `Prefabs/Duck_Usec.prefab`
- `Prefabs/Duck_PMCLeader.prefab`
- `Prefabs/Duck_ScavBlue.prefab`
- `Prefabs/Duck_ScavRed.prefab`
- `Prefabs/Duck_Killa.prefab`

`DuckCandidateGallery.unity`에서 8종을 함께 확인한다. 모델·머리 형태·눈·입·날개·발 등을 저장한 외형 프리팹이며, AI·체력·장비·무기 자체는 포함하지 않는다. 기존 플레이어와 적 프리팹을 자동 교체하지 않는다.

## 조립 원리와 차이

원본 `CustomFaceData`의 파츠 ID/GUID 대응을 파일명으로 해석하고 `CustomFacePreset`의 배치·색상·머리 설정을 고정 Transform과 머티리얼에 반영한다. 실행 중 원작 DLL이나 외형 조립 스크립트를 요구하지 않는다. 원본과 동일한 셰이더 렌더링을 재현하는 것은 아니며 URP 머티리얼로 구성한다.

Default와 Killa의 원본 0번 눈·입은 빈 파츠이므로 기본 1번 파츠를 보충했다. Killa의 별도 헬멧·방어구나 각 캐릭터의 장비는 포함하지 않는다. 비어 있는 머리카락·눈썹 선택지는 그대로 둔다.

기존 `DuckPlayerCopy`의 Avatar와 `DuckEnemyCopy`의 애니메이션 컨트롤러를 재사용한다. 따라서 이 두 폴더는 유지해야 한다. `Assets/Test`에는 의존하지 않는다.

에디터 생성기는 `Assets/Editor/DuckCandidatePrefabBuilder.cs`이며 메뉴 `Tools > Duck Candidates > Build Fixed Prefabs`로 실행한다. 메뉴는 기존 프리팹을 덮어쓰지 않는다. 생성 시 원본 추출 폴더가 필요하지만, 만들어진 프리팹을 사용할 때는 필요하지 않다.

## 확인 범위

8종 생성, Missing Script 및 Test 폴더 의존성 검사, 비교 씬 렌더링을 확인했다. 외형 프리팹이므로 실제 플레이어·적에 교체한 뒤의 전투와 장비 부착은 별도 확인 대상이다.
