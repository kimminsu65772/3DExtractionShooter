# LOOP-01(로비 준비) · INV-02(장비): 더미 스폰

## 책임

`TargetDummySpawner`는 공통 생성 정의 SO(`EnemySpawnDefinition`)와 포인트별 장비 설정을 보관한다. 생성 정의의 `Prefab`에서 실제 프리팹을 얻는다. 싱글톤을 찾거나 Awake/Start에서 자동 생성하지 않는다. 로딩 담당자가 데이터 준비를 마친 뒤 `TrySpawn(dataRoot)`를 호출한다.

## 인스펙터 설정

1. 기존 훈련 더미 중 하나를 공통 프리팹으로 만든다. 루트에 `HealthComponent`와 `EnemyProtectionLoadout`이 있어야 한다.
2. 체력·방어구 내구도 복구용 `TargetDummyHealthReset`, 피격 콜라이더, 외형과 피격 피드백 등 사용할 기존 구성을 프리팹에 유지한다. 스포너가 이러한 컴포넌트를 임의로 추가하지 않는다.
3. **프리팹 루트는 비활성화한다.** 생성 직후 OnEnable에서 참조를 사용하기 전에 스포너가 데이터 루트를 전달한다. 더미 자식 오브젝트는 원래 활성 설정을 유지한다.
4. 환경 씬에 빈 스포너 오브젝트와 스폰 포인트 네 개를 배치한다. 포인트의 월드 위치와 회전이 생성 위치·회전이 된다. 스포너와 포인트는 같은 씬에 두어 인스펙터 참조를 저장할 수 있게 한다.
5. `Dummy Definition`에 `Assets/GameData/EnemySpawns/TrainingDummy.asset`을 연결하고 `Spawn Entries`의 각 `Spawn Point`를 연결한다. 이 생성 SO의 Prefab은 `Assets/Prefabs/TargetDummy/TrainingTarget_Lv0_Unarmored.prefab`이다. Base_SceneV2의 기존 스포너 참조는 이 방식으로 이전했다.
6. 프리팹 루트의 `EnemySpawnComposition`에서 체력 컴포넌트와 `Assets/GameData/EnemyDefinitions/TrainingDummy.asset`을 연결한다. 이 개별 SO가 이름·네임드 여부를 소유한다. `EnemySpawns`의 생성 SO와 `EnemyDefinitions`의 표시 SO는 역할이 다르다.

기본 설정은 다음과 같으며 인스펙터에서 수정할 수 있다.

| 항목 | 방어구 ID | 헬멧 ID |
| --- | --- | --- |
| 미장착 | 0 | 0 |
| 1등급 | 4001 | 5001 |
| 2등급 | 4002 | 5002 |
| 3등급 | 4003 | 5003 |

장비는 포인트별 ID로 새 더미에 최초 한 번 생성한다. 비활성화·재활성화 때 장비를 재생성하지 않는다. 기존 체력·내구도 복구 동작은 `TargetDummyHealthReset`이 담당한다.

## 로딩 시퀀스 연결

현재 로딩 코드가 Base와 환경 씬을 로드한 뒤 환경 씬의 스포너를 `SceneBootStrap.SetTargetDummySpawner`로 주입한다. 부트스트랩은 데이터 준비 후 다음 생성 메서드를 호출한다.

```csharp
await instance.InitializeAsync();

if (!dummySpawner.TrySpawn(instance))
    return false;
```

위 코드는 SceneBootStrap처럼 bool 결과를 반환하는 초기화 메서드 안의 연결 예시다. `dummySpawner` 참조를 먼저 확보해야 한다. Bootstrap이 Base에 있고 스포너가 Base V2에 있으면 인스펙터로 씬 사이 참조를 저장하려 하지 말고, 두 씬 로드 후 로딩 담당자가 환경 씬에서 스포너를 찾아 전달·호출한다.

## 처리 흐름

- 데이터 준비, 프리팹, 전체 포인트와 장비 ID를 먼저 검사한다.
- 비활성 복제본들을 스포너 자식으로 생성하고 `SetDataRoot()` 후 `TryInitialize(armorId, helmetId)`를 호출한다.
- 모두 구성한 뒤 활성화한다. 구성 실패 시 생성해 둔 복제본은 제거하고 false를 반환한다.
- 생성 성공 후 같은 스포너를 다시 호출해도 중복 생성하지 않고 true를 반환한다.
- 생성 성공 후 `SpawnedGroups`에 각 더미의 Composition을 공개한다. 부트스트랩의 Presenter 참조가 연결되어 있으면 구성원별 SO로 체력바를 등록한다.
- Update나 실시간 탐색은 사용하지 않는다. 스포너 GameObject와 함께 자식 더미의 수명이 관리된다.

## 직접 확인할 항목

- 기존 배치 더미와 새 생성 더미가 겹치지 않도록 기존 배치본을 정리한다.
- 네 포인트에 각각 한 개씩 올바른 방향으로 생성되는지 확인한다.
- 최초 활성화 시 보호 장비의 데이터 참조 오류가 없는지 확인한다.
- 등급별 방어 효과 및 기존 체력·내구도 복구가 유지되는지 확인한다.
- 초기화를 다시 호출해도 더미 수가 증가하지 않는지 확인한다.

씬 배치와 실제 Play Mode 테스트는 직접 진행하는 범위다.
