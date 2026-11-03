using UnityEngine;

namespace LobbyFlow.Audio
{
    /// <summary>
    /// BGM 한 줄과 효과음 한 줄. 재생기만 앱 전체에서 하나 유지하고, 클립은 재생을 요청한 화면이 소유한다.
    /// 화면은 파괴될 때 자기 BGM 을 <see cref="StopBgm"/> 으로 내려 클립 참조를 남기지 않는다.
    /// </summary>
    public static class AudioService
    {
        private static AudioSource _bgm;
        private static AudioSource _sfx;

        public static void PlayBgm(AudioClip clip, float volume = 0.5f)
        {
            if (clip == null)
                return;

            EnsureSources();
            if (_bgm.clip == clip && _bgm.isPlaying)
                return;

            _bgm.clip = clip;
            _bgm.volume = volume;
            _bgm.loop = true;
            _bgm.Play();
        }

        /// <summary>지정한 클립이 재생 중일 때만 멈춘다. 다른 화면이 이미 바꾼 BGM 은 건드리지 않는다.</summary>
        public static void StopBgm(AudioClip clip)
        {
            if (_bgm == null || _bgm.clip != clip)
                return;

            _bgm.Stop();
            _bgm.clip = null;
        }

        public static void PlaySfx(AudioClip clip, float volume = 0.8f)
        {
            if (clip == null)
                return;

            EnsureSources();
            _sfx.PlayOneShot(clip, volume);
        }

        /// <summary>처음 쓸 때 만든다. 플레이를 다시 시작하면 이전 객체가 파괴돼 있으므로 새로 만든다.</summary>
        private static void EnsureSources()
        {
            if (_bgm != null)
                return;

            var go = new GameObject("AudioService");
            Object.DontDestroyOnLoad(go);
            _bgm = go.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _sfx = go.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
        }
    }
}
