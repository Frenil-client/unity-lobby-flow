using System;
using System.Collections.Generic;
using System.Threading;
using LobbyFlow.Content;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LobbyFlow.Patching
{
    /// <summary>
    /// Addressables 카탈로그 확인과 라벨 다운로드(SPEC 6.2). 모든 핸들은 결과를 읽은 직후 이 클래스 안에서 반환한다.
    /// Addressables 작업은 중간에 멈출 수 없으므로 취소 요청을 받아도 완료를 기다린 뒤 반환하고 취소 예외를 던진다.
    /// </summary>
    public sealed class AddressablesPatchService : IPatchService
    {
        public async Awaitable<bool> HasCachedContentAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await GetDownloadSizeAsync(ContentAddresses.PatchBootLabel, cancellationToken) == 0;
            }
            catch (PatchNetworkException)
            {
                return false;
            }
        }

        public async Awaitable<PatchCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            var check = Addressables.CheckForCatalogUpdates(false);
            var catalogs = await CompleteAsync(check, "카탈로그 확인", cancellationToken);

            var updated = catalogs != null && catalogs.Count > 0;
            if (updated)
            {
                LobbyLog.Info($"카탈로그 변경 {catalogs.Count}개, 갱신한다");
                var update = Addressables.UpdateCatalogs(catalogs, false);
                await CompleteAsync(update, "카탈로그 갱신", cancellationToken);
            }

            var bytes = await GetDownloadSizeAsync(ContentAddresses.PatchBootLabel, cancellationToken);
            return new PatchCheckResult(bytes, updated);
        }

        public async Awaitable<long> GetDownloadSizeAsync(string label, CancellationToken cancellationToken)
        {
            var size = Addressables.GetDownloadSizeAsync(label);
            return await CompleteAsync(size, $"'{label}' 크기 계산", cancellationToken);
        }

        public async Awaitable DownloadAsync(string label, Action<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            var handle = Addressables.DownloadDependenciesAsync(label, false);
            try
            {
                while (!handle.IsDone)
                {
                    var status = handle.GetDownloadStatus();
                    progress?.Invoke(new DownloadProgress(status.DownloadedBytes, status.TotalBytes));
                    await Awaitable.NextFrameAsync();
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw new PatchNetworkException($"'{label}' 다운로드 실패: {handle.OperationException?.Message}");

                var final = handle.GetDownloadStatus();
                progress?.Invoke(new DownloadProgress(final.TotalBytes, final.TotalBytes));
            }
            finally
            {
                Addressables.Release(handle);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public void ClearCache(IEnumerable<string> labels)
        {
            // 지금 로드돼 있는 번들은 지워지지 않는다. 로비에서 팝업을 모두 닫고 실행한다.
            Addressables.ClearDependencyCacheAsync(new List<string>(labels), true);
        }

        /// <summary>완료를 기다려 결과를 꺼내고 핸들을 반환한다. 실패하면 <see cref="PatchNetworkException"/>.</summary>
        private static async Awaitable<T> CompleteAsync<T>(AsyncOperationHandle<T> handle, string step, CancellationToken cancellationToken)
        {
            try
            {
                while (!handle.IsDone)
                    await Awaitable.NextFrameAsync();

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw new PatchNetworkException($"{step} 실패: {handle.OperationException?.Message}");

                cancellationToken.ThrowIfCancellationRequested();
                return handle.Result;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }
    }
}
