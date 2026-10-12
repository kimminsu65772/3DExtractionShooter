# 원작 캐릭터 애니메이션 재생 실험

## 최신 시험

원본 컨트롤러와 MagicBlend를 연결한 새 시험은 `CommonCharacter/README.md`를 따른다. 아래 기록은 이전 단일 클립·사격 시험의 결과이며 새 MagicBlend 시험의 완료 근거로 합산하지 않는다.

관련 기능: ENEMY-01(기본 행동)의 외형 재생 검증.

## 확인된 범위

- 원작 스나이퍼 프리팹의 뼈대와 복사한 클립을 사용한다.
- 새 추출본의 `CustomFacePreset_Boss_Sniper`와 파츠 ID 대응을 확인해 머리 장식, 눈, 눈썹, 입, 꼬리, 양쪽 발과 날개를 조립했다. 꼬리 0번과 눈썹 0번처럼 빈 파츠도 설정대로 유지한다.
- MagicBlend, 원작 게임 로직, KAnimationCore DLL은 실행하지 않는다.
- 대기 클립 샘플링 시 12개, 걷기 클립 샘플링 시 18개 뼈 회전이 변경됐다.
- 조준 클립은 비교 시점 간 회전 변화가 없다. 정상 조준 완료 판정은 하지 않았다.
- 누락된 커브 경로는 `Armature/Root/Hand.IKTarget.L`, `Armature/Root/Hand.IKTarget.R`이다.
- `SniperPreview.prefab`은 조립한 테스트 모델이다. 입은 원작 데칼 대신 아틀라스의 해당 영역을 표시하는 임시 Quad로 대체했다.
- 원작 추출 셰이더 대신 테스트 복사본에만 URP Lit 임시 머티리얼을 적용했다.
- Unity 플레이 모드에서 Idle/Walk/Aim 자동 전환을 실행했다. 각 Animator 상태가 일치했고 각 전환 뒤 뼈 회전 변경을 확인했다. 재생 중 Console 오류는 없었다.
- 조준 상태 재생 확인은 총기 그립·양손 IK까지 완성됐다는 뜻이 아니다. 무기를 들고 조준하는 최종 자세는 별도 작업이다.
- 원작과 완전히 동일한 셰이더·데칼·외형이라는 판정은 하지 않았다. 머리 비율, 파츠 배치와 조준 자세는 육안 검수가 필요하다.

## 사용

1. `DuckAnimationTest.unity`를 연다. 기존 씬의 저장하지 않은 변경은 먼저 원하는 방식으로 보존한다.
2. 편집 모드에서는 `Tools > Duck Animation Test > Start Walk Preview`로 걷기 뼈대 재생을 확인한다.
3. `Stop Preview`로 멈춘다. 편집 모드 미리보기는 씬을 자동 저장하지 않는다.
4. 이 테스트 씬만 연 상태에서 Play를 누르면 Idle/Walk/Aim이 5초마다 전환된다. 화면 버튼으로 선택할 수도 있다.

## 구성과 정리

- 테스트 전용 C# 스크립트, 씬, 컨트롤러, 캐릭터 프리팹, 메시/아바타/클립 복사본, 임시 머티리얼, 캡처는 `Assets/Test`에 있다.
- 기존 씬과 원본 프리팹/클립을 수정하지 않는다. AITest의 저장하지 않은 변경 때문에 별도 씬을 추가로 열었다.
- 테스트를 끝내고 미리보기를 멈춘 뒤 이 씬을 닫으면 `Assets/Test` 폴더를 통째로 삭제할 수 있다.
- 일부 사용하지 않는 원작 직렬화 참조가 복사한 계층에 남을 수 있으므로 독립 배포용 패키지로 취급하지 않는다.
- `Tools~`의 읽기 전용 분석 도구도 테스트 폴더에 포함한다. 원작 게임 DLL을 프로젝트의 플러그인으로 추가하지 않았다.

## 핵심 원리

- 프리셋의 파츠 ID를 실제 프리팹으로 해석하고 해당 뼈의 소켓에 자식으로 배치한다. 뼈가 움직이면 파츠도 계층을 통해 함께 움직인다.
- 원작 클립을 복사하고 Animation Event를 제거한 뒤 테스트 Animator Controller에서 직접 재생한다. 원작 게임 로직의 호출 없이 클립과 뼈대의 호환성을 검사한다.
- 애니메이션이 머리 Transform을 덮어쓴 뒤 `SniperHeadPreview.LateUpdate`에서 외형 설정을 적용한다. 이 순서는 시험용 외형 보정이며 실제 게임 도입 시 애니메이션과의 충돌을 다시 검토한다.

## 실제 투사체 사격 실험 — 2026-10-08

관련 기능: ENEMY-01(기본 행동), ACT-03(사격).

- `SniperFiringTest.unity`를 열어 Play하면 2초 뒤부터 1.2초 간격으로 세 발을 발사한다.
- 원작 적 설정의 무기 ID 437은 `Sniper_VPO_Deng`이다. 외형은 `IG_Gun_VPO_Deng`의 테스트 복사본을 오른손 소켓에 붙였다.
- `SniperFiringPreview`가 우리 `WeaponInstance`/`WeaponAmmoState`, `Projectile`, `HealthComponent`를 사용한다. 피해를 직접 주는 모의 사격이 아니라 실제 SphereCast 충돌 경로를 거친다.
- 플레이 모드에서 세 발이 각각 20 피해를 주고 표적 체력이 100→80→60→40으로 줄었으며 잔탄이 3→2→1→0으로 줄었다. 마지막 상태는 `animatorAim=True`였다. 해당 실행에서 Console 오류는 없었다.
- 고정 4개 탄환을 미리 생성해 비활성 객체를 재사용한다. 이번 연결 검증은 Addressables/KeyedPoolService/EnemyAttackController를 거치지 않는다. 실제 적에 도입할 때는 기존 EnemyAttackController와 ProjectileSpawnService의 발사 성공 알림에 외형 반동을 연결한다.
- 무기 ID와 외형은 원본 연결을 확인했지만 피해 20, 탄속 20, 용량 3 및 Rifle 탄약 분류는 명시적인 시험값이다. 원작 SNP 탄종과 밸런스를 우리 CSV에 정식으로 등록한 것이 아니다.
- 원작 `CharacterAnimationControl.OnAttack`은 근접 무기만 공격 레이어를 실행한다. `ItemAgent_Gun.StartVisualRecoil/UpdateVisualRecoil`은 총을 뒤로 이동시키고 복귀시키는 방식이다. 시험 드라이버에서도 별도 총기 사격 클립을 만들지 않고 조준 자세와 절차적 총기 후퇴를 조합한다. 원작 MagicBlend와 전체 IK를 복원한 것은 아니다.
- 현재 표적을 향해 총구 방향을 맞추는 시험 보정을 사용한다. 양손 접촉 정확도·여러 각도의 조준·이동 중 사격은 아직 합격 판정하지 않았다. 원작 머티리얼 대신 시험용 단색 머티리얼을 사용한다.
- `Fire one shot`은 수동 발사를 요청한다. 자동 발사를 끄려면 Inspector의 Auto Fire를 끈다. `Reset three shots`는 비행 중 탄환이 없을 때 표적과 탄창을 초기화한다. `Walk pose/Aim pose`는 자세를 전환하며 Walk에서는 발사를 차단한다.
- 기존 AITest 및 외형 시험 씬을 보존하기 위해 새 씬을 추가로 열었다. 사격 시험은 (100,0,100) 근처에 배치하며 다른 카메라는 플레이 중에만 끄고 종료 시 복구한다. 반복 실행에는 사격 시험 씬만 여는 편이 명확하다.

### 준비 도구와 런타임의 책임

- `Editor/SniperFiringTestBuilder.cs`: 모델·총의 복사본, 임시 머티리얼, 표적, 실제 Projectile 프리팹, 카메라·조명을 구성하고 별도 씬을 저장한다. 기존 시험 씬을 덮어쓰지 않는다.
- `SniperFiringPreview.cs`: 조준 자세 재생, 발사 요청, 준비된 탄환 초기화, 실제 탄약 소비, 총기 반동, 결과 표시를 수행한다. 시험 제어 코드이며 전체 적 AI를 대체하지 않는다.
- `FiringProjectile.prefab`: 프로젝트의 실제 Projectile 컴포넌트와 가시화용 구체로 구성한다. 원작 투사체 DLL은 사용하지 않는다.
- `SniperFiringPreview.prefab`의 표적·카메라는 씬 참조이므로 프리팹만 다른 씬에 배치하면 다시 연결해야 한다. 바로 실행할 수 있는 결과물은 `SniperFiringTest.unity`다.

실제 프로젝트 적용 전에는 외형 검수 → 적의 기존 장비/발사 성공 이벤트 연결 → 이동·조준·발사 취소 검증 → Addressables 등록 순으로 진행한다. 이 실험이 성공했다는 이유만으로 실제 AI 씬의 적용까지 완료했다고 판단하지 않는다.
