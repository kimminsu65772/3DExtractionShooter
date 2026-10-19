# 무작위 장비 입력 리소스와 반동

관련 기능: ACT-03(사격), INV-02(장비), ENEMY-02(적 종류 확장), CORE-01(외부 데이터 파이프라인), CORE-02(리소스 로딩 및 수명 관리).

## 이번에 끝낸 범위

- 기존 4종 포함 무기 16종, 방어구 9종, 머리 장비 18종의 게임용 외형·아이콘·Addressables 연결과 CSV 등록.
- 원본 DLL/게임 로직 없이 외형 의존성만 복사했다. 원본 폴더는 변경하지 않았다.
- 기존 무기 4종의 외형·아이콘과 반동 이외 수치는 보존했다. 신규 무기 12종, 보호구 21종을 추가했다.
- 새로 가져온 원본 외형의 Missing Script 60개를 Unity API로 제거했다. 최종 외형의 메시·머티리얼·총구와 아이콘 주소를 검사했다.
- 보호구의 소수 방어 수치와 원본 최대 내구도를 등록했다. quality는 레어도로 원본 기록에 남기며 내구도나 방어 수치와 혼동하지 않는다.

## 위치와 ID

- 무기 외형: `Assets/Art/Weapons/Duckov/Prefabs/`, 주소 `duckov/weapons/{weaponId}`.
- 보호구 외형: `Assets/Art/Equipment/DuckProtection/Prefabs/`, 주소 `duckov/armor/{itemId}`, `duckov/helmets/{itemId}`.
- 새 원본 의존성: `Assets/Art/Equipment/DuckovRandom/Source/`.
- CSV: `Assets/GameData/Weapons/weapons.csv`, `Assets/GameData/Armor/armor.csv`, `Assets/GameData/Helmet/helmet.csv`.
- 원본 GUID·레어도·수치·위키 주소: `.codex/data/random-equipment-source.json`.

| 무기 ID | 외형 |
| --- | --- |
| 1001–1004 | 기존 AK-47 / Glick / MP-155 / MF |
| 1005 | 수제 사냥총 |
| 1006 | AK-103 |
| 1007 | AK-74U |
| 1008 | ADAR 2-15 |
| 1009 | StG 77 |
| 1010 | DT MDR-556 |
| 1011 | MP7 |
| 1012 | UP-45 |
| 1013 | Vector |
| 1014 | TOZ-66 |
| 1015 | TT-33 |
| 1016 | PM |

방어구는 4001–4009, 머리 장비는 5001–5018이다. 표시 이름이 다른 원본 파일명(AK102/AK103, UAK45/UP-45 등)은 원본 GUID로 식별하고 위키 표시명을 사용했다. 활은 제외했다.

## 반동: 문제 → 아이디어 → 표현 → 한계

기존에는 반동을 즉시 더하고 방아쇠를 누르는 동안 회복을 막았다. 방향 범위가 비대칭인 원본 반동, 짧게 밀리는 느낌, 발사 후 회복 지연을 표현하지 못했다. 원본 값을 기존 12배 화면 배율에 그대로 넣는 것도 단위가 맞지 않았다.

매 발의 총량을 뽑아 적용 시간에 나누어 더하고, 마지막 실제 발사 이후 회복을 시작한다. PlayerRecoilController의 Update와 AddRecoil은 동일한 마지막 계산 시각을 공유하여 같은 시간 구간을 중복 계산하지 않는다. 별도 코루틴이나 프레임별 할당은 없다.

| 원본 | WeaponDefinition / CSV | 의미 |
| --- | --- | --- |
| RecoilScaleV | verticalRecoilPerShot | 기준 화면 수직 강도 |
| RecoilScaleH | horizontalRecoilPerShot | 기준 화면 수평 강도 |
| RecoilVMin / VMax | recoilVerticalMin / Max | 매 발 수직 무작위 배율 |
| RecoilHMin / HMax | recoilHorizontalMin / Max | 매 발 수평 무작위 배율, 음수/비대칭 허용 |
| RecoilTime | recoilDuration | 적용 시간, 실제로는 발사 간격 이하 |
| RecoilRecoverTime | recoilRecoveryDelay | 마지막 발사 이후 회복 시작 지연 |
| RecoilRecover | recoilRecoverySpeed | 기준 화면에서 초당 회복 픽셀 |

기준 화면은 높이 1440px이다. `현재 화면 높이 / 1440`을 표시와 마우스 상쇄의 단위 변환에 함께 쓴다. 예전 recoilScreenMultiplier=12는 제거했다. maxRecoilOffset 열은 호환을 위해 보존하지만 기존처럼 누적 제한에는 쓰지 않는다.

읽을 순서:

1. WeaponDefinition의 반동 필드: 어떤 입력을 받는가.
2. PlayerAttackController의 실제 발사 성공 후 AddRecoil 호출: 언제 반동이 시작되는가.
3. PlayerRecoilController.AddRecoil: 방향·강도 추첨과 적용 시간 예약.
4. AdvanceRecoilToNow → AdvanceRecoil: 시간 구간별 적용 및 회복.
5. ConsumeScreenRecoil: 반대 마우스 입력을 먼저 반동 상쇄에 사용하는 기존 방식.

원본과 완전히 같지는 않다. 기존 ADS/캐릭터 배율, 축별 마우스 상쇄는 프로젝트의 동작을 유지한다. 원본의 커서 경계 보정·일정량 다른 방향으로 움직였을 때의 회복 취소·총 모델 자체 후퇴 연출은 추가하지 않았다. 매우 낮은 프레임률에서 다음 발이 먼저 들어오면 이전 발의 미적용량을 먼저 반영하여 잃지 않게 했다. 무기 교체는 정의뿐 아니라 실제 WeaponInstance로도 구분하므로 같은 종류의 두 총 사이에서도 이전 반동을 넘기지 않는다.

## 나머지 데이터 환산과 미지원

### 2026-10-17 레어도 런타임 반영

- 무기 16종·방어구 9종·헬멧 18종의 원본 `quality`를 각 CSV의 `rarity` 열에 반영했다. `WeaponDefinition.rarity`와 `ProtectionDefinition.rarity`에서 읽는다.
- 방어 수치·내구도와 별개인 0 이상의 정수다. TT-33의 원본 0도 유효하며, 이전 CSV가 열을 생략하면 기본값 0을 사용한다.
- 바인딩 재생성과 검증까지 완료했다. UI 슬롯 색상표와 랜덤 장비 추첨은 아직 연결하지 않았다. 상세: `.codex/handoffs/2026-10-17-item-rarity.md`.

- 신규 총의 발사 속도·탄창·장전 시간·탄속·소음·무게·ADS 시간·이동 보정은 원본 수치를 사용한다. 원본 탄종은 현재 Rifle/Pistol/Shotgun 체계로 대응한다. SMG는 Pistol 탄약을 사용한다.
- 신규 총의 기본/최대/증가 탄퍼짐은 원본 정규화 값 × ScatterFactor × 0.5로 반각 단위로 옮겼다. 회복은 원본 ScatterRecover × ScatterGrow × ShootSpeed에 같은 각도 환산을 적용한다. 원본은 수평 분포, 현재는 원형 탄퍼짐이므로 분포까지 같은 재현은 아니다.
- 신규 산탄총의 baseDamage는 원본 Damage / ShotCount, singleShotAngle은 원본 ShotAngle / 2다. 현재 투사체는 개별 탄환에 baseDamage를 적용하기 때문이다. 기존 MP-155는 요청하지 않은 밸런스 변경을 피하려고 기존 55/5발/반각16을 보존했다. 이 값은 원본의 총 피해 분배와 달라 별도 검토 대상이다.
- 사거리의 3구간 체계는 원본의 연속 거리 감쇠와 다르다. 신규 총은 R=원본 BulletDistance에 대해 0.495R/R/1.5R, 배율 1/1/DmgOverDistance로 대응한 프로젝트용 근사다. 기존 4종 사거리는 보존했다.
- headMultiplier, moveSpreadMultiplier, projectileRadius와 풀 프리웜은 현재 프로젝트 관례다. 원본 전체 전투 재현으로 표현하지 않는다.
- 공용 주소는 `projectiles/bullet`, `weapons/muzzleVfx`, `rifle/shot`이다. 기존 hitVfxAddress가 비어 있어 신규 총도 비워 두었다. 존재하지 않는 공용 주소를 만들지는 않았다.
- 방한, 청각, 야간 시야, 에너지 소비 등 보호구의 부가 능력은 원본 기록에만 보존했다. 외형 등록이 해당 능력 구현을 의미하지 않는다.
- 무작위 SO 장비 추첨·의료품 지급은 아직 연결하지 않았다. 현재 스폰용 Ranged/USEC/분대에는 EnemyProtectionView가 없어 보호구 외형 연결도 후속 작업이다. 인벤토리/드롭은 기존 장비 인스턴스를 유지하는 구조를 변경하지 않았다.

## 확인한 것과 직접 확인할 것

Unity 편집 상태에서 신규 타입 컴파일, 자동 생성 바인딩 일치, 실제 CSV 로더의 16/9/18행 변환·검증, 43종의 외형/아이콘 주소·메시·머티리얼·총구 방향을 확인했다.

반동은 즉시값0 → 적용 중간50 → 적용 완료100 → 회복 지연 유지100 → 회복90 → 종료0의 수학 검사를 통과했다. 30/60/144 프레임 간격의 같은 시각 결과가 일치했다. 적용 시간0, 마우스 상쇄, 빈손 초기화 및 잘못된 반동 범위/지연 데이터 거절도 확인했다. 플레이 모드는 실행하지 않았다.

직접 플레이에서는 단발/연사 후 회복 시작, ADS/비ADS, 반대 마우스 제어, 빈손·다른 무기 전환, 신규 총 파지와 사격 방향을 확인한다. 기존 콘솔의 Inspector/그림자 관련 경고나 예외를 이번 작업으로 모두 해결했다고 보지 않는다.

## 출처

- 로컬 추출본 `AuxiliaryFiles/GameAssemblies/TeamSoda.Duckov.Core.dll`의 InputManager/ItemAgent_Gun 동작을 분석했다. 원본 코드를 프로젝트에 복사하지 않고 기존 구조에 맞게 작성했다.
- 원본 아이템 프리팹의 상수·스탯·modifier와 각 아이템 위키를 대조했다. 43종 모두 위키 최대 내구도/무게와 원본 상수가 일치했다. 행별 URL은 원본 기록 JSON에 있다.
- [Scav](https://escapefromduckov.net/ko/wiki/creatures/scav), [Scav-low](https://escapefromduckov.net/ko/wiki/creatures/scav-low), [USEC](https://escapefromduckov.net/ko/wiki/creatures/usec-hiddenwarehouse).
