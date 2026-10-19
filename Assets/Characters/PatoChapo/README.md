# ENEMY-02(적 종류 확장): 꼬마덕과 호위 용병 외형

## 바로 확인할 파일

- `PatoChapoGallery.unity`: 두 외형을 나란히 배치한 확인 씬. 카메라·조명·바닥이 포함된다.
- `Prefabs/Duck_PatoChapo.prefab`: 꼬마덕(Pato Chapo, ShortEagle)의 전용 모델.
- `Prefabs/Duck_PatoChapoGuard.prefab`: 꼬마덕과 함께 생성되는 용병의 Solider 외형.

두 프리팹은 외형 전용이다. AI, 체력, 피격 충돌체, 무기·방어구 장착, 스폰 테이블은 연결하지 않았다. 기존 플레이어·적·분대 배치와 GroundZero 씬은 변경하지 않는다. 원작 DLL과 런타임 외형 조립 스크립트를 요구하지 않는다.

## 출처와 재사용

- 원본 추출 경로: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets`.
- 꼬마덕 프리셋 `EnemyPreset_Boss_ShortEagle`의 characterModel GUID는 원본 `CharacterModel_Duck_Boss_1.prefab`과 일치한다. 이미 프로젝트에 임포트되어 있던 이 모델에서 필요한 자산을 분리했다.
- 부하 프리셋 `EnemyPreset_Boss_ShortEagle_Elete`는 `0_CharacterModel_Custom_Enemy.prefab`과 `CustomFacePreset_Solider`를 참조한다. 이미 고정 조립한 `DuckCandidates/Prefabs/Duck_Solider.prefab`의 외형을 재사용했다.
- `SourceData/*.yaml.txt`는 원본 프리셋 두 개와 Solider 외형 설정의 읽기 전용 참고 사본이다. 원작 ScriptableObject나 실행 코드로 임포트한 것이 아니다.
- 원작의 꼬마덕 체력 160, 부하 체력 60, 부하 스폰 범위 3~4는 참고 데이터다. 이번 프로젝트의 전투 수치나 인원수로 자동 적용하지 않았다. 원작 아이템 ID도 우리 카탈로그 ID와 혼동하면 안 된다.

## 복원 방식

### 꼬마덕

- 전용 리그·스킨 본 37개·원래 Avatar를 보존했다. 공통 커스텀 오리의 Avatar를 강제로 붙이지 않았다.
- 원작 전용 AnimatorController와 참조된 애니메이션 14개를 `Animations`에 복사하고 참조를 연결했다. 대기 포즈의 첫 프레임을 프리팹의 기본 자세로 저장했다.
- 전용 몸통 메시와 머리에 부착된 원본 도끼 장식을 보존했다. 이 도끼는 장착 무기나 전리품으로 연결한 것이 아니다.
- 복사본에서 Missing Script 6개와 원작 동작 컴포넌트를 제거했다. 비활성 원본 FakeAO·보스 표식은 외형 복구본에서 제외했고, 추가 CharacterShowBack 렌더 패스도 제외했다.
- `Assets/Shader/RestoredPatoChapoCharacter.shader`는 원작 셰이더를 그대로 복구한 코드가 아니라 URP용 근사 구현이다. 원본 텍스처의 알파를 몸통 색 마스크로 사용해 부리·발의 RGB를 보존하고, 노멀·발광·테두리 빛을 연결한다. 마스크 텍스처이므로 복사본 임포터의 Alpha Is Transparency를 껐다.
- 모든 셰이더 패스에 동일한 UnityPerMaterial 버퍼를 사용하며 URP의 그림자·깊이 패스 구현을 공유한다. 피격·체온·바람·원작의 가림 표시까지 동일하게 재현하지 않는다.

### 부하

- 머리·부리·눈·눈썹·날개·발이 조립된 Solider 외형을 사용한다. 몸통만 있는 프리팹이 아니다.
- 재사용한 후보의 스킨 본 24·25번은 null이었지만 어떤 정점의 양수 가중치에도 사용되지 않았다. 복구본의 몸통 메시만 별도로 복사하고 마지막 두 bind pose와 본 슬롯을 함께 제거했다. 기존 후보와 공유 메시에는 손대지 않았다.
- 공통 모델의 기존 Avatar·적 애니메이터를 재사용한다. `DuckCandidates`, `DuckPlayerCopy`, `DuckEnemyCopy` 폴더는 의존성이므로 유지해야 한다.

### 효과·머티리얼

- 보스 연기·불꽃은 원본 Particle System과 색상 설정을 보존하고 정식 URP Particles/Unlit 머티리얼로 연결했다.
- 이름이 같은 추출본 더미 셰이더를 피하기 위해 정식 셰이더는 Packages 경로로 직접 로드했다.
- 달리기 먼지는 참조를 보존하되 비활성 상태다. 동작에 맞춰 켜는 런타임 연결은 아직 없다.
- 원본 에셋·원본 추출 폴더는 수정하지 않았다. 새 프리팹은 Third_Party_Assets, Assets/Test, 원작 DLL에 의존하지 않는다.

## 검증 범위와 제한

- 두 프리팹에 Missing Script가 없고 필요한 메시·머티리얼·유효한 Avatar가 연결됨을 확인했다. 스킨 본과 bind pose 수는 각각 보스 37개, 부하 24개로 일치하며 모든 본이 해당 프리팹 내부를 참조한다. 복구 셰이더 오류와 원치 않는 외부 의존성도 없음을 정적으로 확인했다.
- 원본 전용 AnimatorController의 걷기·대기 계열 커브 경로는 꼬마덕 리그에 존재한다. 일부 구르기·근접 클립에는 현재 리그에 없는 제어본·추출 중 해석되지 않은 경로가 남아 있다. 원작의 모든 모션이 완전히 복원됐다는 뜻이 아니다.
- 부하는 기존 공통 애니메이션을 유지한다. 기존 클립의 IKTarget 및 placeholder 경로는 이번 복원에서 새로 해결하지 않았다.
- Play Mode, 실제 전투·무기 장착·동작 테스트와 파티클 시뮬레이션은 수행하지 않았다. 확인 씬에서 외형·색·발광·효과를 먼저 직접 확인하고, 이후 필요한 AI 프리팹에 연결한다.
