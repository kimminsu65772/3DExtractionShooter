# CSV 무기 외형

관련 기능: INV-02(장비), ACT-03(사격), CORE-02(리소스 로딩 및 수명 관리).

| CSV ID / 이름 | 원본 프리팹 | 게임용 프리팹 | 새 Addressables 주소 |
| --- | --- | --- | --- |
| 1001 AK-47 | IG_Gun_AK47 | Prefabs/AK47.prefab | duckov/weapons/1001 |
| 1002 Glick | IG_Gun_GLick | Prefabs/GLick.prefab | duckov/weapons/1002 |
| 1003 MP-155 | IG_Gun_MP155 | Prefabs/MP155.prefab | duckov/weapons/1003 |
| 1004 MF | IG_Gun_MF | Prefabs/MF.prefab | duckov/weapons/1004 |

원본: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets`.
Source에는 원본 시각 자산과 누락된 의존성만 복사했다. 기존 프로젝트에 같은 GUID가 있는 자산은 재사용한다.
실제 장착에는 Prefabs의 정리된 프리팹을 사용한다. 원작 스크립트·탄피 파티클·물리 판정은 제거하고 URP 머티리얼과 우리 WeaponView를 연결했다.

원작 외형 전체를 회전해 총구의 전방을 루트 +Z로 통일했다. 원작 루트 원점을 Grip으로 두며 필요한 손잡이 미세 조정은 이 Grip의 위치에서 한다. WeaponView는 홀더 부모 배율을 상쇄하고 홀더와 Grip 사이의 차이만큼 무기 전체를 이동한다.

기존 weapons.csv와 AK47/MP155 프리팹 및 기존 주소는 변경하지 않았다. 샘플은 CSV의 ID·수치를 읽고 위 새 외형 주소를 별도로 연결한다. 실제 게임 전환 시 원하는 행의 weaponViewAddress를 위 주소로 설정한다.

## 비교 씬

`Assets/Test/WeaponShowcase/WeaponShowcase.unity`를 단독으로 열고 Play한다.
Idle / Run / Aim / Shot 네 노란 오리 플레이어 외형을 동시에 표시한다. 상단 네 무기 버튼은 모든 구역의 무기를 함께 바꾼다. Repeat shot, Shot once, Front view, Side view를 제공한다.

- 게임용 Player_YellowDuck의 복사본에서 입력·인벤토리·FSM 컴포넌트를 제거한 외형 비교 환경이다. 원본 플레이어 프리팹은 변경하지 않았다.
- 기존 DuckPlayer Controller의 Sprint와 Aim을 사용한다. Run은 WalkForward 배속 1.4의 제자리 재생이다.
- Shot은 기존 FireEventPose(조준 클립 기반)와 OnFireFrame 이벤트를 사용한다. 전용 반동·장전 모션을 추가한 것은 아니다.
- 우리 KeyedPoolService, WeaponView.TryAttachTo, ProjectileSpawnService.TrySpawnShot, WeaponAmmoState를 사용한다. 산탄은 CSV의 산탄 개수와 각도를 사용하고 한 번의 발사에 한 발만 소비한다.
- 총구를 표적 방향으로 강제로 꺾지 않는다. 실제 총구 전방으로 발사한다. 반복 외형 시험을 위해 빈 탄창은 즉시 보충하며 실제 인벤토리 재장전 시험과 구분한다.
- 종료 시 초기화 취소를 기다리고 풀과 Addressables 핸들을 해제한다.

생성 메뉴: `Tools/ExtractionShooter/Weapons/Build CSV Weapon Showcase`. 기존 결과를 덮어쓰지 않는다.

## 확인 결과 (2026-10-09)

Unity Play 모드에서 네 무기의 네 구역 장착·교체 및 발사 수 증가를 확인했다. Run 구역의 Sprint 상태와 Aim 구역의 Aim 상태 진입을 확인했다. MP-155 그랩과 홀더의 위치 오차는 약 0.00000019m였고 조준 총구 전방은 +Z였다. 정리된 무기 네 프리팹의 누락 스크립트·메시·머티리얼·셰이더 참조를 확인했다.

전체 모션 구간의 손·그립 관통, 무기별 크기와 왼손 접촉은 최종 수동 검수 대상이다. 전용 권총 자세와 반동·장전 클립은 이번 결과에 포함하지 않는다.
