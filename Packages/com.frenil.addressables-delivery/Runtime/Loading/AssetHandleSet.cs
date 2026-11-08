using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace AddressablesDelivery.Loading
{
    /// <summary>
    /// 화면 하나가 쓰는 Addressables 리소스 묶음(초상, 아이콘, BGM 등). 화면이 소유하고 화면이 사라질 때 Dispose 한다.
    /// 완료된 핸들은 Dispose 때 반환하고, 진행 중인 핸들은 완료되는 즉시 반환한다. 반환된 뒤 도착한 결과는 null 로 돌려준다.
    /// </summary>
    public sealed class AssetHandleSet : IDisposable
    {
        // 핸들은 구조체라 목록에서 찾을 때 값 비교에 기대지 않도록 참조로 감싼다.
        private sealed class Entry
        {
            public AsyncOperationHandle Handle;
        }

        private readonly List<Entry> _entries = new();
        private readonly string _owner;
        private bool _disposed;

        public AssetHandleSet(string owner) => _owner = owner;

        /// <summary>로드에 실패했거나 기다리는 동안 Dispose 됐으면 null.</summary>
        public async Awaitable<T> LoadAsync<T>(string address) where T : Object
        {
            if (_disposed || string.IsNullOrEmpty(address))
                return null;

            var handle = Addressables.LoadAssetAsync<T>(address);
            ResourceCounters.AssetAcquired();
            var entry = new Entry { Handle = handle };
            _entries.Add(entry);

            while (!handle.IsDone)
                await Awaitable.NextFrameAsync();

            if (_disposed)
            {
                ReleaseHandle(handle);
                ResourceCounters.StaleResultReleased();
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                DeliveryLog.Warning($"{_owner} 리소스 로드 실패 '{address}': {handle.OperationException?.Message}");
                _entries.Remove(entry);
                ReleaseHandle(handle);
                return null;
            }

            return handle.Result;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var entry in _entries)
            {
                // 진행 중인 핸들은 LoadAsync 가 완료를 확인한 뒤 반환한다.
                if (entry.Handle.IsDone)
                    ReleaseHandle(entry.Handle);
            }

            _entries.Clear();
        }

        private static void ReleaseHandle(AsyncOperationHandle handle)
        {
            Addressables.Release(handle);
            ResourceCounters.AssetReleased();
        }
    }
}
