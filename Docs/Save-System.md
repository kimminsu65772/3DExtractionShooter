# DATA-01(저장/불러오기) — 저장 기반

## 확정 정책

- 로비에서는 창고 입출고와 장비·퀵슬롯 변경이 성공한 뒤 저장을 요청한다.
- 탐험 중 저장 요청은 사망·탈출의 결과를 확정한 뒤에만 한다.
- 탐험 중 종료하면 탐험 전 저장 상태로 복원한다.
- 주기적 저장, Deferred, 종료·일시정지 시 강제 저장은 없다.
- 로딩 씬이 저장 완료를 기다린 뒤 탐험 또는 로비로 전환한다. 저장 실패를 성공으로 취급하지 않는다.

## 이번 구현 범위

`Assets/Scripts/Save`에 기존 다섯 파일의 책임을 옮겼다. 원본 파일은 변경하지 않았다.
비동기는 원본과 같은 UniTask를 사용한다. UniTask 패키지는 직접 임포트할 예정이며 이번 작업에서 설치하지 않았다.

- `SaveData`: 버전·저장 시각만 가진 추상 기반. 실제 아이템/창고 데이터 정의는 다음 단계다.
- `SaveDataFactory.CreateNewData<T>()`: 해당 저장 타입의 초기 값과 새 프로젝트 버전 1을 사용한다.
- `SaveDataLoader.Load<T>()`: 파일을 읽고 버전을 검사한 뒤 해당 타입으로 변환한다.
- `SaveDataLoader.LoadBackup<T>()`: 호출자가 명시적으로 백업을 읽는다. 손상 시 자동 초기화나 자동 롤백은 하지 않는다.
- `SaveDataWriter.SaveFileAsync(json)`: 요청 순서대로 파일 쓰기 → 임시 파일 교체 → 이전 저장 백업.
- `SaveScheduler`: 한 번 생성해 씬 간 동일 객체를 유지하는 일반 C# 서비스. 씬이나 UI를 직접 제어하지 않는다.

## 호출 순서

1. 게임 흐름 담당자가 완료된 창고·장비·사망·탈출 결과를 저장 DTO로 변환한다.
2. `RequestSaveAsync(data, SavePolicy.Immediate)`를 호출하고 반환 UniTask를 기다린다.
3. 로딩 씬에서는 `FlushAsync()`를 기다린다. Soon 예약이 남아 있다면 대기를 끝내고 저장한다.
4. 성공 시 다음 씬으로 전환한다. 예외 발생 시 전환을 멈추고 오류를 표시한다.
5. 재시도는 `RetryAsync()`로 마지막 승인 데이터만 다시 저장하고 `FlushAsync()`로 완료를 확인한다.

`RequestSaveAsync`는 호출 시 JSON을 복사한다. DTO의 데이터를 이후 변경해도 기존 요청에 섞이지 않는다.
`FlushAsync`는 새로운 데이터를 읽거나 저장 요청을 생성하지 않는다. 탐험 진입 시 로비의 요청만 마무리한다.
`Soon`은 첫 요청부터 기본 5초 동안 연속 변경을 모은다. 즉시 저장이 기본이며 Soon 사용은 호출자가 선택한다.
쓰기 시작 전 모인 요청은 최신 스냅샷으로 합쳐지고, 쓰기 도중의 새 요청은 다음 쓰기로 처리한다.
`IsSaving`은 실제 쓰기 중인지, `HasUnsavedChanges`는 아직 저장되지 않은 승인 요청이 있는지를 나타낸다.
파일 쓰기 실패는 요청 UniTask와 `FlushAsync` 양쪽으로 전달된다. 자동 무한 재시도는 하지 않는다.
공유 완료 신호는 `UniTaskCompletionSource`로 만들어 요청자와 로딩 담당자가 함께 기다릴 수 있다.
요청과 대기는 Unity 메인 스레드에서 호출한다. 파일 쓰기만 `RunOnThreadPool`로 보내고 메인 스레드로 복귀한다.

## 아직 연결하지 않은 부분

- 구체적인 플레이어·창고 저장 타입 및 런타임 변환 계층.
- 로비/탐험에서 저장을 허용하는 이벤트 연결과 서비스 수명 관리.
- 로딩 씬, 실패 표시/재시도 UI, 씬 전환.
- 실제 아이템 잔탄·잔여량·내구도 및 퀵슬롯 참조 복원.

저장 경로는 조립 담당자가 결정해 Loader와 Writer에 동일한 절대 경로를 전달한다.
한 저장 파일에는 하나의 Writer와 Scheduler를 공유한다.
파일 구조의 버전 검사와 게임 데이터의 유효성 검사는 별개다. 아이템 검증은 DTO/변환 계층을 만들 때 추가한다.

## 검증 기록 — 2026-10-14, Task 구현 당시

- 실제 런타임 소스 여섯 파일을 .NET Standard 2.1 대상으로 컴파일했다.
- 별도 임시 실행 환경에서 파일 저장/불러오기, 요청 순서, Soon 병합, 동시 완료 대기, 백업을 검사했다.
- 파일을 잠가 쓰기를 실패시킨 뒤 요청/Flush에 예외가 전달되고 기존 파일이 보존되는지 검사했다.
- 재시도가 마지막 승인 스냅샷만 저장하며, DTO의 후속 변경이나 단순 Flush가 새 저장을 만들지 않는지 검사했다.
- 잘못된 JSON과 누락/미지원 버전이 거부되고 원본 파일이 남는지 검사했다.
- 위 실행 검사는 통과했다. 임시 검증 도구와 출력은 Git 제외 경로인 `Temp/SaveInfrastructureVerification`에 둔다.
- Unity MCP로 컴파일을 요청했으나 에디터 재로딩과 연결 응답 지연으로 신규 파일의 에디터 컴파일 완료는 확인하지 못했다. Unity 인게임 검증도 수행하지 않았다.

위 기록은 UniTask 복원 이전 결과다. UniTask 버전의 재검증 결과와 혼동하지 않는다.

## UniTask 복원 후 검증 — 2026-10-14

- Writer·Scheduler의 반환 타입, 완료 신호, 대기, 작업 스레드 실행을 UniTask로 맞췄다.
- 원본 팀 프로젝트의 기존 UniTask.dll을 별도 검증 프로젝트에서만 참조해 런타임 소스 전체의 .NET Standard 2.1 컴파일을 확인했다. 경고와 오류는 없었다.
- 현재 프로젝트에 UniTask 패키지를 대신 설치하거나 원본 DLL을 복사하지 않았다.
- UniTask 버전의 Unity PlayerLoop 실행 검증은 패키지 임포트 후 진행해야 한다. 이전 Task 실행 검사를 UniTask 실행 통과로 간주하지 않는다.

API 확인: [UniTask 실행 구현](https://github.com/Cysharp/UniTask/blob/master/src/UniTask/Assets/Plugins/UniTask/Runtime/UniTask.Run.cs), [공유 완료 소스 구현](https://github.com/Cysharp/UniTask/blob/master/src/UniTask/Assets/Plugins/UniTask/Runtime/UniTaskCompletionSource.cs).
