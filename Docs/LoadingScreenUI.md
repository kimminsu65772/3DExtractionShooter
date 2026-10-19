# 로딩 화면 UI

관련 기능: LOOP-02(탐험 진입), LOOP-01(로비 준비).

## 적용 위치

- 씬: `Assets/Scenes/Loading.unity`
- 씬 오브젝트: `Canvas/LoadingScreenUI`
- 재사용 프리팹: `Assets/Prefabs/UI/Loading/LoadingScreenUI.prefab`

| 오브젝트 | 준비한 화면 요소 | 저장된 상태 |
| --- | --- | --- |
| LoadingScreenUI | 불투명한 검은 배경 | 켜짐 |
| Loading Indicator | 오리 정지 아이콘과 `로딩 중...` | 켜짐 |
| Click Indicator | 원형 아이콘과 `화면을 클릭하여 계속` | 꺼짐 |
| Interaction | 화면 전체의 투명 Image + Button | 꺼짐, On Click 비어 있음 |

클릭 안내를 미리 보려면 Loading Indicator를 끄고 Click Indicator를 켠다. 확인 후 원래 상태로 되돌린다. Interaction은 나중에 입력 처리를 직접 연결할 곳이며 현재 누른다고 씬이 전환되지는 않는다.

## 원본에서 확인한 구조

원본 추출 폴더의 `ExportedProject/Assets/Resources/GameManager.prefab`에서 `GameManager/SceneLoader` 하위 두 안내 그룹을 확인했다. 배경 전용 `LoadingScreen_Black.unity`와 공통 안내 UI는 별개다.

- Canvas 기준 해상도 1920×1080, 너비 기준 배율.
- Loading Indicator: 하단 중앙, y=165, 오리 영역 128×128, 텍스트 크기 36.
- Click Indicator: 하단 중앙, y=166.69, 아이콘 영역 80×80, 아이콘-문구 간격 16, 텍스트 크기 36.
- 원본 텍스트 현지화 키: `Loading`, `Title_PressToContinue`.
- 원본의 클릭 아이콘은 Plugins.dll의 ProceduralImage와 RoundModifier로 그린 테두리 2.22의 원이다. 원작 DLL 없이 기본 UI 원형 이미지 두 개로 대체했다.
- 레이아웃은 원본 값을 바탕으로 Unity Editor의 네이티브 UI 컴포넌트로 재구성했다. GameManager 프리팹 전체나 원작 스크립트를 가져온 것은 아니다.

## 아이콘·글꼴과 제한

- 기존에 가져온 원본 `spritesheet.png`(GUID `65e4bfba37538564180bc89b1cf2feec`)의 좌상단 프레임을 RawImage로 표시한다.
- 원본 FlipbookImage 셰이더는 추출본의 DummyShaderTextExporter 결과여서 프레임 재생 로직이 없다. 따라서 현재 오리는 정지 이미지다. 프레임 전환·깜빡임·페이드는 구현하지 않았다.
- 글꼴은 기존 원본 리소스 `Jua-Regular.ttf`로 필요한 글자만 구운 `Assets/UI/Loading/Fonts/Jua-Loading SDF.asset`을 사용한다. 고정 글자 28개, 512×512 정적 아틀라스다. 새로운 문구를 쓰면 글자를 다시 준비하거나 별도 폰트를 연결해야 한다.
- 같은 이름의 추출본 TMP 셰이더가 자동 선택되어 글자가 보이지 않는 문제를 확인했다. 이 폰트 머티리얼만 `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader`에 직접 연결했다. 다른 머티리얼은 변경하지 않았다.
- 이전 임시 Panel과 LoadingTextController는 삭제하지 않고 비활성 상태로 보존했다.

## 직접 구현할 연결 지점

이번 요청은 화면 요소 준비이므로 상태 전환이나 입력 대기는 작성하지 않았다. 후속 클론 코딩에서는 LoadingSceneController에서 두 안내 그룹과 Interaction의 Button을 참조하면 된다.

1. 준비 중에는 Loading Indicator를 켜고 Click Indicator와 Interaction은 끈다.
2. 합의한 준비 완료 조건을 충족하면 로딩 안내를 끄고 클릭 안내와 Interaction을 켠다.
3. 클릭 후 진행할 흐름과 로딩 씬 언로드 시점은 직접 연결한다.

원작 SceneLoader는 `clickToConinue` 옵션에서 대상 로드 progress 0.9 이후 클릭을 기다린 다음 allowSceneActivation을 허용한다. 우리 프로젝트는 부트스트랩 초기화까지 완료를 보장하려는 구조이므로, 이 순서를 그대로 복사한 상태는 아니다.

## 검증

- 실제 저장된 UI 설정을 PreviewScene에 복사하여 두 상태를 1280×720으로 렌더했다. 캡처를 위해 복사본만 ScreenSpaceCamera로 전환했다.
- 한글 글자 누락 없음과 두 화면의 텍스트·아이콘 표시를 육안 확인했다.
- 결과: `.codex/verification/20261017/loading-ui-loading.png`, `loading-ui-ready.png`.
- 새 UI에 Missing Script 없음, 씬 EventSystem 1개, Interaction의 영구 Button 콜백 0개를 확인했다.
- 실제 Play Mode 로딩·클릭·자동 화면 전환은 실행하지 않았다. 기존 전환 코드가 실행되면 현재 흐름대로 바로 다음 씬으로 넘어간다.
- 수정 전 씬: `.codex/backups/2026-10-17-loading-ui/Loading.before-original-ui.unity`.
