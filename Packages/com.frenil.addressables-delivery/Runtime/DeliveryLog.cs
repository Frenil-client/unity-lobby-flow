using System;
using UnityEngine;

namespace AddressablesDelivery
{
    /// <summary>
    /// 패키지 런타임 로그. 기본은 <c>[Delivery]</c> 접두어로 콘솔에 남기고, 프로젝트 로그 형식에 맞추려면 <see cref="Handler"/> 를 바꾼다.
    /// </summary>
    public static class DeliveryLog
    {
        private const string Prefix = "[Delivery]";

        /// <summary>null 이면 기본 출력. 받은 문자열에는 접두어가 없다.</summary>
        public static Action<LogType, string> Handler { get; set; }

        public static void Info(string message) => Write(LogType.Log, message);

        public static void Warning(string message) => Write(LogType.Warning, message);

        public static void Error(string message) => Write(LogType.Error, message);

        private static void Write(LogType type, string message)
        {
            if (Handler != null)
            {
                Handler(type, message);
                return;
            }

            switch (type)
            {
                case LogType.Warning:
                    Debug.LogWarning($"{Prefix} {message}");
                    break;
                case LogType.Error:
                    Debug.LogError($"{Prefix} {message}");
                    break;
                default:
                    Debug.Log($"{Prefix} {message}");
                    break;
            }
        }
    }
}
