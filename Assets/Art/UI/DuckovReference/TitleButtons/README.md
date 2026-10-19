# GameStart 버튼 레퍼런스

관련 기능: UI-01(핵심 HUD), LOOP-01(로비 준비).

`Prefabs/`의 NewGameButton_Reference, ContinueButton_Reference, QuitButton_Reference를 Canvas 아래에 배치해 사용한다. 씬 배치와 실제 클릭 연결은 아직 하지 않았다.

- 새 게임·이어하기: 원본 `Canvas/MainMenuContainer/Menu/MainGroup/Layout/Continue`를 기준으로 한 540×128 버튼. 원본은 하나의 버튼에서 저장 상태에 따라 새 게임/이어하기 문구를 바꾼다. 우리 정책에 맞게 두 프리팹으로 분리했다.
- 종료: 같은 Layout의 `Btn_Quit`, 329.45×90 버튼.
- 원본의 아이콘, 색, RectTransform, 내부 HorizontalLayoutGroup와 LayoutElement를 사용한다. 글자 변경으로 텍스트 폭은 원본 중국어와 다르다.
- ProceduralImage는 일반 Image와 이번에 생성한 9-slice Rounded40.png로 대체했다. TrueShadow와 원본의 동적 효과를 완전히 복원하지 않았다.
- **글꼴은 사용자가 직접 만든 글꼴을 연결한다.** 세 프리팹의 `Text (TMP)`에서 Font Asset을 지정한다. 현재 LiberationSans SDF는 TMP가 자동 지정한 임시 기본값이며 한글 표시 완료 상태가 아니다. 로딩용 글꼴과 별도로 생성하려던 임시 글꼴은 삭제했다.
- 원본 게임 로직·DLL·저장 콜백·로컬라이징·DOTween 의존성은 가져오지 않았다. Button.onClick은 비어 있다. Button의 기본 Color Tint만 사용하며 Hovering 오브젝트는 비활성이다.

## 후속 연출 메모

사용자 결정: 원본 DOTween은 기록만 하고 우리 쪽에서 연출을 구현한다. 현재 연출 스크립트는 추가하지 않았다.

- 원본 PunchReceiver: duration 0.1초, punchScaleUniform -0.1, vibrato 10, elasticity 1, 위치·회전 punch 0.
- 원본 호버 크기 보간: idleSizeDelta (0,0), activeSizeDelta (-8,-8), duration 0.1초. Hovering 흰색 alpha 약 0.0902.
- 원본 전체 필드와 제외한 컴포넌트는 source-manifest.json에 보존했다. 이 수치를 우리 연출의 확정값으로 간주하지 않는다.

## 확인 범위

Unity 편집 모드에서 세 프리팹 저장과 아이콘·모양·크기 렌더링 확인. 누락 스크립트 0, 원본 영구 클릭 콜백 0, TargetGraphic 참조 존재. 글꼴 작업을 사용자 담당으로 되돌린 후 다시 구조를 확인했다. 임시 글꼴로 만든 미리보기는 제거했다. 한글 표시, 실제 입력, 저장·씬 이동 연결은 미검증이다. Loading 씬 clean·Play 꺼짐으로 복귀했으며 GameStart 씬은 수정하지 않았다.

원본: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets/Scenes/MainMenu/MainMenu.unity`. 아이콘 GUID·텍스처 의존성은 Dependencies의 원본 .meta와 source-manifest.json을 참조한다. 외부 원본은 변경하지 않았다.
