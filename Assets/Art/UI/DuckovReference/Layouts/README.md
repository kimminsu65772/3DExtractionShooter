# UI 화면 구성 레퍼런스

관련 기능: UI-01(핵심 HUD), INV-01(인벤토리), WORLD-02(전리품·상호작용).

이미지만 모은 폴더가 아니라 원본의 RectTransform, 그래픽, 레이아웃 그룹, 스크롤뷰 및 슬롯 템플릿을 추출한 표시 전용 프리팹이다. 현재 프로젝트의 UI나 데이터에는 연결하지 않았다.

## 확인 위치

- 씬: `Assets/Test/UIReference/Duckov_UIReference.unity`
- 프리팹: 이 폴더의 `Prefabs/`
- 정적 미리보기: `Previews/`
- 원본 경로·변환 정보: `source-manifest.json`
- Missing Script와 원본 버튼 콜백 검사: `validation.json`

씬의 `Reference Canvas` 아래에 대상별 오브젝트가 있다. 기본으로 HUD 비교 배치가 보인다. 인벤토리나 창고를 볼 때는 다른 비교 오브젝트를 끄고 `Inventory_Reference` 또는 `Warehouse_Reference`를 켠다. 플레이 모드 없이도 Scene 뷰에서 UI 배치를 확인할 수 있다. 실제 데이터 조작용 씬이 아니므로 EventSystem과 인게임 참조는 추가하지 않았다.

## 구성

| 대상 | 프리팹 | 원본 표시 계층 |
| --- | --- | --- |
| 플레이어 체력 | PlayerHealth_Reference | HUDCanvas/HP |
| 플레이어 스태미너 | PlayerStamina_Reference | HUDCanvas/SmoothFollowCharacterHUD/Stamina |
| 인벤토리 | Inventory_Reference | GameplayUICanvas/Tabs/ViewArea/InventoryView |
| 창고/컨테이너 | Warehouse_Reference | GameplayUICanvas/Tabs/ViewArea/LootView |
| 소모품 퀵슬롯 3~8 | QuickSlots_Reference | HUDCanvas/ItemShortCut |
| 무기 칸 1·2 및 원본 V 버튼 | WeaponSlots_Reference | HUDCanvas/WeaponButtons |
| 장전 등 원형 작업 진행 | ReloadProgress_Reference | HUDCanvas/AimMarker/NormalPanel/AimMarker/ActionProgress |
| 상호작용 진행·취소 안내 | InteractionProgress_Reference | HUDCanvas/ActionProgress_Slider |
| 상호작용 대상 선택·F키 안내 | InteractionPrompt_Reference | HUDCanvas/MouseKeyboardOnly/Interact_MouseKeyboard |
| 유닛 머리 위 체력·이름 | UnitHealthBar_Reference | 독립 HealthBar.prefab |
| 인벤토리 칸 템플릿 | InventorySlot_Reference | 독립 InventoryEntry.prefab |

원본의 창고는 `LootView`가 `PlayerStorage.Inventory`를 대상으로 표시하는 구조다. `StorageDock`은 입고 대기 목록이므로 창고 본체로 사용하지 않는다. 원형 진행 UI도 장전만을 위한 전용 시스템이 아니라 공용 작업 진행 UI다.

## 원본 대비 차이와 한계

- 원본의 게임 로직, DLL, 타사 그래픽·트윈·로컬라이징 스크립트와 영구 버튼 콜백은 제외했다. 현재 uGUI/TMP 컴포넌트로 참조를 다시 연결했다.
- ProceduralImage는 일반 Image, 네 변의 테두리 이미지 또는 표시용 RawImage/정적 둥근 그래픽 텍스처로 바꿨다. 그림자·글로우·동적 테두리 연출까지 원본과 같다는 의미는 아니다. `ReferenceCircle`은 정적인 샘플이므로 실제 진행률에 따라 자동 갱신되지 않는다.
- 원본 중국어 글꼴 대신 기존 `Jua-Loading SDF`를 공유한다. 일부 문구·숫자는 샘플로 바꿨으므로 글꼴과 글자 폭도 원본과 다르다.
- 원본에서 실행 중 생성되는 인벤토리 칸에는 샘플 빈 칸을 채웠다. 샘플 그리드는 4열이며 창고 대상 목록에는 64칸을 생성한다. 실제 창고 데이터와는 무관하다. 원본 항목 템플릿은 삭제하지 않고 비활성 상태로 남긴다. 원본 실행 중 레이아웃 코드가 없으면 스크롤뷰 높이가 0이 되는 부분은 표시용 LayoutElement로 보정했다.
- 샘플에서는 상세 정보 창, 입고 대기 목록, 반려동물 목록, 무게 바, 임시 로딩 표시 및 아이템 디버그 아이콘을 표시하지 않는다. 필요한 원본 자식 계층은 프리팹 안에서 확인할 수 있다.
- 드래그·장착·사용·창고 이동·장전 진행·유닛 추적 등 실제 동작을 복원한 UI가 아니다. 다음 UI 교체 단계에서 우리 이벤트와 런타임 아이템을 연결해야 한다.

uGUI 스킬의 범위에 따라 기존 화면을 교체하지 않고 별도 레퍼런스로 분리했다. 표시 확인용 씬은 Build Settings에 추가하지 않았다.

## 제작 도구

`Assets/Editor/DuckovUIReferenceBuilder.cs`는 이번 표시 변환·샘플 구성·미리보기 촬영을 위한 에디터 전용 도구다. 플레이어 빌드에 포함되는 게임 로직은 추가하지 않는다. 원본 계층 분석과 추출 절차는 `.codex/tools/extract-duckov-ui-references.cjs`에 있다.
