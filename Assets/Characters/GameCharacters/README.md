# 게임용 오리 캐릭터 프리팹

- `Enemy_Default.prefab`: Duck_Default 외형과 기존 EnemyRangedBrain FSM, 이동·감지·공격·보호 장비·전리품 기능을 조합한다. 회피 컴포넌트는 부착하지 않는다.
- `Player_YellowDuck.prefab`: Duck_YellowDuck 외형과 기존 플레이어 이동·전투·인벤토리·장비 기능을 조합한다.

외형 후보와 기존 DuckPlayerRoot/DuckEnemyRoot 프리팹은 변경하지 않는다. 기존 씬의 캐릭터를 자동으로 교체하지 않는다.

새 모델에는 기존 Animator Controller, 무기 장착점, 머리·몸통 히트박스와 조준점을 연결한다. 표시 제어 Renderer 목록은 새 외형 기준으로 구성한다. 게임 규칙과 장비 수치는 변경하지 않는다.

프리팹을 씬에 배치한 뒤 카메라·CombatDataCompositionRoot·플레이어 감지 대상 등 씬 외부 참조를 연결한다. 일반 적이 이동할 위치에는 NavMesh가 필요하다.

Unity Editor에서 두 프리팹의 Animator, 히트박스 2개, 내부 무기 장착점 연결, FSM/플레이어 구성, Missing Script 없음과 Assets/Test 의존 없음 및 컴파일 오류 없음을 확인했다. 이번 외형 교체의 이동·사격·손 그립 인게임 검증은 아직 수행하지 않았다.

생성 도구는 `Assets/Editor/DuckGameplayPrefabBuilder.cs`이며 메뉴는 `Tools > ExtractionShooter > Characters > Create Default Enemy and Yellow Player`다. 기존 생성 결과가 있으면 덮어쓰지 않는다.
