# UI 참조 이미지 보관

UI-01(핵심 HUD)에 사용할 원본 추출 이미지 124개를 분류해 보관한다.

| 폴더 | 내용 |
| --- | --- |
| Panels | 패널, 사각 테두리, 가로 프레임 마스크 |
| Icons | 장비·메뉴 픽토그램, 소지 무게·칸 수 아이콘 |
| StatusIcons | 버프·디버프 상태 아이콘 |
| InputAndCursor | 키·버튼 표시, 조준점, 커서 |

원본 경로: `C:/Users/admin/Desktop/AssetRipper_export_20261007_063805/ExportedProject/Assets/Texture2D`.

PNG만 복사하고 Unity에서 새로운 GUID를 생성했다. 스크립트·DLL·UI 프리팹·원본 셰이더는 가져오지 않았다. 모두 Single Sprite로 가져오며, 원본 메타데이터의 Border, Pivot, Pixels Per Unit을 반영하고 UI용으로 mipmap을 끈다. 원본 Sprite.asset은 중복 복사하지 않는다.

`Panels/Panel.png`의 Border는 `(8, 8, 8, 8)`이다. UI Image의 Type을 Sliced로 설정해 늘린다. 실제 화면에 배치하거나 기존 UI를 교체하지는 않았다. 전체 아이템 썸네일 일괄 가져오기는 이번 보관 범위가 아니다.
