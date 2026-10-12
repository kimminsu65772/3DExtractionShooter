# ENEMY-01(기본 행동): 오리 적 복사본

- `DuckEnemyRoot.prefab`: 기존 원거리 적의 FSM, 감지, NavMeshAgent, 공격, 무기, 체력, 전리품 기능에 오리 외형을 연결한 복사본이다.
- `DuckEnemyIntegration.unity`: 오리 플레이어 시험 씬을 복사하고 적 외형만 교체했다. 이 씬만 열고 플레이한다.
- `DuckEnemy.controller` / `DuckEnemy.overrideController`: 기존 적의 전환 틀을 복제하고 오리 Idle, WalkForward, Aim 클립을 연결한다.
- `Assets/Editor/DuckEnemyCopyBuilder.cs`: 원본을 덮어쓰지 않고 복사본을 생성하는 Editor 도구다.

## 연결 원리

AI는 기존 `EnemyRangedBrain`이 결정하고 실제 이동은 `EnemyMotor`가 실행한다. `EnemyAnimationController`는 실제 속도와 공격 상태를 읽는다. 실제 발사 성공 알림을 받은 뒤 Fire 상태를 재생하며, 애니메이션 이벤트로 다시 발사하지 않는다.

무기는 보이는 오른손 `Wings.R` 아래의 WeaponMount를 사용한다. 기존 `WeaponView.TryAttachTo`가 크기와 Grip 위치를 보정한다. 머리와 몸통 히트박스도 애니메이션 본을 따라간다. 시야 기준점은 본의 흔들림과 무관하게 루트에 남긴다.

외형과 클립은 `DuckPlayerCopy`의 정리된 자산을 공유한다. `Assets/Test`에 의존하지 않지만 `DuckPlayerCopy` 폴더는 필요하다.

## INV-02(장비) / WORLD-02(전리품): 적 보호 장비 연결

- `EnemyProtectionLoadout`은 적마다 별개의 방어구·헬멧 내구도를 보관하고 `IProtectionProvider`로 피격 부위별 장비를 제공한다.
- Initial Armor Id는 4001~4003, Initial Helmet Id는 5001~5003으로 1~3레벨을 선택한다. 기본값 0은 미장착이다. 플레이 전에 설정한다.
- Data Root를 비워두면 같은 오브젝트의 `EnemyWeaponLoadout`이 사용하는 데이터 공급자를 참조한다.
- `EnemyEquipmentLoadout`이 무기·별도 탄약·방어구·헬멧 목록을 모으고 `EnemyLootDropper`가 상자를 생성한다. 총의 잔탄과 보호 장비 내구도는 기존 런타임 객체를 전달해 보존한다.
- 이 연결은 보호 효과와 전리품을 담당한다. 보호 장비 외형 자동 부착은 포함하지 않는다.
- DuckEnemyRoot와 SampleScene, DuckPlayerIntegration, DuckEnemyIntegration의 기존 적을 연결했다. 적 생성 도구도 같은 연결을 구성한다.
- 플레이 모드에서 몸통·머리별 내구도 감소, 전리품의 동일 객체 보존, 중복 드롭 차단, 잘못된 ID의 부분 지급 방지와 재시도를 확인했다. 시험에 사용한 1레벨 ID 설정은 저장하지 않았다.
- 이번 단독 씬 실행에는 유효한 NavMesh가 없다는 오류가 있어 AI 이동 검증은 제외했다.

## 사용

시험 씬에는 데이터와 플레이어 참조가 연결되어 있다. 프리팹을 다른 씬에 넣으면 EnemyWeaponLoadout/EnemyAttackController의 Data Root와 EnemyPerception의 Player Root, Target View Point, Target Aim Point Provider를 연결한다. NavMesh가 준비된 위치에 배치한다.

## 확인한 범위와 남은 항목

- 플레이 모드에서 무기 준비, Grip·Mount 일치, 플레이어 발견 후 Attack/Aim 전환, 실제 공격 성공 시 탄약 14→13 차감을 확인했다.
- 실제 NavMesh 이동 속도 3.5와 MoveSpeed 전달을 확인했다. Fire 상태 진입도 확인했다.
- 체력에 치명적 피해를 적용했을 때 Dead 진입, 충돌체 즉시 해제, 전리품 컨테이너 증가, 적 제거와 전리품 잔존을 확인했다.
- 다른 시험 씬을 함께 로드한 첫 실행에는 중복 EventSystem, 다른 씬의 NavMesh/누락 스크립트 로그가 있었다. 런타임에서 다른 씬을 비활성화하여 적 연결을 검사했다. 전체 프로젝트의 오류 해소를 의미하지 않는다.
- 달리기는 원본과 같은 이동 클립 배속 방식으로 연결했다. 적의 Walk/Run 재생 배속은 0.8/1.4이며 오리 플레이어 Sprint도 1.4다. 실제 이동 속도는 바꾸지 않는다. Fire는 조준 자세를 사용한다. Reload/Dead는 기존 자리 표시자이므로 장전·사망 동작이 보이는 완성 애니메이션은 아니다. 사망 처리와 전리품 생성은 작동한다.
- 최종 손 접촉, 발 미끄러짐, 다른 무기 축과 크기, 시야 안팎 표시 전환은 인게임에서 확인한다.
