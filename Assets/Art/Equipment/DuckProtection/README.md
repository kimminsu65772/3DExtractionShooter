# 레벨 1~3 보호 장비 외형

INV-02(장비)의 방어구·헬멧 외형을 기존 CSV에 맞춰 조립했다. CSV와 Definition, 내구도·피해 감소·장착 로직은 변경하지 않았다.

| CSV ID | 종류·등급 | 프리팹 | 원본 외형 |
| --- | --- | --- | --- |
| 4001 | 방어구 1 | Prefabs/Armor_Lv1_4001.prefab | IG_Armor_LV1 |
| 4002 | 방어구 2 | Prefabs/Armor_Lv2_4002.prefab | IG_Armor_LV2 |
| 4003 | 방어구 3 | Prefabs/Armor_Lv3_4003.prefab | IG_Armor_LV3 |
| 5001 | Motorcycle Helmet·헬멧 1 | Prefabs/Helmet_Lv1_5001.prefab | IG_Helmat_LV1 |
| 5002 | Steel Helmet·헬멧 2 | Prefabs/Helmet_Lv2_5002.prefab | IG_Helmat_LV2 |
| 5003 | SWAT Helmet·헬멧 3 | Prefabs/Helmet_Lv3_5003.prefab | IG_Helmat_LV3 |

근거 CSV는 Assets/GameData/Armor/armor.csv와 Assets/GameData/Helmet/helmet.csv다. 원본 외형은 Assets/Third_Party_Assets/DuckovImported/GameObject에 있다.

ProtectionPreview.unity를 단독으로 열어 3개 등급을 비교한다. 방어구 루트는 ArmorSocket, 헬멧 루트는 HelmatSocket에 로컬 위치·회전 0과 스케일 1로 붙인다. 원본 외형 내부의 메시 배치 오프셋은 유지한다. 이 계층을 통해 장비는 해당 뼈의 움직임을 따라간다.

원작 스크립트와 Collider/Rigidbody를 제거한 외형 전용 프리팹이다. 자동 장착, 보호 효과, 월드 아이템 상호작용은 이 프리팹 자체가 담당하지 않는다. 기존 캐릭터 프리팹은 수정하지 않았다.

URP Lit 머티리얼로 알베도·노멀을 연결했다. 원본 금속·매끄러움 맵의 패킹은 변환하지 않았으므로 광택까지 완전히 동일한 복원은 아니다. 메시·텍스처는 정리한 DuckovImported 폴더를 참조하므로 해당 의존 에셋을 삭제하지 않는다.

생성 도구: Assets/Editor/ProtectionVisualPrefabBuilder.cs. 메뉴 Tools > Duck Equipment > Build Level 1-3 Protection. 기존 생성 결과를 덮어쓰지 않는다.

프리팹 생성·누락된 스크립트/메시/머티리얼 검사 및 정적 착용 화면을 확인했다. 애니메이션 중 관통, 장착·해제 외형 갱신, 내구도 연동의 인게임 검증은 별도 범위다.
