using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using LobbyFlow.Loading;
using UnityEngine;

namespace LobbyFlow.Tests
{
    /// <summary>Awaitable 을 쓰는 비동기 본문을 UnityTest 코루틴으로 돌린다. 본문의 예외는 테스트 실패로 다시 던진다.</summary>
    internal static class AsyncTest
    {
        public static IEnumerator Run(Func<Awaitable> body)
        {
            Exception error = null;
            var done = false;

            async void Start()
            {
                try
                {
                    await body();
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    done = true;
                }
            }

            Start();
            while (!done)
                yield return null;

            if (error != null)
                ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    internal sealed class TestSource : IPreviewSource
    {
        public TestSource(string key) => PreviewKey = key;

        public string PreviewKey { get; }
        public string PreviewAddress => $"Test/{PreviewKey}";
    }

    /// <summary>
    /// 테스트가 완료 시점과 결과를 정하는 미리보기 로더. 받은 리소스 수와 반환 수를 세어 소유권 규칙이 지켜졌는지 본다.
    /// <see cref="AutoComplete"/> 를 켜면 요청 즉시 성공한다.
    /// </summary>
    internal sealed class FakePreviewLoader : IPreviewAssetLoader
    {
        public sealed class Request
        {
            public IPreviewSource Source;
            public CancellationToken Token;
            public readonly AwaitableCompletionSource<PreviewAsset> Completion = new();
        }

        private readonly GameObject _prefab;

        public FakePreviewLoader(GameObject prefab) => _prefab = prefab;

        public readonly List<Request> Requests = new();
        public bool AutoComplete { get; set; }
        public int Acquired { get; private set; }
        public int Released { get; private set; }

        public Awaitable<PreviewAsset> LoadAsync(IPreviewSource source, CancellationToken cancellationToken)
        {
            var request = new Request { Source = source, Token = cancellationToken };
            Requests.Add(request);
            if (AutoComplete)
                Succeed(Requests.Count - 1);

            return request.Completion.Awaitable;
        }

        /// <summary>요청이 끝나 리소스를 넘긴다. 취소된 요청이어도 넘길 수 있다(로더 계약, IPreviewAssetLoader).</summary>
        public void Succeed(int index)
        {
            var request = Requests[index];
            Acquired++;
            request.Completion.SetResult(new PreviewAsset(request.Source.PreviewKey, _prefab, () => Released++));
        }

        public void Fail(int index, Exception error) => Requests[index].Completion.SetException(error);

        /// <summary>Addressables 로더처럼 작업은 끝났지만 취소를 확인해 받은 리소스를 스스로 반환하고 취소 예외를 던진다.</summary>
        public void CompleteCanceled(int index)
        {
            var request = Requests[index];
            Acquired++;
            Released++;
            request.Completion.SetException(new OperationCanceledException(request.Token));
        }
    }
}
