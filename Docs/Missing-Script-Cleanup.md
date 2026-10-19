# 프로젝트 Missing Script 정리 — 2026-10-16

관련 범위: WORLD-01(탐험 공간), LOOP-01(로비 준비) 등을 포함한 프로젝트 자산 정리. 게임 기능 복원 완료를 뜻하지 않는다.

## 처리 결과

- Assets의 Unity가 인식하는 프리팹 3,574개와 씬 55개를 검사했다. 비활성 오브젝트, 중첩 프리팹, 예제·테스트·백업 씬을 포함했다.
- 3,082개 자산에서 Missing MonoBehaviour 61,157개를 제거했다. 원본 애니메이터 두 개의 끊어진 StateMachineBehaviour 하위 자산도 총 4개 제거했다.
- 정상 타입을 읽을 수 없고 스크립트 GUID도 존재하지 않는 설정 데이터 1,478개(.asset), 타임라인 9개(.playable)를 백업 후 삭제했다. 데이터 파일 정리는 별도 확인을 받고 수행했다.
- 설정 데이터는 삭제 전에 정상 소유 자산의 참조가 없음을 확인했다. 타임라인 중 불꽃놀이 데이터의 연결이 11개 씬에 남아 있어 PlayableAsset 참조 11개와 해당 트랙 바인딩 33개를 먼저 정리했다. 파티클·씬 오브젝트 자체는 삭제하지 않았다.
- 정상 게임 스크립트, 메시, 머티리얼, 텍스처, 원본 추출 프로젝트는 삭제하지 않았다. 현재 열려 있는 GroundZero와 기존 Git 변경을 보존했다.
- 일회성 Editor 처리 도구는 Assets에서 제거하고 `.codex/tools/ProjectMissingScriptCleanup.cs`에 보관했다. 런타임 관리 코드는 추가하지 않았다.

## 검증

- 전체 프리팹·씬 재검사: Missing 컴포넌트 0, 처리 실패 0.
- 최종 텍스트 자산의 m_Script 참조를 GUID·local fileID·MonoScript.GetClass로 검증했다. 서로 다른 참조 138종 모두 유효했다.
- 삭제한 설정/타임라인 GUID를 다시 조사해 Assets에 잔여 참조가 없음을 확인했다.
- 변경 전후 GameObject·Transform·Renderer·Collider 등 비-MonoBehaviour 객체 수를 비교했다. 구형 씬 6개에 Unity 저장 형식의 SceneRoots/OcclusionCullingSettings가 추가된 것 외에 차이는 없었다.
- 일부 과거 테스트 씬은 현재 SquadCommander의 RequireComponent 조건에 따라 SquadRearPositionPlanner가 Unity에 의해 자동 보충됐다. 직접 새 전술이나 이동 규칙을 구현한 것은 아니다.
- Play Mode·전투·이동 테스트는 하지 않았다. 커밋·푸시·Notion·자동화 변경도 하지 않았다.

## 콘솔 점검

콘솔은 지우지 않았다. 점검 시 Error 2건과 Warning 14건, 서로 다른 메시지 3종이 있었다.

일회성 정리 도구 제거 후 재컴파일이 완료됐고, 마지막 조회에서는 Error/Warning 기록이 없었다. 명시적인 콘솔 Clear는 실행하지 않았다. 이 기록 초기화가 아래 기존 모션 참조 오류의 수정 완료를 뜻하지는 않는다. 해당 컨트롤러 파일의 잘못된 참조는 그대로 남아 있다.

1. `Duplicating NavMeshSurface does not duplicate the referenced NavMesh data`
   - AI Navigation 스킬을 적용하고 설치된 2.0.9 패키지의 NavMeshSurface.OnValidate/UnshareNavMeshAsset를 확인했다.
   - 여러 백업 씬을 Additive로 열면 동일 NavMeshData를 중복 Surface로 판단해 새로 열린 쪽의 참조를 null로 만든다.
   - 저장된 과거 백업 씬 8개의 NavMeshData 참조 14개를 삭제 전 백업과 비교해 원래대로 복구했다. 현재 GroundZero의 Factory/Camp 데이터 연결은 유지됐으며 재베이크하지 않았다.
2. `Creating missing SquadRearPositionPlanner component for SquadCommander in Factory_SquadTest.`
   - 과거 씬을 열면서 현재 코드의 `[RequireComponent(typeof(SquadRearPositionPlanner))]`를 충족시키는 과정에서 발생한 Unity의 호환성 보충 기록이다. Missing Script가 남았다는 의미는 아니다.
3. `Broken text PPtr ... EnemyAnimator.controller ... (-2425434872338613091) doesn't exist!`
   - `Assets/Animations/Enemy/EnemyAnimator.controller`의 Locomotion 상태에 있는 m_Motion이 존재하지 않는 내부 객체 ID를 가리킨다(136행).
   - 기존 Git HEAD에도 동일한 잘못된 참조가 있다. 이 파일은 이번 정리에서 수정하지 않았다.
   - 이 컨트롤러를 참조하는 EnemyAnimationSet.overrideController에는 클립 치환이 없다. 현재 씬·프리팹 등 직렬화 자산에서 해당 override의 사용처는 발견되지 않았다.
   - Missing Script와 다른 기존 애니메이션 자산 오류다. 현재 사용 여부에 따라 유효한 이동 BlendTree/클립을 복구하거나 레거시 자산을 별도로 정리해야 한다. 임의의 모션을 지정하거나 오류를 숨기는 수정은 하지 않았다.

## 백업과 복구

백업 루트: `.codex/backups/2026-10-16-missing-scripts/`.

- `Assets/`, `continuation/Assets/`: 컴포넌트 제거 직전의 파일과 메타.
- `scriptable-assets/Assets/`: 삭제한 설정 데이터와 메타.
- `controller-assets/Assets/`: 원본 애니메이터 정리 전 파일과 메타.
- `timelines/scriptable-assets/Assets/`: 삭제한 타임라인과 메타.
- `timelines/reference-owners/Assets/`: 타임라인 연결을 해제하기 직전의 씬과 메타.
- cleanup/verification/구조 비교/참조 조사/삭제 보고 JSON과 SHA256, 오브젝트별 삭제 기록을 함께 보관했다.

복구가 필요하면 Unity 편집 상태와 복구 범위를 먼저 확인한 뒤 필요한 파일·메타만 대응 위치에 되돌린다. 후속 편집이 있을 수 있으므로 백업 전체를 무조건 덮어쓰거나 Git을 초기화하지 않는다.

프리팹 일괄 저장 방식은 [Unity SaveAsPrefabAsset 문서](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityeditor/prefabutility/saveasprefabasset)를 확인했다. 실제 프로젝트 API와 동작은 설치된 Unity 6000.3.7f1에서 검사했다.
