# ENEMY-01(기본 적 행동): 적 모델 애니메이션 연결

## 연결할 것

1. 기존 EnemyRangedBrain·EnemyMotor·EnemyAttackController가 붙은 적 루트에 `EnemyAnimationController`를 추가한다.
2. 루트 아래에 실제 캐릭터 모델을 배치한다. 모델의 `Animator`와 Avatar는 해당 모델의 것을 유지한다.
3. `Assets/Animations/Enemy/EnemyAnimationSet.overrideController`를 모델별로 복제하고 이름을 바꾼다. 같은 모델의 적은 같은 Override를 공유할 수 있다.
4. 복제한 Override의 7개 자리에 실제 클립을 넣는다.
5. EnemyAnimationController의 `Animator`에 자식 모델의 Animator, `Animation Set`에 모델별 Override를 연결한다. Brain·Motor·Attack Controller는 같은 루트에서 자동으로 찾는다.
6. `Run Animation Reference Speed`는 달리기 클립에 대응할 실제 이동 속도로 설정한다. 기본값은 3.5다. 속도 / 기준 속도가 MoveSpeed이며, 0은 Idle, 0.5는 Walk, 1은 Run이다.

Override는 상태와 화살표를 복사하는 것이 아니라 공통 Animator가 사용하는 클립만 바꾼다. 서로 다른 모델마다 다른 Controller를 다시 만들 필요가 없다. 한 모델에 Animator가 여러 개면 자동 검색에 의존하지 말고 사용할 Animator를 직접 연결한다.

## 클립 교체 자리

| 교체 자리 | 사용할 동작 | Loop Time |
| --- | --- | --- |
| Enemy_Idle | 서서 대기 | 켠다 |
| Enemy_Walk | 걷기 | 켠다 |
| Enemy_Run | 달리기 | 켠다 |
| Enemy_Aim | 총을 겨눈 채 대기 | 켠다 |
| Enemy_Fire | 발사 | 끈다 |
| Enemy_Reload | 장전 | 끈다 |
| Enemy_Dead | 사망 | 끈다 |

제공한 .anim 파일은 **교체 자리용 빈 동작**이다. 상태 전환은 되지만 실제 자세는 바뀌지 않는다. 걷기 클립이 없으면 Walk에도 Run을 넣는 임시 설정은 가능하다. 다만 두 자리의 역할 자체는 유지한다.

Humanoid는 유효한 Avatar와 리타게팅 가능한 클립을 사용한다. Generic은 모델의 뼈대 이름·계층·클립 경로가 맞아야 한다. Override가 서로 다른 Generic 뼈대를 자동 변환하지는 않는다. 현재 Cat 모델의 Rig 오류를 이 컴포넌트가 고쳐주지는 않는다.

## 누가 무엇을 결정하는가

- EnemyRangedBrain/FSM: 조사·추적·공격·사망을 결정한다.
- EnemyMotor/NavMeshAgent: 실제 위치와 회전을 움직이고 현재 수평 속도를 제공한다.
- EnemyAttackController: 발사 간격·점사·탄약 소비·장전 완료를 결정한다. 성공한 발사에 OnFired, 각 장전 시작에 OnReloadStepStarted를 보낸다.
- EnemyAnimationController: 위 결과를 읽고 Animator 값만 설정한다. 총알을 생성하거나 탄약을 소비하지 않는다.
- EnemyAnimator: 전달받은 값에 따라 Locomotion·Aim·Fire·Reload·Dead 사이를 바꾼다. 이동 상태 안에서는 Idle·Walk·Run을 속도에 맞춰 섞는다.
- EnemyDeathAnimationBehaviour: Dead의 실제 교체 클립이 끝나면 적 루트에 완료를 알린다.

실제 발사를 Animation Event에 연결하지 않는다. 플레이어용 클립을 가져올 때 `OnFireFrame`처럼 플레이어에게만 있는 이벤트는 제거한다. 적에게 같은 이벤트를 붙이면 기존 코드 발사와 중복될 수 있다.

## 장전과 사망

장전 재생 속도는 `실제 클립 길이 / 무기의 reloadDuration`이다. 2초 클립을 1초에 끝내려면 2배로 재생한다. Animator 자체 Speed는 1로 둔다. 샷건에는 한 발을 넣는 클립을 연결한다. 기존 적 장전 코드는 한 발마다 다음 장전 시작 알림을 보낸다.

사망 즉시 이동·장전을 멈추고 루트 아래 Collider를 끈다. HitPartProvider는 사망 시 본 오브젝트를 끄지 않고 충돌체만 끈다. 덕분에 히트박스가 가슴·머리 본에 붙어 있어도 사망 모션이 유지된다. Dead 클립이 끝나면 적 루트를 Destroy한다. 화면 밖에서도 완료를 처리하며, Animator가 없거나 잘못 연결되면 기존 즉시 제거를 사용한다. 종료 알림이 누락되면 클립 길이와 Death Completion Timeout 중 충분한 대기 시간 이후 제거한다.

**전리품 상자 생성과 사망 파티클 재생은 이번 작업에 포함하지 않았다.** 전리품은 나중에 사망 시작 시 독립 오브젝트로 생성해야 한다. 죽은 모델의 자식으로만 두면 적을 제거할 때 전리품까지 사라진다.

## 연결 후 플레이 검증

풀에서 빌린 총의 충돌체도 사망 동안에는 끈다. EnemyWeaponLoadout은 원래 켜져 있던 충돌체를 기억해 풀 반환 직전에 복구한다. 원래 꺼져 있던 충돌체는 켜지 않는다. 사망 연출의 설정이 다음에 같은 총 외형을 빌린 플레이어·적에게 남지 않도록 하기 위해서다.

1. Idle에서 대기하고 조사·추적 이동 시 걷기/달리기 자세로 바뀌는지 확인한다.
2. 조사 위치에 도착하면 실제 정지에 따라 대기 자세로 돌아가는지 확인한다.
3. Attack 진입 시 총을 겨누고 성공한 발사에만 Fire가 나오는지 확인한다. 점사 휴식에는 Aim을 유지한다.
4. 탄창 소진 시 Reload가 무기의 장전 시간에 맞고, 샷건은 한 발마다 반복하는지 확인한다.
5. 장전 도중 죽으면 장전·발사가 중단되고 Collider가 해제되는지 확인한다.
6. 사망 클립을 끝까지 보여준 뒤 모델을 제거하는지 확인한다. 화면 밖 사망도 확인한다.
7. 실제 본 자세와 총구 위치, 이동 발 미끄러짐은 모델별 클립을 연결한 뒤 조정한다. 애니메이션이 위치를 중복으로 움직이지 않도록 Apply Root Motion은 코드에서 끈다.

기존 실전 씬과 플레이어 Animator는 수정하지 않았다. 클립을 준비한 뒤 사용자가 모델과 컴포넌트를 연결한다. 상태/Override 검증은 별도 Unity 프로젝트에서 수행하며 실제 모델의 플레이 검증을 대신하지 않는다.

2026-10-04 검증 결과: 런타임 및 생성 도구 C# 컴파일 오류 0개·경고 0개, 별도 Unity의 Animator 전환·히트박스 검사 25/25 통과. EnemyWeaponLoadout의 충돌 복구는 코드 검토와 컴파일로 확인했으며 실제 풀 반환은 인게임에서 추가 확인한다.

## 추가 Override 만들기

Unity 메뉴 `Tools > ExtractionShooter > Enemy > Create Animation Override`로 새 모델용 Override를 만든다. `Create Common Animator`는 기존 공통 Controller가 있으면 덮어쓰지 않는다. 이미 만들어진 Controller에 사용자가 변경한 전환을 보존하기 위해서다.
