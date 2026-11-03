using UnityEngine;

namespace LobbyFlow.Loading
{
    public enum LoadFailMode
    {
        None,
        Once,
        Always,
    }

    /// <summary>
    /// 테스트 메뉴가 바꾸는 지연·실패 주입 값. 요청이 시작될 때 한 번 읽어 그 요청에 고정한다.
    /// 모의 조건이므로 이 값으로 재현한 결과는 실제 네트워크 장애 검증과 구분해 기록한다.
    /// </summary>
    public static class LoadFaultSettings
    {
        public static readonly float[] DelayOptions = { 0f, 0.5f, 1f, 3f };

        private static int _delayIndex;

        public static float DelaySeconds => DelayOptions[_delayIndex];

        /// <summary>켜면 지연을 다음 요청 하나에만 적용하고 0 으로 돌린다. 늦게 끝나는 요청과 빨리 끝나는 요청을 섞는 용도.</summary>
        public static bool DelayOnce { get; set; }

        public static LoadFailMode FailMode { get; set; }

        /// <summary>켜면 취소 요청을 무시하고 지연을 끝까지 기다려 결과를 돌려준다. 취소 뒤 도착한 결과의 정리 경로를 재현한다.</summary>
        public static bool IgnoreCancellation { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _delayIndex = 0;
            DelayOnce = false;
            FailMode = LoadFailMode.None;
            IgnoreCancellation = false;
        }

        public static void CycleDelay() => _delayIndex = (_delayIndex + 1) % DelayOptions.Length;

        public static void CycleFailMode() => FailMode = (LoadFailMode)(((int)FailMode + 1) % 3);

        public static float ConsumeDelay()
        {
            var delay = DelaySeconds;
            if (DelayOnce)
                _delayIndex = 0;
            return delay;
        }

        public static bool ConsumeFailure()
        {
            switch (FailMode)
            {
                case LoadFailMode.Once:
                    FailMode = LoadFailMode.None;
                    return true;
                case LoadFailMode.Always:
                    return true;
                default:
                    return false;
            }
        }
    }
}
