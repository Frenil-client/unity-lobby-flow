#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#else
using UnityEngine;
#endif

namespace LobbyFlow.UI
{
    /// <summary>Android 뒤로가기와 PC Esc. Input System 은 Android 뒤로가기를 Esc 로 전달한다.</summary>
    public static class BackKey
    {
        public static bool WasPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
