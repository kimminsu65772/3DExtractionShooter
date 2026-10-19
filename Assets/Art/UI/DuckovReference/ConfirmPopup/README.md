# 새 게임 덮어쓰기 확인창 레퍼런스

관련 기능: DATA-01(저장/불러오기), LOOP-01(로비 준비), UI-01(핵심 HUD).

`OverwriteConfirm_Reference.prefab`을 GameStart의 Canvas 마지막 자식으로 배치한다. ButtonGroup 내부에 넣지 않는다. 기본 상태는 사용자가 비활성화하고 필요할 때 표시하도록 연결한다. 지금 프리팹은 확인하기 쉽게 활성 상태이며 런타임 콜백은 없다.

## 원본과 변경 범위

- 원본: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets/Scenes/MainMenu/MainMenu.unity`
- 계층: `Canvas/MainMenuContainer/SaveFileSelection/RestorePanel/ConfirmPanel`
- 원본 백업 복원 확인창에서 SlotLine 전체(슬롯 번호·백업 시각)를 제외했다. 제목·경고 아이콘/문구·확인/취소 버튼·전체 화면 반투명 차단 배경을 남겼다.
- 원본의 RectTransform, 수평/수직 LayoutGroup, ContentSizeFitter, 색과 버튼 Color Tint를 사용한다. ProceduralImage는 일반 Image와 기존 TitleButtons/Dependencies/Rounded40.png로 대체했다. 원본 모서리별 세부 표현·TrueShadow와 동일하다고 보장하지 않는다.
- 경고 아이콘은 기존 Layouts/Dependencies/Sprites/priority_high_64dp_FFFFFF_FILL1_wght400_GRAD0_opsz48_b09f9d2c.asset, GUID b09f9d2c3c332d44f8cedd186bf49614를 재사용했다.
- 원본 게임 코드, DLL, 클릭 콜백, 로컬라이징, DOTween과 기타 원본 연출 컴포넌트는 가져오지 않았다. 제외 컴포넌트 설정은 source-manifest.json에 기록만 남겼다.

## 사용자 연결

글꼴은 사용자 담당이므로 TMP 기본 글꼴과 읽을 수 있는 임시 영문을 사용했다. 별도로 만든 글꼴을 네 Text 컴포넌트에 지정하고 다음 문구로 바꾼다.

| 위치 | 권장 문구 |
| --- | --- |
| Layout/Title | 새 게임을 시작할까요? |
| Layout/Warning/Text (TMP)_1 | 기존 저장 데이터가 덮어써집니다. |
| Layout/Bttons/BtnConfirm/Text | 확인 |
| Layout/Bttons/BtnCancel/Text | 취소 |

`Bttons`는 원본 계층 이름이다. 확인/취소의 OnClick은 비어 있다. 확인은 승인된 신규 저장 경로, 취소는 창 닫기에 후속 연결한다. 팝업을 여는 것만으로 파일을 변경하지 않는다.

## 검증

편집 모드 렌더링 Preview.png 확인. Missing Script 0, 확인/취소 버튼 2개, 영구 콜백 0, TargetGraphic 정상, 루트 전체 화면 Image의 Raycast Target 활성. 장식 그래픽은 Raycast Target을 껐다. 기본 문구에서 패널/버튼/텍스트 크기와 가시성을 확인했다. 한글·사용자 글꼴 적용 후 재배치, 실제 클릭/키보드 포커스, 저장 연결은 미검증이다.

작업 전후 GameStart 씬은 사용자 미저장 상태를 그대로 보존했고 씬 배치·저장·Play Mode·입력 주입을 하지 않았다. 재생성 도구는 `.codex/tools/import-confirm-popup.cs`이며 기존 프리팹을 덮어쓰지 않으므로 재실행하지 않아도 된다.
