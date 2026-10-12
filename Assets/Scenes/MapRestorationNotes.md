# 탐험 맵 복원 기록

관련 기능: WORLD-01(탐험 공간), ACT-01(이동), UI-01(핵심 HUD).

## 로컬 리소스 의존성

- 원본 추출 위치: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets`.
- 외부 리소스는 기존 Git 정책에 따라 `Assets/Third_Party_Assets`에 로컬 보관한다. 이 저장소의 씬과 머테리얼만으로 맵이 완전히 복원되는 것은 아니다.
- HiddenWarehouseRestored, GroundZeroRestored, DuckovImported의 GUID를 보존해야 한다. GroundZero는 HiddenWarehouseRestored의 일부 공유 리소스도 참조한다.
- MK Toon 패키지는 `Assets/Third_Party_Assets/MK`에 필요하다. 새 머테리얼은 패키지 셰이더 GUID를 참조한다.
- 원작 DLL은 가져오지 않았다. 원작 Missing Script와 일부 미해결 자산 참조는 남아 있다.

## HiddenWarehouse

- 지상 씬과 Main 씬 및 조명 데이터를 복원했다. 지하는 제외했다.
- 기존 편집 복사본 Level_HiddenWarehouse_Copy도 보존한다.

## GroundZero

- Level_GroundZero_1과 Main 및 조명 데이터를 복원했다. Cave는 제외했다.
- PlayerSpawnPoint에 Player_YellowDuck을 배치하고 카메라, 입력, 데이터 구성, FOV 및 인벤토리 UI의 씬 참조를 연결했다.
- 원작 레이어를 현재 프로젝트의 Ground/Obstacle/Ignore Raycast로 교정했다.
- BunkerDoorExit의 Trigger SphereCollider를 트리거로 수정했다. 다른 Missing 기반 이동 차단 영역까지 모두 수정한 것은 아니다.
- Terrain은 별도 URP Terrain/Lit 머테리얼을 사용한다. TerrainData와 칠하기 데이터는 보존했다.
- SodaPointLight 106개와 AtmosphereFX 루트 및 연기 자식 7개를 제거했다. 건물은 제거하지 않았다.
- SodaLit을 사용하던 MeshRenderer 4,915개를 MK Toon Simple/Cel로 교체했다. 원본 머테리얼은 보존하고 새 머테리얼 171개로 텍스처, 색상, 노멀, 발광 설정을 옮겼다. 원작 특수 셰이더 효과의 완전 복원은 아니다.

## 확인 범위

- 씬 로드, Terrain 텍스처 표시, 플레이어 입력 이동 및 Scene View의 MK Toon 표시를 확인했다.
- 전체 맵 충돌·사격·파밍·FOV·성능의 인게임 회귀 검증은 아직 완료하지 않았다.
- 적용 전 씬 백업과 상세 인계는 로컬 `.codex/backups` 및 `.codex/handoffs`에 보관한다. 테스트 캡처와 백업은 커밋하지 않는다.
