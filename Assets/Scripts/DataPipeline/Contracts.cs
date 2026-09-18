using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    // 구현용 계약이다. 모든 TODO는 가이드의 단계 번호에 대응한다.
    public enum AssetKind { Projectile, MuzzleVfx, HitVfx, AudioClip, AudioEmitter }
    public enum PipelineState { Uninitialized, LoadingData, Validating, LoadingAssets, Prewarming, Ready, Failed, ShuttingDown, Disposed }
    public enum PoolOverflowPolicy { Reject, GrowWithinLimit }

    public sealed class DataIssue
    {
        public string Source; // 파일/Addressable 주소
        public int Record; // 논리 레코드 번호. 인용부호 안 개행과 물리 줄을 구분한다.
        public string Column;
        public string RawValue;
        public string Message;
    }

    public sealed class DataReport
    {
        public bool HasErrors => throw new NotImplementedException("TODO S01: 누적 오류 여부");
        public IReadOnlyList<DataIssue> Issues => throw new NotImplementedException("TODO S01: 읽기 전용 진단 목록");
        public void Add(DataIssue issue)
        {
            // TODO S01: 원본 위치와 오류를 보존. 보고 개수/문자 길이 상한을 정한다.
            throw new NotImplementedException();
        }
    }

    public sealed class CsvRecord
    {
        public int Number;
        public string[] Cells;
    }

    public sealed class CsvDocument
    {
        public string Source;
        public string[] Headers;
        public IReadOnlyList<CsvRecord> Records;
    }

    public interface ICsvRecordReader
    {
        CsvDocument Read(System.IO.TextReader reader, string source, DataReport report);
    }

    public interface ICsvBinding<T>
    {
        IReadOnlyList<string> ExpectedHeaders { get; }
        bool TryBind(CsvRecord row, IReadOnlyDictionary<string, int> header, DataReport report, out T result);
    }

    public interface IRowValidator<T>
    {
        void Validate(T row, int record, DataReport report);
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvColumnAttribute : Attribute
    {
        public string Name { get; set; } // null이면 필드 이름
        public bool Optional { get; set; } // 누락/빈 셀 허용 여부
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvIgnoreAttribute : Attribute { }

    public sealed class RuntimeWeapon
    {
        // TODO S04: 생성자로 검증된 데이터와 ID를 받고 읽기 전용 API로 전환한다.
        public WeaponDefinition Definition;
        public int ProjectileId;
        public int MuzzleVfxId;
        public int HitVfxId;
        public int FireSoundId;
    }

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

    public sealed class PoolLease
    {
        // TODO S07: 외부 생성/수정 불가로 전환. 매 대여마다 새 Generation을 부여한다.
        public int PoolId;
        public GameObject Instance;
        public ulong Generation;
    }

    public interface IPoolReturner
    {
        bool TryReturn(PoolLease lease);
    }

    public interface IPoolLifecycle
    {
        void OnRent(PoolLease lease, IPoolReturner owner); // 비활성 상태에서 호출
        void OnReturn(); // 타이머/이벤트/Trail/Particle/참조 정리
    }
}
