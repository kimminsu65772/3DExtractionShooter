# 공통 아이템 사용 연결

관련 기능: UI-01(핵심 HUD), SURV-01(체력).

## 책임

- PlayerHeldItemController는 선택된 객체와 퀵슬롯의 유효성을 확인하고 PlayerItemUseController에 사용·취소를 요청한다. 회복약 타입을 알지 않는다.
- PlayerItemUseController는 같은 오브젝트의 IItemUseHandler 구현 컴포넌트를 Awake에서 수집한다. CanHandle이 맞는 첫 활성 담당자에게 TryUse를 요청한다. 해당 담당자가 사용 조건으로 거절하면 다른 담당자로 우회하지 않는다.
- PlayerMedicineUseController는 회복약 여부·체력·가방 소유·행동·UI 조건을 판단한다. 기존 사용 대기, 완료 시 소비·회복, 회피·사망·무기 교체·소유권 상실 시 취소 규칙을 유지한다.
- IItemUseHandler.IsUsing은 실제 사용 진행 상태를 제공한다. 공격·장전은 PlayerItemUseController.IsUsing을 읽으며 회복약을 직접 참조하지 않는다.

## 씬 연결

플레이어 루트에 PlayerItemUseController, PlayerMedicineUseController, PlayerHeldItemController, PlayerItemQuickslots를 함께 둔다. HeldItemController의 Item Use는 같은 루트의 공통 창구를 연결한다. 기존 Medicine Use 참조는 삭제되었으므로 새 창구를 연결해야 한다. 자동 검색은 플레이 시작 시 처리하며, 실행 중 새 담당자를 추가하는 기능은 현재 범위에 없다.

## 확장

새 음식·투척·버프 담당자가 IItemUseHandler를 구현하고 같은 루트에 붙으면 공통 창구가 검색한다. 담당자의 CanHandle 범위는 겹치지 않게 구성한다. 취소 시에는 효과와 수량을 완료 전에 적용하지 않도록 각 담당자가 책임진다.

## 검증

- 전체 런타임 및 인벤토리 Editor 코드의 별도 C# 컴파일: 오류 0, 경고 0.
- Tests/ItemUse의 실제 공통 창구 소스와 Unity 대역을 이용한 분기 검증: 12/12 통과.
- Unity의 실제 컴포넌트 생명주기, 회복 대기·수량 소비, 화면 입력은 대역 테스트의 검증 범위가 아니다. 실전 플레이 검증이 남아 있다.
- 숫자키·왼쪽 클릭 입력 연결은 이번 변경에 포함하지 않는다.
