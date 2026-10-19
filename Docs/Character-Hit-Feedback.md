# ACT-07(피드백): 캐릭터 피격 발광과 FX

## 연결 방법

1. 더미의 `HealthComponent`와 같은 오브젝트 또는 외형 자식에 `CharacterHurtFeedback`을 추가한다.
2. `Health`에 해당 더미의 체력 컴포넌트를 지정한다. 같은 오브젝트 또는 부모에 있으면 자동으로 찾는다.
3. `Target Renderers`에는 반짝일 외형의 렌더러를 지정한다. 비워 두면 자식 MeshRenderer/SkinnedMeshRenderer를 Awake에서 한 번 수집한다. 무기까지 자식으로 들어 있다면 몸체 렌더러만 직접 지정한다. 파티클·라인·트레일 렌더러는 제외한다.
4. `Hit Fx`에는 `Assets/Art/FX/CharacterHit/HitFx_Blood.prefab`, `Hit Fx No Blood`에는 같은 폴더의 `HitFx_NoBlood.prefab`을 연결한다. FX를 비워도 발광은 동작한다. `Use Blood Fx`를 끄면 깃털 효과를 선택한다.
5. 이벤트를 별도로 등록할 필요는 없다. OnEnable에서 `HealthComponent.OnDamageTaken`을 구독하고 OnDisable에서 해제한다.

## 처리 흐름과 비용

- 유효한 피해 → 피격 강도 1 → 즉시 발광 및 hitPoint에서 FX 생성 → Update에서 unscaledDeltaTime × 8만큼 감쇠 → 원래 프로퍼티 블록 복원.
- 기본 감쇠 시간은 약 0.125초다. 연속 피격은 코루틴을 겹치지 않고 피격 강도를 다시 1로 설정한다.
- 체력, 무적, Renderer.enabled, 이동, 애니메이터에는 관여하지 않는다. 체력 회복 이벤트는 피격 효과를 발생시키지 않는다.
- 머티리얼 슬롯별 MaterialPropertyBlock을 캐시한다. 피격마다 머티리얼을 복제하지 않는다.
- URP Lit/Simple Lit에서 발광 키워드가 꺼져 있으면 Awake에서 해당 슬롯의 런타임 머티리얼만 한 번 복제한다. 기본 발광은 검은색으로 두고 컴포넌트 파괴 시 원래 머티리얼을 복원하고 복제본을 제거한다. 공유 머티리얼 에셋은 수정하지 않는다.
- MPB 사용은 SRP Batcher의 이점을 제한할 수 있다. FX는 원본처럼 Instantiate 방식이며 원본 수명 2.2초 후 기존 `ParticleEffectLifetime`으로 제거한다. 대규모 동시 피격 최적화나 풀 통합은 이번 범위가 아니다.

## 원본과의 차이

- 원본 `HurtVisual`은 `_HurtValue`를 제어하지만 AssetRipper가 내보낸 SodaCharacter 셰이더는 색상 처리 부분이 없는 DummyShader다. 따라서 재생 제어 원리를 유지하고, 현재 셰이더의 발광색과 발광 마스크를 MPB로 제어하는 호환 구현으로 대체했다. 원본 셰이더의 정확한 색 혼합식까지 복원한 것은 아니다.
- 현재 지원 대상은 MK Toon(`MK/Toon/...`), URP Lit/Simple Lit, 프로젝트의 `Restored Pato Chapo Character`다. 지원하지 않는 셰이더는 초기화 시 경고하고 해당 슬롯만 제외한다.
- 원본 FX의 파티클 설정과 메시·텍스처를 재사용하되 다섯 머티리얼은 실제 URP 패키지의 Particles/Unlit 기반 호환본으로 새로 만들었다. 원본 전용 디더·라이팅 표현은 동일하지 않다. 기존 원본 의존 에셋은 덮어쓰지 않았다.
- `DamageInfo`에는 충돌 법선이 없으므로 `-hitDirection`을 바깥 방향으로 근사한다. 충돌점은 실제 `hitPoint`를 사용한다. 임의로 DamageInfo를 만드는 테스트에서는 두 값을 함께 채운다.
- 원작 오디오 이벤트 프록시와 수명용 Missing Script를 FX 복사본에서 제거했다. 원작 사운드 프레임워크, DLL, 사망 FX는 가져오지 않았다.

## 직접 확인할 항목

- 단발 피격 후 원래 색으로 돌아오는지, 연속 피격에서도 색이 남지 않는지.
- 같은 머티리얼을 쓰는 다른 더미까지 동시에 반짝이지 않는지.
- 피격 도중 비활성화했다가 다시 켜도 원래 색인지.
- 혈흔/깃털 FX가 충돌 위치에 생성되고 2.2초 뒤 사라지는지.
- 방어구 장착 더미에서도 실제 피해를 받았을 때 발광하는지. 회복만으로는 발광하지 않는지.

구현 과정에서는 컴파일과 에셋 참조만 검사한다. 더미·씬·캐릭터 프리팹에는 컴포넌트를 붙이지 않으며 Play Mode 테스트는 진행하지 않는다.
