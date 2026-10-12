# 공통 캐릭터 / 원본 MagicBlend 시험

## 직접 재생 + 프로젝트 무기 장착 시험

현재 비교 대상은 `DirectClipWeaponTest.unity`다. 오른쪽의 `Idle / Walk / Aim animation` 버튼으로 세 Animator 상태를 직접 재생한다. MagicBlend·KRig·별도 PlayableGraph·총구 강제 회전은 없으며, `ProjectWeaponMountPreview`가 실제 `WeaponView.TryAttachTo()`를 호출한다. 기존 비교 씬은 보존했다.

2026-10-08 MCP 플레이 검사에서 Aim 상태 재생과 총구 전방 각도 0도, Walk 상태의 시간 진행, 두 상태의 그립/mount 일치 및 무기 크기 유지를 확인했다. 오류 로그는 없었다. 걷기는 총기용 팔 자세를 합성하지 않은 직접 클립이므로 총이 아래·뒤쪽으로 향한다. 이 상태의 자연스러움, 양손 접촉, 실제 사격은 완료 판정하지 않았다. 검사 후 백그라운드 실행 설정을 복원하고 Play를 종료했다.

생성 메뉴는 `Tools/Duck Animation Test/Build Direct Clip Weapon Test`다. 이미 존재하는 시험 씬은 덮어쓰지 않는다. Game 창은 Free Aspect 또는 창에 맞는 축소 배율로 표시해야 메뉴가 출력 영역 밖으로 잘리지 않는다.

## 보이는 손 기준 비교 시험

### 프로젝트 무기 장착 경로 적용

`Tools/Duck Animation Test/Use Project Weapon Mount`는 활성 시험 씬의 무기에 실제 `WeaponView`를 붙이고, `Wings.R/ProjectWeaponMount`와 `ProjectGrip`을 연결한다. 조준 클립의 손 회전을 샘플링해 캐릭터 전방을 향하도록 mount의 고정 로컬 회전을 계산한다. 매 프레임 세계 방향으로 강제 회전하지 않는다.

`ProjectWeaponMountPreview`는 카탈로그·리소스 풀 대신 시험 전용 대여 초기화를 수행하고 `WeaponView.TryAttachTo()`를 호출한다. 실제 게임의 크기 상쇄와 그립 위치 보정 코드를 재사용하지만, PlayerWeaponHolder 전체나 발사 입력을 연결한 시험은 아니다. mount 위치는 손 끝 쪽의 초기값이며 손잡이 접촉은 육안 미세 조정 대상이다.

2026-10-08 MCP 플레이 검증: 직접 Aim 출력은 LateUpdate에서 수동 평가한다. 재생 완료 자세에서 총구 방향 `(0, 0, 1)`, 캐릭터 전방과 각도 차이 0도, 그립/mount 오차 약 0.000031 유닛, 무기 월드 크기 `(1, 1, 1)`을 확인했다. 걷기로 전환한 뒤 직접 Aim 출력 제거, MoveSpeed 1, 그립 일치, 재부착 성공과 크기 유지도 확인했다. 콘솔 오류는 없었다. 검사 동안 Application.runInBackground를 켰다가 false로 복원하고 Play를 종료했다. 양손 접촉·모든 걷기 방향의 시각적 자연스러움·실제 투사체 발사는 이번 검증 범위가 아니다.

`VisibleHandMountTest.unity`는 별도 비교 씬이다. 원래 CommonCharacterAnimationTest는 그대로 남겼다.

- 손 외형이 붙은 Wings.R 아래에 VisibleHandWeaponHolder를 만들고 총을 연결한다.
- Holder는 위치 0이며 처음 총 축은 이전 소켓 방향을 참고한다. 그 뒤에는 Wings.R의 움직임을 그대로 따른다.
- 이전 시험의 (-0.04, -0.08, -0.28) 총 위치 오프셋은 사용하지 않는다.
- Grip_OriginalMountOrigin은 VPO 프리팹의 원래 장착 원점을 표시한다. 메시에서 손잡이를 새로 추정한 점이 아니므로 자연스러운 접촉은 육안 확인이 필요하다.
- 빌더는 편집기에서 대기·전방 걷기·우측 걷기·조준 클립의 12개 시점을 샘플링해 손 기준/Holder/Grip의 위치와 회전 일치를 검사하고 원래 자세를 복원한다. 이 검사는 런타임 MagicBlend나 시각적 접촉을 판정하지 않는다.
- 씬만 열고 Gun → Walk forward / Run forward를 비교한다. 방향이 마음에 들지 않으면 Holder의 로컬 회전, 접촉 위치는 로컬 위치를 조절한다. 손 외형이나 총 메시 자체를 옮기지 않는다.

관련 기능: ENEMY-01(기본 행동), ACT-03(사격) 외형 연결을 위한 별도 시험이다.

## 실행

1. 저장하지 않은 기존 씬은 원하는 방식으로 보존한다.
2. `Assets/Test/CommonCharacter/CommonCharacterAnimationTest.unity`만 열고 Play를 누른다.
3. `Gun` 상태에서 `Walk forward/backward/left/right`와 `Run forward`를 선택한다.
   `Aim animation`은 기존 시험의 `Armature_Aim` 클립을 직접 재생한다. 이동·손 상태·합성 자산 버튼을 누르면 직접 재생을 종료하고 기존 합성으로 돌아간다. 조준 클립 재생 버튼이며 투사체 발사나 게임의 ADS 입력을 실행하지 않는다.
4. `Empty`, `Melee`, `Bow`, `Carry`로 손 상태를 바꾸고 하단 합성 자산 버튼으로 자세를 비교한다.
5. 근접·활 공격은 `Attack trigger`로 요청한다. 총기 발사 애니메이션 클립을 추가한 것은 아니다.

걷기와 달리기는 제자리 재생이다. 이동 모터와 AI는 연결하지 않았다. 직접 상태 재생 버튼은 원본 전이에 의해 다른 상태로 넘어갈 수 있다. 합성 자산 수동 선택도 다음 Hand 상태 전이 시 원본 자산으로 갱신될 수 있다.

실제 투사체와 체력 차감 시험은 기존 `Assets/Test/SniperFiringTest.unity`다. 두 씬의 목적을 구분한다. 새 MagicBlend 씬에는 투사체 발사 담당자를 넣지 않았다.

## 가져온 것 / 제외한 것

- 원본 위치: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets`.
- 원본 `CustomCharacterAnimationController_MagicBlend`와 참조하는 AnimationClip·MagicBlend 자산·KRig·Avatar를 복사했다.
- MagicBlend Runtime 소스와 `KAnimationCore.Runtime.dll`만 연결했다. 원작 게임 Core DLL과 전체 Plugins는 가져오지 않았다.
- `CompleteCommonDuck.prefab`은 눈·입·머리 장식·날개·발이 이미 붙은 프리팹이다. 원작 외형 조립 스크립트와 `SniperHeadPreview`는 실행하지 않는다.
- 머리 모양은 편집 단계에서 고정했다. 복사한 클립에서 Head의 scale, HeadTip/HairSocket의 position·scale 커브를 제거해 외형 보정과의 충돌을 줄였다. 원본 추출 폴더는 수정하지 않았다.
- 원작 게임용 AnimationEvent는 복사본에서 제거했다. 총 외형은 이전 시험의 VPO 메시 복사본과 임시 URP 머티리얼을 사용한다. 원작 셰이더·데칼의 완전 복원은 아니다.
- MagicBlend 초기화 전 상태 콜백이 발생하면 합성 자산을 보관했다가 초기화 때 연결하도록 시험 복사본에 보호 처리를 추가했다.

## 확인한 범위 / 미확인 범위

- Unity 컴파일 후 오류 없는 상태에서 편집기 빌더가 씬과 프리팹을 생성했다.
- KRig 계층의 뼈 이름이 프리팹 안에서 유일하게 대응하는 것을 생성 과정에서 확인했다.
- 일반 걷기·조준·활·구르기 클립의 미대응 경로는 `Hand.IKTarget.L/R`다. 이 시험은 해당 IK 해결기를 복원하지 않는다. 양손 그립의 완성은 별도 확인 대상이다.
- 원본 컨트롤러의 `Die1`은 다른 구형 뼈대의 TPose 클립을 참조한다. 사망 모션 완료로 취급하지 않고 해당 버튼은 제외했다. 구형 `Duck_Attack_L2R`도 현재 뼈대와 다르다. 원본 컨트롤러 참조를 추적하기 위해 자산은 보존하되 보편적 사용 가능 목록으로 판정하지 않는다.
- 새 씬의 플레이 모드·총기 흔들림·상태별 자세는 아직 육안 검수하지 않았다. 이전 사격 씬의 성공 결과를 이 씬의 성공으로 간주하지 않는다.

## 원리

Animator는 방향별 이동을 만들고, MagicBlend는 지정된 뼈의 자세를 그 결과에 합성한다. 원본 `MagicBlend_Gun`은 오른팔 체인에 조준 자세를 적용한다. 따라서 무기를 들고 걷는 동작은 단일 클립만 선택하는 문제와 다르다.

시험 파일은 모두 `Assets/Test` 내부에 있다. 새 프리팹의 일부 메시·머티리얼은 기존 Test 폴더의 복사본을 참조한다. CommonCharacter 폴더만으로 독립 배포하는 패키지가 아니며 전체 Test 폴더 단위로 제거할 수 있다.

프리팹에는 시험 조작 담당자가 붙어 있다. 실제 플레이어·적 프리팹에 도입할 때는 CommonCharacterAnimationPreview를 제거하고 해당 게임의 애니메이션 담당자를 연결한다. 프리팹만 다른 씬에 넣으면 기존 씬의 카메라를 사용한다.

여러 시험 씬을 함께 열면 Unity의 활성 씬에 속한 CommonCharacterAnimationPreview 하나만 카메라와 조작 패널을 제어한다. 비교할 씬을 활성 씬으로 지정한 뒤 실행한다. 다른 시험 담당자가 서로의 카메라를 끄는 상황을 방지한다.

애니메이션 시험 패널은 화면 오른쪽에 표시한다. 함께 열린 AITest의 왼쪽 HUD와 겹치지 않게 분리했다. `Idle`과 그 아래의 `Aim animation`은 스크롤 밖의 상단 고정 버튼이며, 나머지 상태 목록을 내려도 숨지 않는다.

## 직접 클립 + 프로젝트 무기 장착 시험 — 2026-10-08

현재 사용할 씬은 `DirectClipWeaponTest.unity`다. 이전 MagicBlend 비교 씬과 구분한다.

- Play 후 오른쪽의 Idle, Walk forward/left/right/backward, Roll, Aim animation, Shot 버튼으로 조작한다. 걷기는 제자리 재생이며 이동 모터를 연결하지 않는다.
- MagicBlend와 KRig를 제거하고 Animator 단일 레이어에서 클립을 직접 재생한다. 구르기는 한 번 재생 후 Idle로 돌아간다.
- 실제 `WeaponView.OnRent`와 `TryAttachTo`를 사용한다. 보이는 오른손 Wings.R 아래의 ProjectWeaponMount에 부착하고 ProjectGrip을 겹친다. 총구 방향을 매 프레임 표적으로 강제 보정하지 않는다.
- `DirectClipShotPreview`는 고정 4개의 실제 Projectile을 재사용하고 WeaponInstance의 탄약을 차감한다. Animator가 조준 자세를 반영한 뒤 LateUpdate에서 총구 전방으로 발사한다. 전용 총기 사격 클립·절차적 반동·게임의 ADS 입력은 연결하지 않았다.
- 시험 지정 조준 거리는 15m다. 기본 피해 20에 effectiveRangeMultiplier 0.55가 적용되어 한 발당 11 피해다. 원작 밸런스 값이 아니라 연결 검증용 설정이다.
- 실제 플레이 모드에서 좌우·뒤 걷기 상태와 재생 시각 진행, 구르기 후 Idle 복귀를 확인했다. 조준 상태에서 3발 모두 표적에 명중해 HP 100→89→78→67, 잔탄 3→2→1→0을 확인했다. 빈 탄창의 추가 발사는 발생하지 않았으며 Reset target / ammo로 HP 100과 잔탄 3이 복구됐다.
- 실제 합성 Game 화면 캡처에서 모든 버튼 표시를 확인했다. 구르기 중간 자세 캡처는 일시정지 후 정규화 시각 0.4로 샘플링한 이미지이며, 전체 구르기 영상 검수를 대체하지 않는다.
- 손·그립 위치 오차는 약 0.000043m, 총 월드 스케일은 (1,1,1)이었다. 일반 걷기 클립에서 총이 아래로 향하는 자세는 남아 있다. 양손 그립·이동 중 사격·전체 모션 자연스러움은 완성 판정하지 않았다.
- 이번 실행에서 Console 오류는 없었다. 테스트 뒤 Play를 중지하고 임시 runInBackground 설정을 기존 false로 복구했다.

파일 책임: DirectClipWeaponPreview는 버튼·클립 선택·구르기 복귀, ProjectWeaponMountPreview는 실제 무기 부착 API 호출, DirectClipShotPreview는 탄환·탄약·피해 연결을 담당한다. Editor/DirectClipWeaponTestBuilder는 시험 씬과 컨트롤러를 구성한다. 기존 씬을 확장할 때는 Extend Direct Clip Weapon Test 메뉴를 사용하며 기존 상태를 중복 생성하지 않는다.
