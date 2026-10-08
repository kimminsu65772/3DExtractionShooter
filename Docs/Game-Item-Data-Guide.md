# 게임용 아이템 데이터 적용 기록

작성일: 2026-10-02. 관련 기능: CORE-01(외부 데이터 파이프라인), ACT-02(조준), ACT-03(사격), ACT-04(탄약·장전), INV-01(인벤토리 칸), INV-02(장비), SURV-01(체력), UI-01(핵심 HUD).

## 확정한 규칙

- `C:/Users/admin/Desktop/에셋`의 원본 CSV는 수정하지 않는다. 게임에서 쓰는 열과 단위로 변환하여 `Assets/GameData`에 저장한다.
- 반동 계산과 직접 조정한 반동·힙파이어 수치는 유지한다. 원본의 반동·탄퍼짐 숫자는 단위와 계산 방식이 확인되지 않아 그대로 이식하지 않는다.
- ADS는 별도 탄퍼짐 설정을 갖는다. 초기값은 기존 힙파이어 값에 기존 `aimSpreadMultiplier`를 곱해 만든다. 자세는 즉시 바뀔 수 있지만 수치 보정은 `adsDuration`이 지난 뒤 적용한다.
- 샷건은 한 번에 탄약 1발을 소비하고 산탄 5개를 생성한다. 각 산탄의 기본 피해는 55다. `singleShotAngle=16`은 중심에서 한쪽까지 16도, 전체 32도다.
- 샷건은 0.4초마다 한 발씩 넣는다. 넣은 탄약은 장전 취소 뒤에도 유지한다. 한 발 이상 있으면 공격으로 장전을 중단하고 발사할 수 있다.
- 탄약은 Rusty=관통 0, Standard=1, AP=2다. 이번 데이터의 탄약 피해 배율은 모두 1이다. `(S)`는 권총용이며 소총탄·산탄과 호환되지 않는다.
- 회복 아이템은 2초 후 한 번 회복하고 아이템 1개를 소비한다. 의료 키트의 원본 내구도 기반 반복 사용은 도입하지 않는다.
- 방어구·헬멧은 등급을 저장하지만 내구도와 수리를 구현하지 않는다.
- 시작 시 무기 1번 슬롯이 선택되어 있다. 첫 무기가 그 칸에 들어오면 기존 장착 경로에서 바로 손에 든다. `PlayerEquipment.selectedWeaponSlotIndex=0`은 사용자 설정을 유지한다.

## CSV와 ID

| 파일 | 개수 | ID | Addressables 주소 |
| --- | ---: | --- | --- |
| `Assets/GameData/Weapons/weapons.csv` | 4 | 1001 AK-47, 1002 Glick, 1003 MP-155, 1004 MF | `data/weapons` |
| `Assets/GameData/Ammo/ammo.csv` | 9 | 2001~2003 소총탄, 2011~2013 권총탄, 2021~2023 산탄 | `data/ammo` |
| `Assets/GameData/Medicine/medicine.csv` | 4 | 3001~3004 | `data/medicine` |
| `Assets/GameData/Armor/armor.csv` | 3 | 4001~4003 | `data/armor` |
| `Assets/GameData/Helmet/helmet.csv` | 3 | 5001~5003 | `data/helmet` |

2001은 기존 Standard 소총탄 ID를 보존한다. 소총탄은 2002 Rusty·2003 AP이며, 권총탄과 산탄은 각 범위에서 Rusty·Standard·AP 순서다. 기존 ID에 연결된 무기 외형·발사음·VFX·탄약 아이콘 주소는 보존한다.

## 필드 변환

| 원본 정보 | 게임 필드 / 처리 |
| --- | --- |
| Name, Weight, MaxStack | displayName, unitWeight, maxStackCount |
| WeaponClass | 기존 weaponType·ammoType·animationStyle과 연결한다. CSV에 공통 ItemType 열을 중복 추가하지 않는다. |
| Damage, FireRate, MagazineCapacity | baseDamage, fireRate, magazineCapacity |
| ReloadTime | reloadDuration. Shotgun이면 reloadMode=SingleRound, 나머지는 Magazine |
| BulletSpeed, SoundRange | projectileSpeed, shotNoiseRadius |
| BulletsPerShot, SingleShotAngle | bulletsPerShot, singleShotAngle |
| ADSTime | adsDuration |
| MovementSpeedCoefficient, ADSMovementCoefficient | movementSpeedMultiplier, adsMovementSpeedMultiplier |
| HipFire·ADS 탄퍼짐, 원본 반동 | 원본 숫자는 보류한다. 기존 힙파이어·반동을 유지하고 ADS 4개 값은 기존 조준 배율에서 계산한다. |
| Range, OverRangeDamageMultiplier | 기존 optimalRange/effectiveRange/minDamageRange와 배율을 유지한다. 원본 한 숫자를 임의로 세 구간으로 변환하지 않는다. |
| HealHP | healAmount. useDuration은 합의한 2초 |
| BodyArmorBonus, HeadArmorBonus | 각각 ArmorDefinition·HelmetDefinition의 armorLevel |
| BackpackCapacityBonus | ArmorDefinition.backpackCapacityBonus. 빈 원본 값은 0 |
| Value, AssetFolderName, SourceURL/WikiURL | 런타임 Definition에서 제외 |
| MaxDurability, Durability Cost, Durabilty | 제외. 아이템 1개 단위로 소비하며 방어구 내구도도 없다. |
| BurstCount, FireMode, AimingRange | 제외. 적 점사 정책과 발사당 산탄 수를 혼동하지 않는다. |
| ColdProtectionBonus | 현재 체온 기능과 함께 보류 |

원본의 치명타·유도탄·폭발·디버프·연사 가속·변이체 전용 보정 등 현재 전투 계산에 없는 고급 옵션은 이번 변환에서 런타임 동작을 추가하지 않았다. 해당 기능을 도입할 때 별도로 정책과 계산 경계를 정해야 한다.

새 MF의 튜닝 반동·탄퍼짐·세 구간 사거리는 AK-47 값을 초기값으로 사용한다. MF 전용 외형·아이콘·발사음 주소를 가정하여 만들어 넣지 않았다. 이는 최종 밸런스 값이 아니다.

ADS 초기값은 기존 감각을 기준으로 만든 값이지 원본 게임의 공식을 재현한 값이 아니다. 특히 최대 탄퍼짐 제한을 ADS 전용 최대치로 적용하므로 누적 퍼짐의 끝부분은 예전의 공통 최대치 제한과 달라질 수 있다.

## 실행 흐름과 파일 책임

### 데이터 준비

`CombatDataCompositionRoot.Start()` → `CombatDataBootstrap.InitializeAsync()` → 각 CSV 로드 → `CsvTableLoader` → 각 `Generated...Binding` → Validator → Catalog 공개.

- `Assets/Scripts/Inventory/Item/ItemDefinition.cs`: 이름·ID·중첩 한도·무게·아이콘의 공통 정의다.
- 같은 폴더의 `MedicineDefinition.cs`, `ArmorDefinition.cs`, `HelmetDefinition.cs`: 회복량/사용 시간, 몸통 등급/추가 칸, 머리 등급을 각각 정의한다.
- `Assets/Scripts/DataPipeline/Catalog/ItemDefinitionCatalog.cs`: ID로 검증된 종류별 정의를 조회한다.
- 같은 폴더의 `ItemDefinitionValidator.cs`: 잘못된 ID·수량·회복량·등급 등을 데이터 준비 중 거부한다.
- `Assets/Scripts/DataPipeline/Csv/GeneratedMedicineBinding.cs`, `GeneratedArmorBinding.cs`, `GeneratedHelmetBinding.cs`: CSV 문자열을 각 정의의 필드 타입으로 바꾼다. 상속한 공통 필드도 생성 대상이다.
- `Assets/Scripts/DataPipeline/Editor/CsvBindingGenerator.cs`: `Tools/CSV/Generate All Item Bindings`에서 5종 바인딩을 다시 생성할 수 있다. 자동 생성 파일을 직접 편집하지 않는다.

### 인벤토리와 상자

- `Assets/Scripts/Inventory/Item/DefinitionInventoryItem.cs`: 공통 정의를 가리키면서 실제 수량을 갖는 물건이다. Definition의 최대 중첩 수량 안에서 더하고 뺀다.
- `Assets/Scripts/Inventory/InventoryContainer.cs`: 회복 스택의 병합·부분 이동과 종류+ID 구분을 처리한다. 방어구·헬멧은 중첩 1이므로 각각 별도의 객체로 보관한다.
- `Assets/Scripts/Loot/LootContainer.cs`: 회복 아이템·방어구·헬멧을 상자의 초기 아이템 목록에서 구성할 수 있다. 잘못된 ID나 칸 부족 시 중간 결과만 공개하지 않는다.
- `Assets/Scripts/UI/Inventory/InventoryUIController.cs`, `InventorySlotView.cs`: 새 종류의 이름·아이콘·수량·선택 상세 정보를 표시한다.

### 사격과 조준

- `Assets/Scripts/Combat/Weapons/WeaponDefinition.cs`: 장전 방식, ADS 시간/수치, 손에 든 무기의 이동 보정, 산탄 개수/각도를 추가한다.
- `Assets/Scripts/Combat/Weapons/WeaponAdsTiming.cs`: 같은 실제 무기에 조준을 유지한 시간을 확인한다. 조준 해제나 무기 교체 때 다시 시작한다.
- `Assets/Scripts/Player/PlayerAimController.cs`: 입력·행동 상태·UI 상태로 ADS 준비 완료 여부를 제공한다.
- `Assets/Scripts/Player/PlayerRecoilController.cs`, `WeaponSpreadUtility.cs`: 준비 완료 후 ADS 전용 탄퍼짐을 쓰며 기존 반동 계산은 유지한다.
- `Assets/Scripts/Player/PlayerController.cs`: 선택하여 손에 든 총의 이동 보정을 적용한다. 가방/비선택 칸의 무기는 이 보정의 대상이 아니다. 회피 거리는 변경하지 않는다.
- `Assets/Scripts/DataPipeline/Integration/ProjectileSpawnService.cs`: `TrySpawnShot()`에서 모든 산탄을 비활성 상태로 준비한 뒤 활성화한다. 대여/초기화 실패 시 모두 반환한다. 구형 `TrySpawn()`은 기존 한 탄환 용도로 유지한다.
- `Assets/Scripts/Player/PlayerAttackController.cs`, `Assets/Scripts/Enemy/Combat/EnemyAttackController.cs`: 성공한 한 번의 발사에 탄약·탄퍼짐·발사음·총구 효과를 한 번만 적용한다.
- `Assets/Scripts/Player/PlayerReloadController.cs`: 샷건 한 발씩 장전과 공격 중단을 처리한다.
- `Assets/Scripts/Enemy/Combat/EnemyWeaponLoadout.cs`, `EnemyAttackController.cs`: 적도 한 발씩 장전하되 기존 무한 예비 탄약 정책을 유지한다. 적이 장전 도중 공격으로 끊는 별도 정책은 추가하지 않았다.

### 회복 사용 API

`Assets/Scripts/Player/PlayerMedicineUseController.cs`의 `TryStartUse(DefinitionInventoryItem)`는 소유한 회복 아이템을 사용하는 진입점이다. 완료 전에는 수량과 체력을 변경하지 않고 2초 후 아이템 1개를 소비하여 `HealthComponent.Restore()`를 한 번 호출한다. 사용 중 공격·장전은 차단하며 이동은 허용한다. 회피·사망·무기 변경·소유권 상실은 사용을 취소한다.

이 컴포넌트를 씬에 자동 부착하거나 3~8번 퀵슬롯에 연결하지 않았다. 아이템을 클릭하면 사용하는 방식도 추가하지 않았다. 손에 든 소비품/퀵슬롯 시스템에서 호출하는 연결은 후속 작업이다.

## 아직 완료하지 않은 영역

- 새 무기의 실제 외형·총구·아이콘·발사음/VFX 주소 입력과 실물 장착 검증.
- 방어구·헬멧 장착 슬롯, 장착 시 CharacterStats의 방어 등급 적용, 방어구 추가 칸 반영. 현재는 정의·획득·보관·표시까지다.
- 3~8번 소비품 퀵슬롯 모델·손에 들기·사용 입력과 회복 API 연결.
- 전체 소지 무게 합산과 이동 제약. unitWeight가 생겼다고 무게 기능이 완료된 것은 아니다.
- 샷건의 실제 애니메이션 반복/중단 연결과 Unity 플레이 모드 검증. 한 발 장전 완료 때마다 Animator Trigger를 반복하는 기능은 추가하지 않았다.

## Unity에서 확인할 순서

1. 재컴파일을 마친 뒤 CombatDataCompositionRoot의 새 주소가 `data/medicine`, `data/armor`, `data/helmet`인지 확인한다. 새 CSV는 Combat Addressable 그룹에 TextAsset으로 등록했다.
2. 새 무기의 `weaponViewAddress`와 선택할 아이콘·음원·VFX 주소를 실제 등록 주소로 채운다. ID는 유지한다.
3. 튜토리얼 상자의 초기 목록에 Meds 3001, Armor 4001, Helmet 5001 등을 넣고 이름·수량·상세 화면과 가방 이동을 확인한다.
4. MP-155의 실제 외형 주소를 채운 뒤 한 발 소비로 5개 산탄이 나오고, 0.4초마다 한 발 장전되며, 공격 중단 후 이미 넣은 탄약이 남는지 확인한다.
5. ADS 완료 전후 탄퍼짐·이동속도를 확인한다. 조준 해제·무기 교체 때 대기 시간이 다시 시작되어야 한다.
6. 소비품 퀵슬롯 연결 때 PlayerMedicineUseController를 플레이어에 붙이고 사용 진입점에서 TryStartUse를 호출한다. 완료/회피 취소도 확인한다.

## 실행한 검사와 한계

- 런타임 및 UI 코드 컴파일: `dotnet build Tests/InventoryUI/Compile.csproj --no-restore -v quiet`, 오류 0 / 경고 0.
- 실제 CsvHelper → 실제 생성 바인딩 → 실제 Validator: CSV 5개, 항목 23개 및 확정 수치 정책 통과.
- `Tests/InventoryCore`: 76/76 통과. ADS 대기/초기화, 회복 2초/회피 취소, 샷건 한 발 장전/중단, 신규 아이템 병합과 기존 탄약 회귀 검사 포함.
- `Tests/WeaponEquipment`: 61/61 통과. 사용자 정책인 초기 1번 선택과 첫 무기 획득 즉시 장착도 포함.
- `Tests/ProjectileShot`: 10/10 통과. 풀 부족·컴포넌트 부족·활성화 예외 시 전체 반환, 수평/수직 조준 포함 4,000개 방향의 16도 범위, 산탄별 피해 55, 기존 단일 발사 호환성 확인.

위 동작 테스트의 Unity 오브젝트와 주변 컴포넌트는 테스트 대역이다. 실제 렌더링·충돌·풀 생명주기·애니메이션이 모두 통과했다는 의미는 아니다. Unity 배치 테스트와 플레이 모드를 이번 작업에서 실행하지 않았다.
