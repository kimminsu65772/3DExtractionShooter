# LOOP-01~03 — 씬 전환 기반

## 구조

- `GameScene`: 게임 흐름에서 사용하는 GameStart, Lobby, Stage1을 구분한다. enum 숫자는 빌드 인덱스가 아니다.
- `GameSceneManager`: enum을 실제 씬의 전체 경로로 바꾸는 Dictionary와 UniTask 기반 비동기 전환을 담당한다.
- 별도의 GameObject나 컴포넌트를 배치하지 않는다. 씬마다 복제되는 매니저나 DontDestroyOnLoad 오브젝트 없이 같은 정적 진입점을 사용한다.
- 씬 매핑은 코드 한곳에서만 변경한다. 씬 파일을 이동·이름 변경하면 경로도 함께 갱신한다. 인스펙터 편집형 설정 에셋은 현재 필요한 동작이 아니므로 추가하지 않았다.

| enum | 씬 경로 | 현재 범위 |
| --- | --- | --- |
| GameStart | Assets/Scenes/GameStart.unity | Camera·Directional Light만 있는 메뉴 제작 전 기본 씬 |
| Lobby | Assets/Scenes/Lobby/Base_SceneV2.unity | 가져온 로비 주 공간. 원작 공통/추가 씬은 자동으로 열지 않는다. |
| Stage1 | Assets/Scenes/Level_GroundZero/Level_GroundZero_1.unity | 현재 플레이어·적 테스트가 연결된 탐험 씬 |

## 호출

호출은 Unity 메인 스레드와 Play Mode에서 한다. UniTask를 사용하는 호출자의 비동기 메서드에서 다음처럼 기다린다.

매니저 안에서 스레드·Play Mode·빌드 등록 여부를 사전 검사하지 않는다. 호출 조건과 Scene List 설정은 호출하는 코드와 프로젝트 설정에서 맞춘다. 잘못된 enum은 Dictionary 조회에서, 잘못된 씬 로딩은 Unity API에서 발생한 예외를 그대로 전달한다.

```csharp
bool changed = await GameSceneManager.LoadAsync(GameScene.Lobby);
if (!changed)
    return; // 이미 전환 중이거나 로비가 활성 씬이다.
```

`GetScenePath(GameScene.Stage1)`은 매핑된 경로를 반환한다. `TryGetCurrentScene(out GameScene scene)`은 현재 활성 씬을 enum으로 확인하며, 매핑에 없는 테스트 씬에서는 false다. `IsLoading`은 전환 요청 차단이나 버튼 상태 표시에 사용할 수 있다.

첫 대기 전에 IsLoading을 켜 연속 클릭과 서로 다른 목적지의 중복 요청을 거절한다. 별도 요청 큐는 없으며 거절된 요청은 나중에 자동 실행되지 않는다. 현재 활성 씬으로 다시 전환하는 요청은 재시작으로 취급하지 않고 거절한다.

로딩은 Single 방식이다. 기존 일반 씬과 오브젝트는 교체되며, `CombatDataCompositionRoot`만 별도의 싱글톤 수명으로 보존한다. 플레이어·인벤토리 상태의 씬 간 전달까지 해결된 것은 아니다. 원작의 Additive 서브 씬 조합도 이번 매니저에는 포함하지 않는다.

## 공용 전투 데이터 루트 — CORE-02(리소스 로딩 및 수명 관리)

- `CombatDataCompositionRoot.Instance`는 최초 루트의 `Awake`에서 설정한다. 루트를 독립된 최상위 오브젝트로 분리한 뒤 `DontDestroyOnLoad(gameObject)`로 씬 전환 중 보존한다.
- 다음 씬에 중복 루트가 있으면 그 GameObject를 제거한다. 중복 루트는 파이프라인 초기화를 시작하지 않으며, 제거될 때 기존 루트의 `Instance`나 리소스를 정리하지 않는다.
- 실제 소유 루트의 파괴 시에만 `Instance`를 비우고 기존 초기화 취소·파이프라인 종료·토큰 해제를 수행한다. 씬 전환 자체는 종료 조건이 아니다.
- `Instance != null`은 객체가 생성됐다는 뜻이지 데이터 준비 완료가 아니다. 기존 `isReady`와 `OnInitialized`로 로드 완료를 구분한다.
- 비활성 풀 루트가 비어 있으면 기존처럼 데이터 루트 아래에 생성되어 함께 보존된다. 별도 오브젝트를 `inactivePoolRoot`로 연결한다면 그 오브젝트도 데이터 루트의 자식으로 배치해야 한다.
- 씬에 직렬화한 참조가 싱글톤으로 자동 교체되지는 않는다. 기존 GroundZero 루트가 중복으로 제거되면 그 루트를 가리키던 플레이어·적·UI 참조는 별도 연결 작업이 필요하다. 이번에는 다른 컴포넌트나 씬 배치를 변경하지 않았다.
- 데이터 루트가 씬의 적을 탐색하거나 참조를 배분하지 않는다. 씬 Bootstrap 또는 적 생성 담당자가 `Instance`를 얻어 필요한 컴포넌트에 전달하는 방식으로 책임을 구분한다. 적 장비는 `OnEnable`에서 참조를 사용하므로 최초 활성화 전에 연결해야 한다.
- 사용 중인 투사체·VFX의 씬 종료 시 반환과 플레이어 런타임 소지 상태 보존은 별도 연결 범위다.
- 2026-10-16 검증: 관련 프로젝트를 포함한 `dotnet build Assembly-CSharp.csproj --no-restore`를 통과했다. 최초 시도에서 프로젝트 참조 빌드를 생략하여 의존 DLL이 없었고, 참조 프로젝트까지 정상 빌드한 뒤 오류 없이 완료했다. Unity CLI 실행 파일과 현재 Unity MCP 도구가 없어 실제 Play Mode 중복 생성·씬 전환 검증은 수행하지 않았다.

## 스폰 시 외부 참조 연결 — LOOP-02(탐험 진입)

- `PlayerController.SetCamera(Transform)`과 `PlayerAimController.SetCamera(Camera)`로 씬 카메라를 연결한다.
- `PlayerWeaponHolder`, `PlayerAttackController`, `PlayerReloadController`, `PlayerInventory`, `PlayerWorldItemDropper`의 `SetDataRoot(CombatDataCompositionRoot)`에 같은 데이터 루트를 전달한다.
- `QuaterViewCameraController.SetTarget(Transform, PlayerInputHandler, PlayerAimController)`로 새 플레이어의 추적 대상·입력·조준 참조를 함께 연결한다.
- 플레이어는 비활성 상태로 생성하고 위 참조를 모두 연결한 뒤 최초 활성화한다. 특히 인벤토리는 `OnEnable`에서 데이터 초기화 이벤트를 구독하므로 활성화 후에 데이터 루트만 교체하는 용도로 사용하지 않는다.
- 이 메서드는 참조 대입만 담당한다. 데이터 로드 완료 대기, 스폰 코드, 씬 배치, 씬 간 인벤토리 전달은 아직 구현하지 않았다.
- 2026-10-16 검증: Unity 재컴파일 후 비활성 임시 오브젝트에서 8개 메서드를 호출하고 카메라·데이터 루트·추적 대상 필드가 전달한 객체를 참조하는지 확인했다. 임시 오브젝트는 검사 후 제거했다. 실제 플레이 모드 스폰·씬 전환은 테스트하지 않았다.

## GroundZero 기본 카메라 — ACT-02(조준)

- `Level_GroundZero_1`의 Main Camera에 원본 기본값을 적용했다. 회전은 pitch 55도·yaw -30도, 일반/조준 거리 모두 45m, 추적 기준점은 플레이어 위치 + Y 0.5m다. 거리가 45m이므로 플레이어보다 약 37.36m 높게 위치한다.
- 원근 투영이며 일반 FOV는 23.5도, 조준 FOV는 22.8도다. 이 씬에서만 `useFieldOfViewSettings`를 켰다. 원본의 출발 구역 Volume이나 ADS 진행률 보간은 이식하지 않았다.
- 기존 조준 오프셋과 추적 보간값, 다른 씬은 변경하지 않았다. Unity에서 필드와 초기 Transform을 확인하고 씬 저장 결과를 대조했다. 실제 조준·이동 화면의 플레이 모드 검증은 남아 있다.

## 저장·로딩 화면과의 경계

매니저가 임의로 저장하거나 다음 목적지를 정하지 않는다. 저장이 필요한 게임 흐름에서는 먼저 기존 SaveScheduler의 승인된 요청을 완료한 뒤 LoadAsync를 호출한다. 저장 예외를 잡고도 전환을 강행하지 않는다.

```csharp
await saveScheduler.RequestSaveAsync(data, SavePolicy.Immediate);
await saveScheduler.FlushAsync();
await GameSceneManager.LoadAsync(GameScene.Lobby);
```

위 코드는 연결 순서 예시이며 사망·탈출·창고 이벤트에 아직 연결하지 않았다. 탐험 중 주기적 저장이나 종료 시 저장은 추가하지 않았다. 별도의 Loading 씬, 화면 전환 효과, 저장 실패/재시도 UI도 이번 범위가 아니다.

Unity 씬 로딩은 취소할 수 없으므로 호출자의 Destroy 토큰으로 대기만 취소하지 않는다. 전환 예외가 발생해도 finally에서 요청 잠금을 해제하고 예외는 호출자에게 전달한다. 로딩 완료는 Unity의 씬 로딩 작업 완료이지, 새 씬의 전투 데이터나 Addressables 초기화 완료를 의미하지 않는다. 새 씬에서는 기존 초기화 완료 신호를 별도로 기다린다.

## 설정과 확인 범위

- 세 목적지를 공통 Build Settings의 Scene List에 활성 상태로 등록했다. 기존 SampleScene과 시작 순서는 보존했다. 실제 시작 메뉴가 완성되면 GameStart를 목록 맨 앞으로 옮긴다. 별도 Build Profile에서 Scene List를 덮어쓰면 그 프로필에도 같은 목적지를 등록해야 한다.
- GameStart에 메뉴나 자동 Lobby 전환은 없다. 실행하면 기본 카메라 화면만 보이는 것이 현재 범위다.
- Lobby의 Missing Script, 원작 전용 셰이더, 플레이어·카메라·창고 기능 복원은 별도 작업이다.
- 컴파일 및 씬 경로·등록 설정을 확인한다. 요청에 따라 Play Mode 씬 전환 테스트는 수행하지 않는다.

## 2026-10-16 확인 기록

- 전체 AssetDatabase 갱신 후 GameScene과 GameSceneManager가 Assembly-CSharp에 컴파일되어 Unity에서 인식되는 것을 확인했다. 이 시점 Console 오류는 없었다.
- Unity Editor API로 Camera·Directional Light가 있는 GameStart 씬을 만들었다. 기존 GroundZero 씬은 저장하지 않았고, 생성 직후 활성 경로와 미변경 상태를 확인했다.
- EditorBuildSettings.scenes 설정의 응답에는 네 씬이 표시됐으나 파일에 저장되기 전 Unity/MCP 응답이 중단됐다. 기존 파일 내용을 확인한 뒤 세 씬 항목만 추가하고, 각 GUID를 씬 메타와 대조했다. 기존 SampleScene과 configObjects는 보존했다.
- 이후 에디터 연결이 복구되어 get_build_settings로 SampleScene(0), GameStart(1), Base_SceneV2(2), Level_GroundZero_1(3)이 모두 활성 등록된 것을 다시 확인했다. Console 오류도 없었다. 강제 종료·재시작은 하지 않았다.
- Play Mode 전환·로비 런타임·동작 테스트는 수행하지 않았다.

## 2026-10-16 방어 코드 정리

- 현재 프로젝트의 씬 전환 흐름에 집중하도록 메인 스레드·Play Mode 검사, Domain Reload 대응 초기화, 빌드 등록 사전 검사, 로딩 결과 null 검사와 별도 enum 오류 처리를 제거했다. 다른 검사나 초기화 장치로 옮겨 넣지 않았다.
- 씬 매핑과 `GetScenePath`, `TryGetCurrentScene`, `IsLoading`, 중복 전환 차단, Single 비동기 로딩, `finally`의 잠금 해제는 남겼다.
- 씬 이름 방식으로 변경하자는 앞선 질문은 취소되었으므로 경로 매핑과 Build Settings는 변경하지 않았다.
- Unity 재컴파일 후 `ResetState` 제거와 기존 세 씬 매핑·`IsLoading` 조회를 확인했다. 컴파일 오류는 없으며, 실제 Play Mode 전환은 실행하지 않았다.
