# 원본 로비 씬 가져오기

> 2026-10-16 후속 정리: 아래 내용은 가져오기 당시 기록이다. 이후 현재 프로젝트 전체 Missing Script 정리를 수행하면서 로비 씬의 Missing 컴포넌트와 참조가 끊어진 원본 설정 데이터도 정리했다. 따라서 현재 씬 파일은 원본과 바이트 단위로 같지 않다. 삭제 전 사본은 `.codex/backups/2026-10-16-missing-scripts/`에 보관하며, 상세 결과는 `Docs/Missing-Script-Cleanup.md`를 따른다. 원본 추출 폴더는 변경하지 않았다.

관련 기능: LOOP-01(로비 준비)의 공간 자산 준비. 로비 기능 구현 완료를 의미하지 않는다.

## 프로젝트 사격 표적 — 2026-10-16

- 관련 기능: ACT-03(사격), INV-02(장비).
- `Base_SceneV2.unity`의 `TrainingTargets` 아래에 무방어·1·2·3레벨 표적을 배치했다. 원래 `Spawners/Dummy/TestSpawn_0~3`의 위치와 회전을 사용하며, 원래 생성 위치 오브젝트는 보존했다.
- 외형은 `CharacterModel_Dummy_1.prefab`을 사용한다. 1~3레벨에는 조립해 둔 `Armor_Lv1~3`과 `Helmet_Lv1~3` 외형을 원래 소켓에 부착했다.
- `EnemyProtectionLoadout`이 방어구 ID 4001~4003, 헬멧 ID 5001~5003을 읽는다. 무방어는 두 ID 모두 0이다. `Lobby Combat Data`의 기존 `CombatDataCompositionRoot`를 네 표적이 공유한다.
- `HealthComponent`, `HitPartProvider`, 머리·몸통 `HitBox`를 재사용한다. 충돌체는 Enemy 레이어의 Trigger이며 기존 Projectile의 충돌 검사로 피격된다. Trigger 콜백이나 Rigidbody는 추가하지 않았다.
- 체력은 기존 시험 표적과 같은 100이다. 후속 요청으로 과거 `TargetDummyHealthReset`을 원래 GUID로 복구해 네 표적에 부착했다. 마지막 피해 후 3초 동안 추가 피해가 없으면 체력과 장착 방어구·헬멧 내구도를 각 최댓값으로 초기화한다. 체력 0에서도 복구되며, 체력이 이미 회복된 경우에도 장비 내구도를 초기화한다. 장비 인스턴스와 ID는 교체하지 않는다. 리스폰·초기화 UI·AI·이동·전리품 드롭은 없다.
- 에디터에서 네 표적과 참조, 머리·몸통 Raycast의 피해/부위 판정 연결, Missing Script 없음 및 지원 셰이더를 확인했다. 실제 Play Mode 사격은 실행하지 않았다.
- 체력 초기화 컴포넌트 복구 후 Unity 컴파일과 네 표적의 활성화·각 1개 부착·`resetDelay=3` 저장을 확인했다. 실제 플레이에서 3초 후 체력이 돌아오는 동작은 별도로 확인해야 한다.
- 내구도 초기화 확장 후 컴파일과 격리된 에디터 객체로 대기 시간 전 미초기화, 추가 피격 시 대기 갱신, 체력·두 장비 동시 복구, 최대 체력일 때 내구도 복구, 체력/내구도 0에서 복구, 동일 장비 객체 보존, 무장비 표적의 체력 복구를 확인했다. 시간 경과는 내부 마감 시각을 조정하고 Update를 호출해 검사했으며 실제 Play Mode에서 기다리거나 사격한 검증은 아니다.
- 배치 전 씬과 메타는 `.codex/backups/2026-10-16-lobby-training-targets/`에 보관한다. 다른 씬과 원본 프리팹·게임 코드는 수정하지 않았다.

## 열어볼 씬

- `Base_SceneV2.unity`: 주 로비 공간. 로비 배치를 살펴볼 때 먼저 연다.
- `Base_SceneV2_Sub_01.unity`: 추가 로비 공간. 필요한 경우 주 공간과 Additive로 연다.
- `Base.unity`: 원작의 공통 로비 관리·창고 등 구성과 서브 씬 목록. 이것만 열면 전체 공간이 나타나는 구조가 아니다.

원본 `Base`의 MultiSceneCore에는 `Base_SceneV2`와 `Base_SceneV2_2`라는 두 서브 구역 ID가 기록되어 있다. 파일명과 런타임 구역 ID는 반드시 같지 않다. 구형 `Base_Scene.unity`는 이번에 가져오지 않았다.

## 조명과 MK Toon 보정 — 2026-10-16

- 관련 기능: LOOP-01(로비 준비)의 공간 자산 보정.
- `Base`, `Base_SceneV2`, `Base_SceneV2_Sub_01`에 남은 `SodaPointLight` 49개를 Unity Point Light로 대체했다. 원본 추출 씬의 동일 오브젝트 ID에서 HDR 색을 읽고, 원래 위치와 스케일을 사용했다. 기존 일반 조명은 보존했다.
- 원작의 메시 기반 덧셈 조명과 일반 Light는 감쇠가 다르다. 원래 반경의 최대 축과 HDR 색의 세기로 근사했으며, 원작의 `hardness`·`fallOff` 또는 비등방성 범위를 동일하게 복원한 것은 아니다. 추가 조명에는 그림자를 켜지 않았다.
- 흰색으로 맵을 가리던 `ZeroPoint`, `SunFogTD`, `EdgeLightQuad`, `BuildingGrid` 더미 셰이더의 렌더러 58개를 비활성화했다. 오브젝트·메시·원본 재질은 삭제하지 않았다. 원작 안개·건설 표시 기능의 복원을 의미하지 않는다.
- 맵·소품·캐릭터 외형의 렌더러 1,247개에 `Assets/Materials/LobbyMKToon`의 복사본 182개를 연결했다. GroundZero와 같은 MK Toon Simple/Cel, DiffuseSmoothness 0.35, LightThreshold 0.35, Specular Off를 사용한다. 외곽선은 추가하지 않았다.
- 원본의 메인 텍스처·반복·오프셋·Tint·노멀·노멀 세기·발광·Smoothness를 대응 속성으로 옮겼다. 일반 불투명 재질의 알파는 1로 두고 명시적 Cutout은 보존했다. 원작의 월드 좌표 타일링·Coverage·캐릭터 마스크·EdgeLight 전용 효과는 포팅하지 않았다.
- 물·모니터 Flipbook·특수 파티클·텔레포터 등은 Toon 변환 대상에서 제외했다. 활성 화면에서 일부 전용 효과가 흰색으로 보일 수 있다. 천장/벽을 상황에 따라 숨기는 원작 런타임 제어도 복원하지 않았다.
- 현재 PC URP 설정은 픽셀당 추가 조명을 사용하고, 오브젝트당 추가 조명 한도는 기존 값 2를 유지한다. 전역 렌더링 설정이나 탐험 맵은 바꾸지 않았다.
- 기존 Lighting Settings/Data는 보존했다. 검사 시 활성 lightmap은 없고 로비 반사 프로브 3개에 bakedTexture가 없었다. GI 재베이크나 반사 프로브 갱신은 수행하지 않았다.
- 저장 후 조명 수·Toon 참조·원본 텍스처/색상 보존·Missing Script 없음·표적 4개 보존을 확인했다. Scene View와 Main Camera 정지 화면을 확인했으며, Play Mode 플레이/성능 검증은 하지 않았다. 원작과의 시각적 완전 일치가 아닌 조명 대체 및 Toon 보정이다.
- 적용 전 씬/메타와 확인 이미지는 `.codex/backups/2026-10-16-lobby-lighting-toon/`에 보관했다.

## 원본과 리소스

- 원본: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets/Scenes`.
- 세 씬과 씬 메타를 변경하지 않고 복사했다. 원작의 오브젝트 배치·계층·직렬화된 필드를 보존한다.
- 기존 GUID가 있는 에셋은 재사용하고, 부족한 참조 에셋 746개와 메타를 `Assets/Third_Party_Assets/LobbyRestored/Dependencies`에 추가했다. 파일·메타 합계 약 77.7MiB다.
- GroundZeroRestored, HiddenWarehouseRestored, DuckovImported 및 기존 리소스와 의존성을 공유할 수 있다. 로비 폴더 하나만 옮기거나 기존 공유 폴더를 삭제하면 참조가 끊길 수 있다.
- 조명·머티리얼·텍스처·메시·프리팹·애니메이션 등 실제 참조 자산을 따라 복사했다. 원작 DLL·C# 코드·다른 탐험 씬은 추가하지 않았다.

## 미복원 범위

- 정적 참조 추적의 미해결 GUID는 28개다. 의도적으로 제외한 DLL 22개, VLB 스크립트 1개, 원본에도 대응 에셋이 없는 GUID 5개다. 새 로비 씬의 원본 미존재 직접 참조는 주로 `overrideCurtainScene`이다.
- Missing Script를 임의로 삭제하지 않았다. 원작의 자동 서브 씬 로딩, 상인·창고·건설·NPC·출발·저장 기능이 작동하도록 연결한 것은 아니다.
- 원본 전용 셰이더와 조명 후처리의 완전 복원 또는 MK Toon 변환은 하지 않았다. 실제 화면의 색·조명·효과가 원작과 같다고 보장하지 않는다.
- 원작 SceneReference에 저장된 문자열 GUID와 추출된 씬 메타 GUID가 서로 다른 항목이 있다. 원작 로딩 코드를 그대로 붙인다고 자동 해결되지 않으며, 우리 씬 전환을 연결할 때 별도로 처리한다.

## 확인 범위

- 씬 파일의 원본 SHA256 일치, 추가 의존성의 메타 존재, Unity의 세 SceneAsset 인식을 확인했다.
- 가져오기 후 Unity Console 오류 없음. 새 씬을 실제로 로드하거나 Play Mode·동작 테스트를 수행하지 않았다. Missing Script와 렌더링의 런타임 검증 완료를 뜻하지 않는다.
- 기존 GroundZero 씬을 활성 상태로 보존했고 씬 배치·프로젝트 Build Settings·게임 코드는 변경하지 않았다.
