# 오리 플레이어 복사본

관련 기능: ACT-01(플레이어 이동), ACT-02(조준), ACT-03(사격), ACT-06(회피), INV-02(장비).

## 실행

`DuckPlayerIntegration.unity`만 열고 Play한다. 다른 시험 씬과 함께 실행하면 입력·카메라·EventSystem·NavMesh가 중복될 수 있다. 원래 SampleScene과 PlayerRoot.prefab은 변경하지 않았다.

- WASD 이동, Shift 달리기, 우클릭 조준, 좌클릭 사격, 기존 회피·장전 키를 사용한다.
- 시작 무기는 자동으로 지급하지 않는다. 기존 튜토리얼 상자의 아이템을 획득해 장착한다.
- `DuckPlayerRoot.prefab`은 현재 SampleScene의 최신 플레이어를 복제한 독립 프리팹이다. 기존 인벤토리·의료품·보호 장비·드롭·무게·스태미너 컴포넌트를 보존한다.
- 다른 씬에 프리팹만 넣을 때는 CombatDataCompositionRoot, 카메라, 기존 인벤토리 UI 참조를 연결해야 한다. 프리팹은 외부 씬 오브젝트 참조를 자체적으로 저장할 수 없으므로 통합 씬에는 해당 연결을 씬 오버라이드로 보관했다.

## 구성

- 외형·클립·Avatar·메시·머티리얼·텍스처는 이 폴더에 복사했다. 프리팹의 Test 폴더 의존은 없다.
- MagicBlend, KRig, 외형 조립 코드와 시험 GUI는 연결하지 않는다.
- DuckPlayer.controller의 방향별 이동 Blend Tree는 기존 PlayerAnimationController의 MoveX/MoveY를 사용한다.
- DuckArms.mask는 Animator를 기준으로 하는 실제 팔 경로만 활성화한다. 조준·발사 중 하체 이동을 보존한다.
- DuckPlayerAnimationContext는 현재 무기 유무만 Animator에 전달한다. 빈손에서 조심스러운 걷기 입력을 눌러도 총 조준 자세를 만들지 않는다.
- WeaponMount는 보이는 Wings.R 아래에 있다. 실제 WeaponView.TryAttachTo로 그립 위치와 스케일을 보정한다. AK47.prefab 내부 외형의 180도 회전을 고려한 기준 회전을 사용한다. 다른 총의 원본 루트 축이 다르면 그 총의 외형 방향을 추가 검수한다.
- 기존 사격 예약을 유지하기 위해 복사한 조준 클립에 OnFireFrame 이벤트를 넣었다. 전용 총기 사격 클립을 제작한 것이 아니다.
- 구르기는 게임의 Dodge 상태로 진입·종료한다. 기존 CharacterController가 이동과 회피 시간을 결정하며 Root Motion은 꺼져 있다.
- 새 몸통·머리 HitBox와 AimPointProvider 참조를 연결했다. CharacterController의 기존 수치는 보존했다.

## 검증과 한계 — 2026-10-08

- 실제 Play 모드에서 AK-47 카탈로그/풀을 통한 장착, 그립 위치 일치, 총구 전방 정렬을 확인했다.
- 시험 중 입력 값을 주입해 실제 PlayerController의 위치 이동과 Animator 방향 매개변수 갱신을 확인했다. 실제 키보드 전체 플레이 검수와는 구분한다.
- 실제 PlayerAttackController → FireEventPose → PlayerAnimationEventRelay → 투사체 생성 경로로 탄약이 소진되는 것을 확인했다. 이전 모의 시험 담당자를 사용하지 않았다. 이번 통합에서는 표적 명중·피해까지 다시 판정하지 않았다.
- 기존 TryStartDodge가 Dodge 상태와 무적을 활성화하고 Animator Dodge 상태가 대응하는 것을 확인했다. 회피 종료 후 Free와 무적 해제를 확인했다.
- 여러 씬 동시 실행의 EventSystem/NavMesh 오류와 복사된 UI Slot Template의 Missing Script를 발견했다. Slot Template의 끊긴 컴포넌트는 복사 씬에서만 제거했다. 원본과 관련 없는 씬은 수정하지 않았다.
- 달리기는 걷기 1.6배 재생의 임시 표현이다. 전용 장전·사망·피격 애니메이션은 준비하지 않았다. 양손 그립, 다른 총의 축, 발 미끄러짐, 실제 UI 파밍 전체 회귀 검수는 남아 있다.
- 검사용 카메라와 주입한 입력은 플레이 종료로 제거했다. 원본 파일 SHA256이 작업 전후 동일함을 확인했다.

프리팹 제작 도구는 `Assets/Editor/DuckPlayerCopyBuilder.cs`다. 새 복사본을 생성하는 메뉴는 `Tools/ExtractionShooter/Player/Create Duck Player Copy`이며 기존 복사본은 덮어쓰지 않는다.
