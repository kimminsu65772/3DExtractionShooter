# 적 스폰 장착 표

관련 기능: ENEMY-02(적 종류 확장), INV-02(장비). 기준일: 2026-10-18.

## 동작 규칙

탐험 시작 시 각 적마다 한 번 추첨한다. 무기·방어구·헬멧은 독립적으로 추첨하며, SO는 공유 확률만 보관한다. 실제 물건·잔탄·내구도는 각 적의 기존 Loadout이 소유하고 처치 시 같은 인스턴스를 전달한다.

- 무기는 풀의 원래 가중치를 사용한다. 일반 넝마꾼의 활(ID 357)만 제외하고 총합 48로 재정규화했다.
- 중복 아이템은 사용자 결정에 따라 첫 항목 하나만 사용한다. 창고 구역 용병의 MP7 가중치 8을 두 번 더하지 않으므로 총합은 50, MP7은 16%다.
- 보호구는 태그·필수 태그·제외 태그·레어도를 원본 컬렉션과 대조했다. 레어도 가중치를 해당 레어도의 조건 일치 아이템 수로 나누어 후보 가중치를 만든다. 원본부터 조건에 맞는 아이템이 없는 레어도는 후보를 만들지 않고, 실제 존재하는 후보의 가중치를 정규화한다. 예: Western 헬멧에는 조건에 맞는 레어도 2 후보가 없다.
- 표의 아이템 확률은 해당 슬롯의 착용에 성공했을 때의 조건부 확률이다. 실제 전체 출현율은 착용 확률 × 아이템 확률이다. 레어도는 방어 수치와 별개다.
- 필요한 무기·방어구·헬멧 후보 누락은 0개다. 제로존 전용 용병의 누락 MP5는 ID 1017로 추가했다.
- 탄약은 현재 프로젝트의 호환 표준탄(소총 2001, 권총/기관단총 2012, 산탄 2022)으로 탄창을 채운다. 무기·보호구 내구도 무작위화, 가방·의료품·기타 전리품 확장은 이번 범위가 아니다.

## 생성 경로

StageEnemySpawner.Prepare → EnemyEquipmentLoadout.PrepareSpawn → 초기 장비 ID 설정 → 활성화 → 기존 Weapon/Protection Loadout.OnEnable 초기화 순서다. 같은 개체에 PrepareSpawn을 다시 호출해도 재추첨하지 않는다. 잘못된 ID·탄약·중복 후보는 실패로 처리하고 재추첨/묵시적 후보 제외를 하지 않는다.

| 구역/역할 | 장착 표 |
| --- | --- |
| Cap_EastRoad, Cap_ToBridge, Wild, Wild_BackToBunker | ScavLow |
| Cap_UnderBridge, Cap_UnderMountain, Wild_Higher | Scav |
| 독립 USEC(MoveUsec) | USECZero |
| 분대장 1명 | PatoChapo |
| 분대원 3명 | USECWarehouse |

넝마꾼 구역 대응은 원본 씬 randomPresetInfos의 GUID를 추적했다. 현재 생성 수량·위치·확률은 변경하지 않았다. ToBridge는 기존 현재 범위대로 근접병 대신 원거리 저등급 표를 사용한다. MoveUsec의 원본은 Warehouse 프리셋이지만, 이번 요청의 제로존 전용/창고 구역 분대원 구분에 따라 독립 USEC에는 USECZero를 적용했다. ScavAK74는 표만 준비했으며 기존 미연결 단일 포인트 2곳을 새로 활성화하지 않았다.

## 일반 넝마꾼 (Scav)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/scav), 원본 `MonoBehaviour/EnemyPreset_Scav.asset`, GUID `0e24ccea33e924f449091d1e9f470ffe`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1005 | Homemade Shotgun | 20.8333% |
| 1003 | MP-155 | 20.8333% |
| 1002 | Glick | 20.8333% |
| 1001 | AK-47 | 10.4167% |
| 1006 | AK102 | 2.0833% |
| 1007 | AK74U | 16.6667% |
| 1008 | ADAR | 8.3333% |

### 방어구 · 착용 50%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 4001 | Armor Lv1 | 98.0392% |
| 4006 | ArmorLV3_2 | 1.9608% |

### 헬멧 · 착용 30%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 5001 | Motorbike Helmet | 36.5854% |
| 5012 | HelmatLV1_2 | 36.5854% |
| 5002 | Steel Helmet | 2.7875% |
| 5006 | Pot | 2.7875% |
| 5007 | Cow Mask | 2.7875% |
| 5008 | Cowboy Hat， 牛仔帽 | 2.7875% |
| 5011 | Helmat_Egg | 2.7875% |
| 5013 | HelmatLV2_2 | 2.7875% |
| 5018 | Helmat_HorseMask | 2.7875% |
| 5009 | Pumpkin | 2.4390% |
| 5010 | Head Lantern | 2.4390% |
| 5014 | HelmatLV3_2 | 2.4390% |

## 저등급 넝마꾼 (ScavLow)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/scav-low), 원본 `MonoBehaviour/EnemyPreset_Scav_low.asset`, GUID `1734986a6252ca24794b50a588744cbb`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1014 | TOZ-66 | 23.0769% |
| 1015 | TT-33 | 38.4615% |
| 1016 | PM | 38.4615% |

### 방어구 · 착용 0%

미착용.

### 헬멧 · 착용 0%

미착용.

## AK-74U 전용 넝마꾼 (ScavAK74)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/scav-low-ak74), 원본 `MonoBehaviour/EnemyPreset_Scav_low_ak74.asset`, GUID `c5f99f109aeec244d8d1685a46832183`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1007 | AK74U | 100.0000% |

### 방어구 · 착용 0%

미착용.

### 헬멧 · 착용 0%

미착용.

## 제로존 전용 용병 (USECZero)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/boss-shorteagle-elete), 원본 `MonoBehaviour/EnemyPreset_Boss_ShortEagle_Elete.asset`, GUID `64d5a3041e15c3545bdd1f258a754482`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1004 | MF | 19.2308% |
| 1011 | MP7 | 15.3846% |
| 1012 | UAK45 | 15.3846% |
| 1003 | MP-155 | 15.3846% |
| 1017 | MP5 | 19.2308% |
| 1013 | Vector | 15.3846% |

### 방어구 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 4002 | Armor Lv2 | 84.7458% |
| 4003 | Armor Lv3 | 7.0621% |
| 4009 | ArmorLV3_3 | 7.0621% |
| 4004 | Armor Lv4 | 0.5650% |
| 4007 | ArmorLV4_2 | 0.5650% |

### 헬멧 · 착용 30%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 5003 | SWAT Helmet | 46.2963% |
| 5015 | HelmatLV3_UN | 46.2963% |
| 5004 | Sight PRO | 3.7037% |
| 5016 | HelmatLV4_2 | 3.7037% |

## 창고 구역 용병 · 분대원 3명 (USECWarehouse)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/usec-hiddenwarehouse), 원본 `MonoBehaviour/EnemyPreset_USEC_HiddenWareHouse.asset`, GUID `2cb5bd6729c3c964eb53cb2008700c59`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1004 | MF | 20.0000% |
| 1009 | StG 77 | 20.0000% |
| 1010 | DT MDR 5.56 | 4.0000% |
| 1011 | MP7 | 16.0000% |
| 1012 | UAK45 | 16.0000% |
| 1003 | MP-155 | 16.0000% |
| 1013 | Vector | 8.0000% |

### 방어구 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 4002 | Armor Lv2 | 82.1918% |
| 4003 | Armor Lv3 | 6.8493% |
| 4009 | ArmorLV3_3 | 6.8493% |
| 4004 | Armor Lv4 | 1.3699% |
| 4007 | ArmorLV4_2 | 1.3699% |
| 4005 | Armor Lv5 | 0.6849% |
| 4008 | ArmorLV5_2 | 0.6849% |

### 헬멧 · 착용 30%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 5003 | SWAT Helmet | 38.4615% |
| 5015 | HelmatLV3_UN | 38.4615% |
| 5004 | Sight PRO | 7.6923% |
| 5016 | HelmatLV4_2 | 7.6923% |
| 5005 | SCIFI MASK | 3.8462% |
| 5017 | HelmatLV5_2 | 3.8462% |

## 꼬마덕 · 분대장 (PatoChapo)

출처: [덕위키](https://escapefromduckov.net/ko/wiki/creatures/boss-shorteagle), 원본 `MonoBehaviour/EnemyPreset_Boss_ShortEagle.asset`, GUID `94241e3344515a84aa320f5f83032700`.

### 무기 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 1001 | AK-47 | 100.0000% |

### 방어구 · 착용 100%

| 프로젝트 ID | 장비 | 착용 성공 시 확률 |
| --- | --- | ---: |
| 4003 | Armor Lv3 | 100.0000% |

### 헬멧 · 착용 0%

미착용.

## 레이어와 검증

일반 원거리 적·USEC·분대 그룹 프리팹 및 모든 대원의 자식 계층은 Enemy(8)다. 기존 Default였던 분대 그룹 루트를 수정했다. 보호구는 EnemyProtectionView에서 캐릭터 레이어를 따른다. 조사한 장비 외형 44종은 Collider가 없어 조준·탄환 물리 검사에 직접 참여하지 않는다. 외형 레이어 변경을 위해 공용 무기 풀을 수정하지 않았다.

실제 CSV 로더(무기 17/방어구 9/헬멧 18), 6개 표의 후보·가중치·호환 탄약, 표마다 5,000회 추첨, 재추첨 방지, 역할별 연결, 보호구의 동일 피격·드롭 인스턴스, 프리팹 레이어/스크립트 참조를 편집 모드에서 검증했다. 실제 Addressables 로딩→스폰→사격·처치 플레이 검증은 아직 수행하지 않았다. 신규 MP5 포함 외형 44종의 메시·총구·머티리얼·주소 검사는 통과했다.
