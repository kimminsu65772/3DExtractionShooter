using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 에셋의 종류를 나타내는 열거형이다.
    // 기존 enum 값의 직렬화 순서를 보존하기 위해 WeaponView는 마지막에 추가한다.
    public enum AssetKind { Projectile, MuzzleVfx, HitVfx, AudioClip, AudioEmitter, WeaponView }

    // 파이프라인의 상태를 나타낸다.
    // 외부 코드에서는 Ready 상태가 될 때까지 기다렸다가 데이터를 요청해야 한다.
    // Failed 상태는 파이프라인이 더 이상 정상적으로 동작하지 못함을 나타내고
    // ShuttingDown 상태는 파이프라인이 종료 중임을 나타낸다.
    // Disposed 상태는 파이프라인이 완전히 종료되어 더 이상 접근할 수 없음을 나타낸다.
    public enum PipelineState { Uninitialized, LoadingData, Validating, LoadingAssets, Prewarming, Ready, Failed, ShuttingDown, Disposed }

    // 풀에서 인스턴스를 빌릴 때 인스턴스가 부족한 경우의 정책을 나타낸다.
    // Reject: 풀에서 인스턴스를 빌릴 수 없으면 요청을 거부한다.
    // GrowWithinLimit: 풀에서 인스턴스를 빌릴 수 없으면 풀의 최대 활성 인스턴스 수를 초과하지 않는 범위에서 새 인스턴스를 생성한다.
    public enum PoolOverflowPolicy { Reject, GrowWithinLimit }

    // CSV 데이터 처리 과정에서 발생한 문제를 나타낸다.
    public sealed class DataIssue
    {
        public string Source; // 파일/Addressable 주소
        public int Record; // 논리 레코드 번호. 인용부호 안 개행과 물리 줄을 구분한다.
        public string Column;
        public string RawValue;
        public string Message;
    }

    // CSV 데이터 처리 과정에서 발생한 문제를 수집하는 클래스다.
    // 해당 클래스에 있는 오류 목록을 통해 데이터 처리 과정에서 발생한 문제를 확인하고
    // CSV 내 문제 위치를 추적할 수 있도록 한다.
    public sealed class DataReport
    {
        private readonly List<DataIssue> _issues = new List<DataIssue>();

        public bool HasErrors => _issues.Count > 0;
        public IReadOnlyList<DataIssue> Issues => _issues;
        public void Add(DataIssue issue)
        {
            // TODO S01: 원본 위치와 오류를 보존. 보고 개수/문자 길이 상한을 정한다.
            if (issue == null)
                return;

            _issues.Add(issue);
        }
    }

    // CSV 문서의 한 행에 해당하는 단위다.
    public sealed class CsvRecord
    {
        public int Number; // 행 번호
        public string[] Cells; // 행을 구성하는 셀 값 배열.
    }

    public sealed class CsvDocument
    {
        public string Source; // CSV 파일 경로
        public string[] Headers; // CSV 헤더 목록
        public IReadOnlyList<CsvRecord> Records; // CSV 행 목록
    }

    // CSV 문서를 읽어 CsvDocument 객체로 변환하는 인터페이스다.
    public interface ICsvRecordReader
    {
        CsvDocument Read(System.IO.TextReader reader, string source, DataReport report);
    }

    // CSV 행을 특정 타입으로 바인딩하는 인터페이스다.
    // 이 때 바인딩의 기준은 CSV 헤더 이름이다.
    public interface ICsvBinding<T>
    {
        // CSV에 존재할 수 있는 전체 헤더 목록
        IReadOnlyList<string> ExpectedHeaders { get; }

        // CSV에 반드시 존재해야 하는 헤더 목록
        IReadOnlyList<string> RequiredHeaders { get; }

        // 문자열 행을 특정 타입으로 바인딩한다.
        bool TryBind(
            CsvRecord row,
            IReadOnlyDictionary<string, int> header,
            DataReport report,
            out T result);
    }

    // 타입 변환 후 값의 의미를 검증하는 인터페이스다.
    public interface IRowValidator<T>
    {
        void Validate(T row, int record, DataReport report);
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvColumnAttribute : Attribute
    {
        public string Name { get; set; } // 데이터 구조에서 매핑할 CSV 헤더 이름
        public bool Optional { get; set; } // 누락/빈 셀 허용 여부
    }

    // 공개 필드지만 CSV 바인딩에서 제외하고 싶은 경우에 사용한다.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvIgnoreAttribute : Attribute { }

    // weaponDefinition에서 정의한 무기 정보를 실제 사용할 게임 리소스의 ID와 매핑한 런타임 무기 정보다.
    public sealed class RuntimeWeapon
    {
        public WeaponDefinition Definition { get; }

        public int WeaponId => Definition.weaponId;

        // 문자열 address를 런타임에서 정수형 ID로 변환한 값이다.
        public int ProjectileId { get; }
        public int WeaponViewId { get; }
        public int MuzzleVfxId { get; }
        public int HitVfxId { get; }
        public int FireSoundId { get; }

        public RuntimeWeapon(
            WeaponDefinition definition,
            int projectileId,
            int weaponViewId,
            int muzzleVfxId,
            int hitVfxId,
            int fireSoundId)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));

            if (projectileId < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileId));

            if (weaponViewId < 0)
                throw new ArgumentOutOfRangeException(nameof(weaponViewId));

            if (muzzleVfxId < 0)
                throw new ArgumentOutOfRangeException(nameof(muzzleVfxId));

            if (hitVfxId < 0)
                throw new ArgumentOutOfRangeException(nameof(hitVfxId));

            if (fireSoundId < 0)
                throw new ArgumentOutOfRangeException(nameof(fireSoundId));

            ProjectileId = projectileId;
            WeaponViewId = weaponViewId;
            MuzzleVfxId = muzzleVfxId;
            HitVfxId = hitVfxId;
            FireSoundId = fireSoundId;
        }
    }

    // 풀링 정책을 나타내는 클래스다.
    // 풀링 정책은 풀의 종류와 풀링 대상 에셋의 종류, 풀의 초기화 시점과 최대 활성 인스턴스 수, 최대 유지 인스턴스 수, 인스턴스 부족 시 정책을 정의한다.
    public sealed class PoolPolicy
    {
        public AssetKind Kind { get; }
        public int PrewarmCount { get; }
        public int MaxActive { get; }
        public int MaxRetained { get; }
        public PoolOverflowPolicy Overflow { get; }

        public PoolPolicy(
            AssetKind kind,
            int prewarmCount,
            int maxActive,
            int maxRetained,
            PoolOverflowPolicy overflow)
        {
            // 이 때 오디오 에셋은 별도로 관리되므로 풀링 대상으로 지정하지 않는다.
            if (kind == AssetKind.AudioClip)
            {
                throw new ArgumentException("AudioClip은 풀링 대상이 아닙니다.", nameof(kind));
            }
            if (prewarmCount < 0)
                throw new ArgumentOutOfRangeException(nameof(prewarmCount));
            if (maxActive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxActive));
            if (maxRetained <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetained));
            if (prewarmCount > maxActive)
                throw new ArgumentException("PrewarmCount는 MaxActive를 초과할 수 없습니다.", nameof(prewarmCount));
            if (prewarmCount > maxRetained)
                throw new ArgumentException("PrewarmCount는 MaxRetained를 초과할 수 없습니다.", nameof(prewarmCount));
            Kind = kind;
            PrewarmCount = prewarmCount;
            MaxActive = maxActive;
            MaxRetained = maxRetained;
            Overflow = overflow;
        }
    }

    // 풀의 설정을 나타내는 클래스다.
    public sealed class PoolSpec
    {
        public int PoolId;
        public AssetKind Kind;
        public string Address;
        public int PrewarmCount;
        public int MaxActive;
        public int MaxRetained;
        public PoolOverflowPolicy Overflow;
    }

    // 풀에서 빌린 인스턴스와 해당 대여 시점을 함께 나타내는 대여권이다.
    // 값이 생성된 뒤 바뀌면 반환 검증을 신뢰할 수 없으므로 readonly struct로 정의한다.
    public readonly struct PoolLease
    {
        // 이 인스턴스를 빌려준 풀의 ID다.
        // 다른 풀에 인스턴스를 잘못 반환하는 상황을 검사할 때 사용한다.
        public int PoolId { get; }

        // 풀에서 실제로 빌린 GameObject다.
        public GameObject Instance { get; }

        // 같은 인스턴스가 몇 번째로 대여되었는지를 나타내는 세대 번호다.
        // 이전 대여에서 늦게 실행된 콜백이 현재 사용 중인 인스턴스를 반환하지 못하게 한다.
        public uint Generation { get; }

        // 유효한 대여권은 풀 구현만 만들 수 있도록 생성자의 접근 범위를 internal로 제한한다.
        internal PoolLease(int poolId, GameObject instance, uint generation)
        {
            // 0은 유효한 풀 ID로 사용하지 않는다.
            if (poolId <= 0)
                throw new ArgumentOutOfRangeException(nameof(poolId));

            // 실제로 대여할 인스턴스가 없는 대여권은 생성할 수 없다.
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            // 0은 아직 대여된 적이 없는 상태를 나타내므로 유효한 세대 번호로 사용하지 않는다.
            if (generation == 0)
                throw new ArgumentOutOfRangeException(nameof(generation));

            // 검증이 끝난 값만 불변 프로퍼티에 저장한다.
            PoolId = poolId;
            Instance = instance;
            Generation = generation;
        }
    }

    // 생명주기 컴포넌트가 구체적으로 PrefabPool을 몰라도 소유자에게 반환을 요청할 수 있도록 하는 인터페이스다.
    public interface IPoolReturner
    {
        bool TryReturn(PoolLease lease);
    }

    // 풀에서 인스턴스를 빌리거나 반환할 때 호출되는 생명주기 콜백을 정의하는 인터페이스다.
    public interface IPoolLifecycle
    {
        void OnRent(PoolLease lease, IPoolReturner owner); // 비활성 상태에서 호출
        void OnReturn(); // 타이머/이벤트/Trail/Particle/참조 정리
    }
}
