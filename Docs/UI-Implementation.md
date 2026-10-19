# UI 구현 코드 안내

대상: UI-01(핵심 HUD), WORLD-02(전리품·상호작용). 2026-10-19 기준.

## 파일별 책임

| 파일 | 수정할 상황 |
| --- | --- |
| `Assets/Scripts/UI/UIManager.cs` | 싱글톤 팝업 스택, ESC 처리, 로딩/씬 전환 시 창 정리 |
| `Assets/Scripts/UI/EscapeMenuController.cs` | ESC 메뉴 레퍼런스 생성, 열기·닫기 |
| `Assets/Scripts/UI/PauseMenuController.cs` | 패널 활성 시 게임 시간 정지·닫힐 때 복구, Resume·MainMenu·Quit과 확인 팝업 처리 |
| `Assets/Scripts/UI/Inventory/InventoryUIController.cs` | 인벤토리 직렬화 참조, 상태, Unity 생명주기, 키 입력 |
| `Assets/Scripts/UI/Inventory/InventoryUIController.View.cs` | 저장된 화면 프리팹 로드·바인딩, 슬롯·무게·상세 표시 갱신 |
| `Assets/Scripts/UI/Inventory/InventoryUIController.Windows.cs` | 인벤토리·전리품·상세·우클릭 창 열기/닫기, 팝업 등록, 커서 전환 |
| `Assets/Scripts/UI/Inventory/InventoryUIController.Items.cs` | 클릭, 장착, 퀵슬롯 등록, 드래그 이동, 버리기 요청 |
| `Assets/Scripts/UI/Inventory/InventoryUIController.Ammo.cs` | 총 해제, T/휠 선택, 선택 수명과 빈 장비 슬롯 안내 숨김 |
| `Assets/Scripts/UI/Inventory/AmmoSelectionView.cs` / `AmmoSelectionEntryView.cs` | 원본 탄종 안내·선택 템플릿의 문구/수량/선택 표시 |
| `Assets/Scripts/UI/Inventory/InventoryUIView.cs` | Inspector에서 연결한 화면·그리드·문구·아이콘 참조를 컨트롤러에 전달 |
| `Assets/Scripts/UI/Inventory/InventoryHotbarView.cs` | 무기·아이템 퀵슬롯의 공통 프리팹 참조 |
| `Assets/Scripts/UI/Inventory/InventoryDropContextMenu.cs` | 준비된 우클릭 메뉴의 이벤트 연결과 포인터 위치 표시 |
| `Assets/Scripts/UI/Inventory/InventorySlotView.cs` | 슬롯 표시와 포인터/드래그 이벤트 전달 |
| `Assets/Scripts/UI/InteractionPromptUI.cs` | 상호작용 대상 중앙 마크, 좌상단 툴팁, 월드 위치를 화면 위치로 변환 |
| `Assets/Scripts/UI/InteractionProgressUI.cs` | 상호작용 진행 이벤트를 상호작용 진행 바에 바인딩 |
| `Assets/Scripts/UI/ActionProgressUI.cs` | 재장전·회복 아이템 사용의 진행률·남은 시간·행동 문구를 공통 링에 바인딩 |
| `Assets/Scripts/UI/ProgressHUD.cs` | 공통 진행 HUD 안의 두 UI에 현재 플레이어 소스를 연결 |
| `Assets/Scripts/UI/UnitHealthBarPresenter.cs` | 생성된 구성원별 SO를 받아 체력바 생성·체력 이벤트 연결·위치 추적 |
| `Assets/Scripts/UI/UnitHealthBarView.cs` | 이름·네임드 아이콘, 체력 보간, 피해 지연 잔상·회복 초록 표시 |

`InventoryUIController`의 다섯 파일은 `partial`로 나눈 **하나의 컴포넌트**다. Inspector에 추가할 컴포넌트는 기존 `InventoryUIController` 하나다. 기존 직렬화 필드와 프리팹 연결을 보존하며, Ammo 파일은 선택 UI의 임시 상태를 보관한다. 같은 컴포넌트의 상태를 공유한다.

## 실행 흐름

### ACT-04(탄약·장전)·UI-01(핵심 HUD): 프레임별 탄종 셀렉터

`InventoryHUD`의 각 `WeaponButton_1/AmmoSelection`, `WeaponButton_2/AmmoSelection`에서 배치를 편집한다. `InventoryUIView.ammoSelectors[0]`은 무기 슬롯 0, `[1]`은 무기 슬롯 1에 대응하며 선택한 총의 프레임 하나만 활성화된다. 빈 선택 슬롯·빈손·소모품에는 안내를 숨긴다. `openAmmoSelector`는 실제로 열었던 뷰를 보관해 슬롯이 바뀌어도 이전 창을 닫는다.

`Tools > ExtractionShooter > UI > Connect Ammo Selection`은 기존 참조만 연결하고 검증한다. Inspector 연결을 우선하며 빈 칸은 해당 무기 프레임 아래에서 찾는다. 셀렉터가 없거나 중복/불완전하면 오류를 내며 UI 생성·위치·폰트 수정은 하지 않는다. 프리팹 모드에서는 현재 편집 중인 내용에 연결하므로 일반 프리팹 저장/자동 저장을 사용한다. 동적 탄종 목록 행은 기존 Entry Template을 필요한 개수만 복제한다.

### UI-01(핵심 HUD): 캐릭터별 이름·아이콘 데이터

- FOV 표시: Presenter는 등록 시 각 HealthComponent의 부모 경로에서 VisibilityTarget을 캐시한다. IsVisible=false이면 해당 체력바 루트를 숨기고, true이면 위치를 갱신한 뒤 같은 뷰를 다시 표시한다. VisibilityTarget 없는 더미는 표시한다. 추가 Raycast는 사용하지 않는다.
- 개별 데이터 SO는 `Assets/GameData/EnemyDefinitions/`의 `Enemy_Ranged`, `Enemy_USEC`, `Enemy_PatoChapo`, `TrainingDummy`다. Display Name은 직접 입력한다. USEC와 꼬마덕의 Is Named는 켜져 있고 나머지는 꺼져 있다.
- `EnemySpawnDefinition`은 생성 프리팹만 지정한다. 개별 이름·네임드 여부는 `EnemyDefinition`이 소유한다.
- 스폰 프리팹 루트의 `EnemySpawnComposition.Members`는 `HealthComponent`와 `EnemyDefinition`의 연결 목록이다. 분대는 프리팹 하나로 유지하며 대장에는 꼬마덕 SO, 호위 3명에는 같은 USEC SO를 연결했다. 자식 배열 순서나 오브젝트 이름으로 이름을 결정하지 않는다.
- `StageEnemySpawner`와 `TargetDummySpawner`는 전체 생성 성공 후 `SpawnedGroups`를 공개한다. 생성 준비 중 Composition의 누락·중복·루트 외부 참조를 검사한다.
- `SceneBootStrap`의 `Unit Health Bar Presenter`에 같은 Base/Stage 씬의 Presenter를 연결하면, 스폰 성공 후 `Register(composition)`으로 각 구성원을 등록한다. 참조가 비어 있으면 체력바 등록만 생략한다. Inspector 씬 연결은 직접 진행한다.
- 이번 작업은 데이터/생성 후 등록 경로를 연결한 것이다. 사망/시야/화면 밖 숨김 등 체력바 전체 정책 구현과 플레이 검증 완료를 의미하지 않는다.
- 파괴 정리: Presenter가 HealthComponent.OnDestroyed를 구독해 유닛 파괴 시 Unregister한다. 체력·파괴 이벤트 해제와 View 제거를 같은 경로에서 처리하고, Presenter가 제거될 때도 모든 등록을 정리한다. 체력만 0이 된 뒤 복구하는 더미는 파괴와 구분한다. 별도의 시야/화면 밖/사망 시 숨김 정책은 후속이다.

### 기존 공용 UI

1. `UIManager`가 시작 시 `Resources/UI/EscapeMenuHost`를 생성한다. ESC는 최상위 팝업 하나를 닫고, 팝업이 없으면 메뉴를 연다. 로딩 중에는 처리하지 않는다.
   생성된 메뉴의 `PauseMenuController`는 활성화 시 기존 Time.timeScale을 저장하고 0으로 설정한다. Resume·ESC·씬 전환 등으로 패널이 비활성화되거나 제거되면 기존 속도로 복구한다. 확인창만 닫으면 정지를 유지한다. Resume로 메뉴를 닫으며 MainMenu·Quit은 로비에서 즉시 실행하고, 그 외 씬에서는 기존 경고 팝업으로 확인한다. 확인창도 스택에 등록되어 취소/ESC로 확인창만 닫을 수 있다. 타이틀 이동은 `GameSceneManager.LoadAsync(GameScene.GameStart)`, 종료는 `Application.Quit()`을 사용한다. 두 동작은 메뉴를 먼저 닫아 게임 시간을 복구한 뒤 실행한다.
2. 플레이어의 `InventoryUIController.Start()`에서 `BuildView()`가 연결된 InventoryUIView 프리팹을 생성하고 데이터를 바인딩한다. Player_YellowDuck의 View Prefab은 InventoryHUD에 연결되어 있다. 참조가 없는 다른 플레이어는 Resources/UI/InventoryHUD를 사용한다. 코드로 대체 화면을 만드는 폴백은 없다. 이미 배치한 Canvas를 사용하려면 그 루트에 InventoryUIView와 참조를 연결한다.
3. 슬롯 이벤트는 `InventorySlotView` → 컨트롤러 → 기존 인벤토리/장비 API로 전달된다. 화면은 `LateUpdate()`에서 현재 데이터를 읽어 갱신한다.
4. 상호작용 HUD는 `SceneBootStrap`에서 생성하고 플레이어의 `PlayerInteractionController`와 카메라를 연결한다. 대상 선택은 기존 상호작용 컨트롤러가 맡는다.
5. `InteractionPromptUI`는 프리팹에 준비된 visualRoot와 actionText를 사용한다. 매 프레임 월드 대상 위치를 따라가며, 팝업이 하나라도 열려 있거나 로딩/입력 차단 중이면 마크와 툴팁을 숨긴다. 계층·글꼴·툴팁 배치를 실행 중 재구성하지 않는다.

## 레퍼런스 연결 위치

- ESC 메뉴: `Assets/Resources/UI/EscapeMenuHost.prefab`
- 인벤토리·장비·창고/전리품·상세창·무게·드래그 표시: `Assets/Resources/UI/InventoryHUD.prefab`
- 무기·아이템 퀵슬롯 공통 편집: `Assets/Prefabs/UI/Inventory/Hotbar.prefab` (InventoryHUD에 중첩)
- 슬롯 템플릿: `Assets/Prefabs/UI/Inventory/InventorySlot.prefab`
- 우클릭 메뉴: `Assets/Prefabs/UI/Inventory/DropContextMenu.prefab` (InventoryHUD에 중첩)
- 상호작용 HUD: `Assets/Resources/UI/InteractionPromptHUD.prefab`
- 표시용 원본 레퍼런스: `Assets/Art/UI/DuckovReference/`

실제로 사용하는 프리팹의 TMP는 `Assets/Third_Party_Assets/HiddenWarehouseRestored/Dependencies/Font/Jua-Regular SDF.asset`을 참조한다. 글꼴은 해당 프리팹 Inspector에서 편집한다. 과거 `InventoryReferenceAssets.asset`은 원본 레퍼런스 목록으로만 남아 있고 런타임 화면 생성에 사용하지 않는다.

`Hotbar.prefab` 루트 아래에서 무기와 아이템 퀵슬롯을 한 화면에서 조정한다. 초기 위치·크기·피벗은 전환 시점의 레퍼런스 프리팹 값을 보존했다. 코드가 편집값을 덮어쓰지 않는다. 가방·전리품의 칸 수가 바뀔 때는 InventorySlot 프리팹을 필요한 수만큼 복제하고, 배치는 저장된 GridLayoutGroup이 맡는다.

중량 표시는 InventoryHUD의 InventoryUIView.weightFill → `Inventory Windows/Content/EquipmentAndInventory/Content/WeightBar/BarArea/BGAndMAsk/MainBar`에 연결된다. 가로 바 위의 TMP에 current/max kg를 표시하며, 보조 WeightBar의 숨겨진 Fill은 사용하지 않는다. 배치/활성 설정은 프리팹에 저장하고 컨트롤러는 PlayerCarryWeight의 값과 fillAmount만 갱신한다.

진행 UI의 배치는 `Assets/Resources/UI/ProgressHUD.prefab`의 `ProgressAnchor`에서 편집한다. 하단 중앙 기준 Y=260이며, 자식으로 기존 InteractionProgressUI·ReloadProgressHUD 프리팹을 중첩한다. 두 표시의 중심은 이 공통 프리팹 안에서 부모 기준 (0,0)으로 맞춰져 있다. 높이는 ProgressAnchor의 Pos Y 하나로 조정한다. 개별 원본 프리팹의 내부 Y는 상호작용 200·재장전 210으로 복원했고, 공통 프리팹의 인스턴스 오버라이드로 배치를 정규화했다.

`SceneBootStrap`은 공통 ProgressHUD를 한 번 생성하고 `SetSources()`로 상호작용·재장전·회복 컨트롤러를 전달한다. `ActionProgressUI.SetSources()`는 PlayerReloadController의 ReloadProgress/ReloadRemainingTime, PlayerMedicineUseController의 UseProgress/UseRemainingTime과 각 상태/진행 이벤트를 구독한다. 기존 ReloadProgressHUD 프리팹의 링·남은 시간·ActionText를 재사용하고 행동 문구는 `재장전 중`/`회복 중`으로 전환한다. 한 발씩 장전은 발마다 진행률과 남은 시간이 반복되며, 상호작용 중에는 링을 숨긴다. 행동 완료·취소 시 숨기고 소스 교체·비활성화 시 구독을 해제한다. 런타임 코드가 크기·위치를 덮어쓰지 않는다.

공통 행동 원은 `Layouts/Sprites/ReloadRing.png`와 `ReloadRingBackground.png`의 전체 링 Sprite를 사용한다. `FillBackground`의 Image는 Simple, 자식 `Fill`은 Filled/Radial360이며 `ActionProgressUI.progressFill`에 연결된다. 기존 `ReferenceCircle` RawImage는 정적 65% 샘플이므로 비활성화한다. DuckovUIReferenceBuilder도 재장전 레퍼런스에는 전체 링 Sprite를 저장한다.

상호작용 표시 위치는 대상의 직계 자식 `InteractionAnchor`가 있으면 그 위치를 사용하고, 없으면 Renderer 경계 중심 → Collider 경계 중심 → Transform 위치 순서로 구한다. 툴팁 간격은 InteractionPromptHUD 프리팹 내부 visualRoot의 `Master/InteractSelection` RectTransform에서 조정한다. 이전 tooltipOffset 필드와 실행 중 레이아웃 설정 코드는 제거했다.

## 이번 정리와 확인 범위

- 05:20 예약 작업으로 런타임 레이아웃 생성기를 제거하고 편집 가능한 화면 프리팹에 저장했다. 화면은 Inspector 참조로 바인딩한다.
- 동적으로 바뀌는 슬롯 수, 드래그/우클릭 메뉴의 포인터 위치, 상호작용 월드 위치 추적, 표시 여부·숫자·게이지 갱신은 런타임 동작으로 남겼다.
- Unity 에디터에서 저장된 참조, 버튼 콜백, Missing Script 및 글꼴을 확인했다. 플레이 테스트는 사용자가 진행한다.

사용자 확인 항목: ESC로 상세창 → 인벤토리 순서 닫기, 빈 스택에서 메뉴 열기, 아이템 이동/버리기·퀵슬롯 선택, 상호작용 표시 추적 및 팝업 중 숨김.
