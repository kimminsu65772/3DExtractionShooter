# ENEMY-02(적 종류 확장)·WORLD-02(전리품·상호작용): 사망 전리품

## 연결

| 대상 | 게임용 프리팹 |
| --- | --- |
| 일반 적·독립 USEC·분대원 3명 | `Assets/Prefabs/EnemyLoot/LootBox_EnemyDie_Template.prefab` |
| 꼬마덕 분대장 | `Assets/Prefabs/EnemyLoot/LootBox_EnemyDie_Box_GroundZeroBoss.prefab` |

기존 `Enemy_Default`, `Enemy_USEC`와 Spawning 폴더의 원거리 적·USEC·꼬마덕 분대, 총 5개 프리팹의 8개 Dropper를 연결했다. 현재 열린 GroundZero의 기존 테스트 분대 중 별도 연결이 남은 분대장과 C 대원도 Undo 가능한 변경으로 연결했다. 씬은 작업 시작 전부터 미저장 상태였으므로 저장하지 않았다. 프리팹 자산은 저장했다.

## 생성과 물리 책임

- `EnemyLootDropper.TryDropLoot()`는 몸통 HitBox의 Transform 위치에서 한 번 생성한다. 오래된 미연결 적만 발 위치보다 0.6m 위를 사용한다.
- 비활성 프리팹을 생성하고 기존 무기·보호구 인스턴스로 내용물을 구성한 뒤 활성화한다. 기본 보관 칸은 8개다.
- 원본 루트 BoxCollider의 **Is Trigger, Center, Size를 보존**했다. 상호작용 범위이며 바닥 충돌을 담당하지 않는다.
- 자식 `PhysicsCollider`의 일반 BoxCollider가 실제 외형 크기로 착지를 담당한다. 루트 Rigidbody는 Use Gravity 켜짐, Is Kinematic 꺼짐, Continuous Dynamic, Interpolate다.
- 비활성 프리팹 자산에서는 `Collider.attachedRigidbody`가 null일 수 있어, 구성 검사는 `GetComponentInParent<Rigidbody>(true)`로 한다.
- 바닥 Raycast와 Ground Mask·검사 거리·PlaceOnGround 경고를 제거했다. 실제 바닥에는 일반 Collider가 필요하다.
- 상자와 자식은 Interaction 레이어다. 적 프리팹과 그 자식은 Enemy 레이어다.

## 외형 복원

원본 추출본의 파일과 현재 공유 임포트 자산은 읽기만 했다. 상자는 `Assets/Prefabs/EnemyLoot`, 새 머티리얼은 `Assets/Materials/EnemyLoot`에 만들었다. 더미 SodaCraft 셰이더를 그대로 사용하지 않고 프로젝트의 실제 MKToon URP 셰이더로 텍스처·색·표면 수치를 대응했다. 보스 상자의 주황색 테두리는 Rim 효과로, 불투명 흰 구체처럼 보이던 SodaPointLight는 메시 표시를 끄고 원본 HDR 색의 URP Point Light로 대응했다. 원본 커스텀 광원 감쇠까지 동일한 복원은 아니다.

용병 캐릭터도 함께 수정했다. 기존 고정 외형 제작기가 파츠 전체에 같은 색을 적용하면서 군화·장갑 텍스처와 입의 검은 재질 슬롯, 눈 흰자 구분을 잃고 있었다. 원본 `CustomFacePart.customColorRenderers`와 `customColorKey`, Usec/Solider 프리셋을 읽어 색을 적용할 렌더러를 구분했다. 복원한 재질 18개는 `Assets/Materials/EnemyCharacters`에 있고, USEC·호위 외형 및 게임용 USEC·분대 프리팹에 연결했다. 몸통의 거친 표면 수치도 원본 Skin 값으로 연결했다.

꼬마덕의 기존 복원 셰이더에는 원본 `_AlphaMaskTint`를 추가해 부리·발 영역의 보조 색을 연결했다. 더미 셰이더에 공개되지 않은 값은 `Material.GetColor()`가 아니라 직렬화된 원본 값에서 읽었다. **장갑의 `_MaskTint`를 사용하는 원본 전용 마스크 처리는 아직 동일하게 복원하지 않았다.** 텍스처·노멀·기본 Tint를 복원한 상태다. 꼬마덕의 조명·발광·Rim도 기존 URP 근사 구현이다.

프리뷰의 비교 순서는 왼쪽부터 USEC, 호위 용병, 꼬마덕이다. 캐릭터 프리뷰는 스킨을 임시 메시로 베이크하고 같은 포즈·조명에서 렌더링했다. 이전 모습은 프리뷰 복사본에 기존 머티리얼을 다시 연결해 비교했다. 실제 프리팹 레이어를 바꾸지 않고 프리뷰 복사본만 Default로 맞췄다.

## 검증

2026-10-18, 편집 모드의 별도 Preview PhysicsScene에서 실제 `TryDropLoot()`를 호출했다. 기본 맵 물리는 시뮬레이션하지 않았고 Play Mode·전투·사망 FX를 실행하지 않았다.

- 두 종류 모두 1.2m에서 생성 후 일반 Default 레이어 바닥에 착지했다. 최종 물리 Collider 바닥 높이는 0m였다.
- Trigger 유지, 일반 Collider 1개, 중복 드롭 차단, 상자 열기 이벤트 1회, 동일 WeaponInstance 전달을 확인했다.
- 직접 `TryInteract()`를 호출한 결과다. 실제 입력·거리 검색·UI 표시를 플레이로 검증했다는 뜻은 아니다.
- 최종 확인에서 8개 Dropper 참조·몸통 중심 연결, 5개 적 프리팹의 Enemy 레이어, 두 상자의 Interaction 레이어와 원본 Trigger 치수 보존을 확인했다.
- C# 컴파일 오류와 관련 셰이더 오류가 없었다. 임시 물리 씬·프리뷰·검증 오브젝트를 정리했다.
- 실제 GroundZero 지형에서 처치 후 착지, FOV·게임 카메라·상호작용 입력은 수동 플레이 확인 대상이다.

기록: `.codex/verification/20261018-enemy-loot/physics-results.json`, `final-audit.json`, `character-materials.json`, `before.png`, `after.png`, `characters-before.png`, `characters-after.png`.
