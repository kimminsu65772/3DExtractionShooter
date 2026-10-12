# 테스트 씬 선택

관련 기능: ENEMY-01(기본 행동), ENEMY-02(적 종류 확장), ACT-03(사격).

테스트 씬은 목적별로 따로 저장되어 있다. 한 번에 하나만 연다. Project 창에서 원하는 씬을 더블클릭해 교체하며, Additive로 여러 시험 씬을 함께 열지 않는다. 동시에 열면 각 씬의 카메라, GUI, 입력, 데이터 및 시험 담당자가 함께 실행된다.

| 목적 | 씬 |
| --- | --- |
| BT 학습·실험 | `Assets/Scenes/AITest.unity` |
| 초기 단일 클립 외형 재생 | `Assets/Test/DuckAnimationTest.unity` |
| 초기 스나이퍼 투사체 사격 | `Assets/Test/SniperFiringTest.unity` |
| 원본 컨트롤러·MagicBlend 비교 | `Assets/Test/CommonCharacter/CommonCharacterAnimationTest.unity` |
| 보이는 손 기준 무기 장착 비교 | `Assets/Test/CommonCharacter/VisibleHandMountTest.unity` |
| 직접 클립·프로젝트 무기 장착·사격 | `Assets/Test/CommonCharacter/DirectClipWeaponTest.unity` |
| 플레이어 오리 모델 통합 | `Assets/Characters/DuckPlayerCopy/DuckPlayerIntegration.unity` |
| 적 오리 모델 통합 | `Assets/Characters/DuckEnemyCopy/DuckEnemyIntegration.unity` |
| 8종 캐릭터 외형 비교 | `Assets/Characters/DuckCandidates/DuckCandidateGallery.unity` |

2026-10-09 동시에 열려 있던 씬을 정리하고 외형 비교 씬 하나만 남겼다. 당시 저장하지 않은 AITest와 DuckAnimationTest는 `Assets/Test/SceneBackups` 아래의 날짜별 폴더에 별도 복사본으로 보존했다. 기존 씬 파일을 덮어쓰지 않았다. 복사본은 해당 시점의 편집 상태를 복구하는 용도이며 평소 테스트 대상이 아니다.
