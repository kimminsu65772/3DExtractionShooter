using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionShooter.DataPipeline
{
    public sealed class RuntimeAssetRegistry
    {
        private readonly Dictionary<(string Address, AssetKind Kind), int> IdByAsset = new();
        private readonly Dictionary<int, (string Address, AssetKind Kind)> assetById = new();

        private int nextId = 1; // 0은 선택 리소스 없음.
        private bool isFrozen;

        public int Register(string address, AssetKind kind)
        {
            if (string.IsNullOrWhiteSpace(address))
                return 0;

            if (isFrozen)
            {
                throw new InvalidOperationException(
                    "고정된 RuntimeAssetRegistry에는 새로운 리소스를 등록할 수 없습니다.");
            }

            var key = (Address: address, Kind: kind);

            if (IdByAsset.TryGetValue(key, out var existingId))
            {
                return existingId;
            }

            int newId = nextId++;
            IdByAsset.Add(key, newId);
            assetById.Add(newId, key);

            return newId;
        }

        public bool TryResolve(int id, out string address, out AssetKind kind)
        {
            if (assetById.TryGetValue(id, out var asset))
            {
                address = asset.Address;
                kind = asset.Kind;
                return true;
            }
            address = string.Empty;
            kind = default;
            return false;
        }
        public void Freeze()
        {
            isFrozen = true;
        }
    }
}
