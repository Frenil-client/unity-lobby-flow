using System;
using UnityEngine;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// 로더가 넘겨주는 미리보기 프리팹과 그 반환 책임.
    /// 받은 쪽이 정확히 한 번 <see cref="Release"/> 한다. 두 번째 호출은 무시하고 경고로 남긴다.
    /// </summary>
    public sealed class PreviewAsset
    {
        private Action _release;

        public PreviewAsset(string key, GameObject prefab, Action release)
        {
            Key = key;
            Prefab = prefab;
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public string Key { get; }
        public GameObject Prefab { get; }
        public bool IsReleased => _release == null;

        public void Release()
        {
            if (_release == null)
            {
                LobbyLog.Warning($"이중 해제 시도: {Key}");
                return;
            }

            var release = _release;
            _release = null;
            release();
        }
    }
}
