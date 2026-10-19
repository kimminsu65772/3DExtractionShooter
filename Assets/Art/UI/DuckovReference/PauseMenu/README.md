# ESC 메뉴 외형 레퍼런스

관련 기능: UI-01(핵심 HUD). 실제 게임 동작을 복원한 메뉴가 아니다.

최신 연결: 싱글톤 `UIManager`가 ESC와 팝업 스택을 관리한다. 인벤토리 본창·상세창·우클릭 메뉴도 팝업으로 취급하며, ESC는 최상단 하나를 닫고 비어 있을 때 이 메뉴를 연다. 이 메뉴도 다음 ESC로 닫힌다. 로딩 중에는 스택을 정리하고 ESC를 무시한다. 메뉴 버튼·일시정지는 미구현이다. 추가 코드의 독립 C# 컴파일만 확인했으며 실제 플레이 테스트는 사용자가 담당한다. 아래 열기 전용 기록은 이 변경 이전 단계다.

2026-10-19 후속 연결: `EscapeMenuController`와 `Resources/UI/EscapeMenuHost`가 실행 시 이 프리팹을 참조한다. 로딩 이외 씬에서 ESC로 한 번 생성해 표시하고, 로딩 중과 활성 씬 변경 시 숨긴다. 재입력 닫기·Resume/Options 등 버튼 기능·일시정지·플레이어 입력 차단은 추가하지 않았다. 아래의 리소스 복원 당시 검증과 별개로 추가 코드의 독립 C# 컴파일만 통과했고 실제 동작 테스트는 사용자가 담당한다.

`PauseMenu_Reference.prefab`은 원본 `Resources/GameManager.prefab`의 `GameManager/PauseMenu`에서 ESC 메인 화면을 분리한 독립 uGUI Canvas다. 기존 플레이 씬·런타임 UI에는 배치하지 않았다. Unity에서 이 프리팹을 직접 열어 외형을 검토한다.

## 포함된 구성

- Screen Space Overlay Canvas, sortingOrder 10000, CanvasScaler 2560×1440, 높이 기준 스케일, GraphicRaycaster.
- Menu/Layout: 제목, Resume·Controls·Options·MainMenu·Quit·RecoverPlayerPos와 설명. 원본의 왼쪽 기준 x=128, 세로 레이아웃 간격 8 및 레이아웃 설정을 보존했다. Controls는 원본처럼 비활성이다.
- 하단 Wishlist·문의·설문 외형. 원본 지역별 NonCN은 비활성이다. 원작에서 실제로 보여 줄 항목을 고르는 게임 코드까지 복원한 것은 아니다.
- 기본 Image·Button·TMP·레이아웃 그룹·ContentSizeFitter·LayoutElement·CanvasGroup. Button 영구 클릭 콜백은 모두 비어 있다. 일부 원본 링크는 일반 그래픽과 원작 전용 컴포넌트로 구성되어 있어 클릭 기능 없이 외형만 남는다.
- 신규 리소스는 SteamLogo·content_copy·CompassArrow의 Sprite 3개와 Texture 3개 및 원본 meta다. 기존 Rounded40, Rect16, LiberationSans SDF를 참조한다. 별도 폰트·머티리얼·셰이더·DLL·원본 게임 C#을 추가하지 않았다.

## 원본 대비 차이

- 원본 루트 scale 0을 확인용 1로 바꿨고, 원작 Hovering 제어 코드가 없어 상시 표시될 호버 자식은 비활성화했다. 그 밖의 직렬화 활성 상태는 보존한다.
- ProceduralImage는 기존 Rounded40을 사용하는 일반 9-slice Image로 대응했다. 둥근 모서리의 동적 형태·그림자·호버/트윈 효과는 동일하지 않다.
- 원본 TranslucentImage 배경은 원본 RGB 색의 일반 Image로 대응했다. 배경 화면 블러·원작 머티리얼은 복원하지 않았다.
- 원본 중국어 폰트 대신 기존 TMP 기본 LiberationSans SDF와 영문 자리표시자를 사용했다. 사용자 글꼴과 한글 문구를 적용해야 한다. 원본 문구·로컬라이징 키·폰트/머티리얼 참조는 source-manifest에 보존했다.
- OptionsPanel 전체 설정 화면, ConfirmDialogue, KeybindingIndicator는 이번 ESC 메인 화면 범위에서 제외했다.

## 확인 범위와 다음 연결

현재 세션에는 Unity MCP 도구가 없어 오프라인 프리팹 파일과 의존성 참조만 검사했다. `validation.json`은 에디터 임포트·Missing Script 실측·렌더·한글·실제 입력 검증을 의미하지 않는다. Play 테스트와 새 컴파일은 수행하지 않았다. 원본 해시와 컴포넌트별 변환 근거는 `source-manifest.json`을 참고한다.

후속 작업은 Unity에서 프리팹을 열어 레이아웃/글꼴을 확인하는 것이다. ESC 동작은 별도 구현 범위다. 현재 메뉴 열기/닫기·일시정지·저장·씬 이동·위치 복구·종료·외부 링크 동작은 없다. 루트가 독립 Canvas이므로 기존 Canvas에 넣을 때 스케일러 중복과 sortingOrder를 확인하고 기존 EventSystem을 재사용한다. 레퍼런스에 EventSystem은 포함하지 않았다.

`Menu/Layout/Btn_MainMenu`를 우리 프로젝트의 로비 이동이나 저장 완료로 해석하지 않는다. 원작 버튼 이름은 외형의 출처만 뜻한다. Wishlist·문의·설문 요소의 실제 사용 여부도 별도로 결정한다.
