# ENEMY-02(적 종류 확장): Stage1 스폰 연결

## 준비된 생성 단위

| 생성 정의 SO (`Assets/GameData/EnemySpawns/`) | 스폰 프리팹 | 동작 |
| --- | --- | --- |
| Enemy_Ranged.asset | GameCharacters/Spawning/Enemy_Ranged.prefab | 일반 정지 사격 FSM |
| Enemy_USEC.asset | GameCharacters/Spawning/Enemy_USEC.prefab | 이동 사격 + 피격 회피 |
| Squad_PatoChapo.asset | GameCharacters/Spawning/Squad_PatoChapo.prefab | 대장 1명 + Guard 3명, 분대 BT, 구르기 없음 |

프리팹 전체 경로의 접두사는 `Assets/Characters/`다. 세 루트는 비활성 상태다. 원본 게임용 프리팹과 저장 씬의 테스트 분대는 보존했다.

## 직접 연결할 순서

1. Project의 Create → ExtractionShooter → Enemy → Spawn Table에서 사용할 구역의 테이블을 만든다.
2. Entries에 생성 정의 SO·생성 확률·최소/최대 수량을 설정한다. 분대 수량 1은 캐릭터 1명이 아니라 완성된 4인 분대 1개다. 생성 확률 0은 생성하지 않으며 1은 반드시 해당 항목의 수량을 추첨한다.
3. Stage1 씬의 오브젝트에 StageEnemySpawner를 붙이고 Areas에 테이블과 스폰 포인트를 연결한다. 같은 구역의 포인트는 한 번씩만 사용한다. 분대 포인트는 대장의 위치이며 나머지 대원은 프리팹의 상대 위치에 배치된다.
4. 데이터 초기화와 플레이어 생성이 끝난 부트스트랩의 스테이지 분기에서 아래 API를 호출한다. 대상 변수 이름은 작성 중인 부트스트랩에 맞춘다.

```csharp
enemySpawner.SetDataRoot(dataRoot);
enemySpawner.SetTarget(playerRoot, targetViewPoint, aimPointProvider);
bool spawned = enemySpawner.TrySpawn();
if (!spawned)
    return false;
```

스포너 내부 순서는 `비활성 생성 → 데이터/감지 대상 주입 → Agent별 NavMesh 보정 → 전체 활성화 → 장비 준비 확인`이다. Brain.Awake가 감지 대상과 조준 정보를 읽으므로 활성화 후에 참조를 연결하지 않는다.

5. 새 스폰을 사용하기 전에 기존 Factory_SquadTest를 비활성화하거나 교체하여 분대가 중복 생성되지 않게 한다. 이번 작업은 기존 배치를 변경하지 않았다.

## 현재 제한과 확인할 점

- GroundZero 씬에 9개 Areas를 연결했다. 일반 Ranged 7구역은 직접 작성한 확률 1·2~3명, USEC 1구역은 0.3·1~2명, 공장 분대는 1·1분대다. 원본 전체 인원수를 그대로 적용한 것이 아니다.
- `StageSpawnPoints_Original` 아래 원본 월드 좌표 포인트를 복원했다. 빈 원본 EnemySpawner 오브젝트 자체의 위치를 생성 위치로 사용하지 않는다. 각 구역 테이블은 `Assets/GameData/EnemySpawns/GroundZeroAreas/`에 있다. 기존 통합 테이블은 보존했다.
- AK74 전용 단일 포인트 2곳은 복원만 하고 Areas에서는 제외했다. 현재 Ranged 2~3명 설정으로는 수용할 수 없으므로 별도 설정이 필요하다.
- 랜덤 무기·방어구·의약품 표는 아직 연결하지 않았다. 현재는 기존 프리팹의 초기 장비 ID를 사용한다. 소유·드롭은 기존 장비 인스턴스 경로를 유지한다.
- PatoChapo는 공통 오리와 골격이 다르다. 전용 Avatar와 걷기/대기/팔 뻗기 클립, 별도 게임용 Controller·상체 마스크를 사용한다. 실제 무기 파지와 사격·장전 전환은 직접 확인해야 한다. 새로운 전용 사격·장전·사망 모션을 만든 것은 아니다.
- 테이블의 생성 수량만큼 포인트가 있어야 한다. 구역의 가능한 최대 합계를 수용하도록 배치하는 것이 안전하다. 분대 대원들의 상대 위치까지 NavMesh 위에 있어야 한다.
- Stage1 진입 후 한 번 생성, 각 적의 감지·교전, Guard의 회피 부재와 USEC의 회피 유지, 처치 후 같은 잔탄·내구도 드롭을 직접 검증한다.
- 이번 검증은 컴파일과 프리팹 참조 검사다. 실제 플레이 검증 완료를 의미하지 않는다.
