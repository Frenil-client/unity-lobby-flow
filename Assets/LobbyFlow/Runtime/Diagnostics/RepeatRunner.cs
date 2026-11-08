using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AddressablesDelivery.Loading;
using LobbyFlow.Content;
using LobbyFlow.Preview;
using LobbyFlow.Scenes;
using LobbyFlow.UI;
using UISystem;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace LobbyFlow.Diagnostics
{
    /// <summary>
    /// 상세 팝업 열기·닫기와 챕터 1 왕복을 정해진 횟수만큼 자동으로 반복하고, 매 회 끝난 뒤의 리소스 집계와 메모리를 기록한다(SPEC 11).
    /// 결과는 로그와 <c>persistentDataPath/repeat-*.csv</c> 에 남긴다. 판정은 마지막 회의 보유 리소스·인스턴스·번들 수가 시작과 같은지다.
    /// 이 프로젝트가 소유한 객체 수(<see cref="ResourceCounters"/>)와 엔진 메모리를 함께 적고, Memory Profiler 스냅샷 비교는 사람이 한다.
    /// 개발 빌드의 테스트 메뉴에서만 부른다.
    /// </summary>
    public static class RepeatRunner
    {
        public const int DefaultCount = 30;

        private const float IntervalSeconds = 0.3f;
        private const float WaitTimeoutSeconds = 15f;
        private const int SettleFrames = 30;
        private const string RoundTripChapterId = "chapter01";

        public static bool IsRunning { get; private set; }

        /// <summary>테스트 메뉴에 보여 줄 진행 상태. 돌고 있지 않으면 마지막 결과.</summary>
        public static string Status { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsRunning = false;
            Status = string.Empty;
        }

        /// <summary>로비에서 캐릭터를 바꿔 가며 상세 팝업을 열고, 미리보기가 뜨면 닫는다.</summary>
        public static async void RunPopupCycles()
        {
            if (!TryBegin("popup", out var content))
                return;

            var recorder = new Recorder("popup");
            try
            {
                await SettleAsync();
                recorder.Sample(0, 0);

                for (var i = 1; i <= DefaultCount; i++)
                {
                    Status = $"팝업 반복 {i}/{DefaultCount}";
                    var watch = Stopwatch.StartNew();

                    var popup = await UIManager.Instance.OpenAsync<CharacterDetailPopup>();
                    if (popup == null)
                        throw new InvalidOperationException("상세 팝업을 열지 못했다");

                    popup.Bind(content, (i - 1) % content.Characters.Count, null);
                    if (!await WaitUntilAsync(() => popup == null || popup.PreviewState is PreviewState.Ready or PreviewState.Failed))
                        throw new TimeoutException($"{i}회 미리보기가 {WaitTimeoutSeconds}초 안에 끝나지 않았다");

                    var failed = popup != null && popup.PreviewState == PreviewState.Failed;
                    var loadMs = watch.Elapsed.TotalMilliseconds;
                    if (popup != null)
                        await UIManager.Instance.CloseAsync(popup);

                    await SettleAsync();
                    recorder.Sample(i, loadMs, failed ? "미리보기 실패" : null);
                    await Awaitable.WaitForSecondsAsync(IntervalSeconds);
                }
            }
            catch (Exception e)
            {
                recorder.Abort(e);
            }
            finally
            {
                Finish(recorder);
            }
        }

        /// <summary>로비 → 챕터 1(로컬, 다운로드 없음) → 로비를 반복한다. 로비 리소스가 다시 로드된 뒤 기록한다.</summary>
        public static async void RunSceneCycles()
        {
            if (!TryBegin("scene", out var content))
                return;

            var chapter = content.FindChapter(RoundTripChapterId) ?? content.Chapters[0];
            var chapterScene = Path.GetFileNameWithoutExtension(chapter.SceneAddress);
            var recorder = new Recorder("scene");
            try
            {
                await SettleAsync();
                recorder.Sample(0, 0);

                for (var i = 1; i <= DefaultCount; i++)
                {
                    Status = $"씬 왕복 {i}/{DefaultCount}";
                    var watch = Stopwatch.StartNew();

                    SceneFlow.EnterChapter(chapter);
                    if (!await WaitUntilAsync(() => !SceneFlow.IsLoading && SceneManager.GetActiveScene().name == chapterScene))
                        throw new TimeoutException($"{i}회 {chapterScene} 진입이 끝나지 않았다");

                    var enterMs = watch.Elapsed.TotalMilliseconds;
                    await SettleAsync();

                    watch.Restart();
                    SceneFlow.Load(SceneFlow.Lobby);
                    if (!await WaitUntilAsync(() => !SceneFlow.IsLoading && SceneManager.GetActiveScene().name == SceneFlow.Lobby))
                        throw new TimeoutException($"{i}회 로비 복귀가 끝나지 않았다");

                    var returnMs = watch.Elapsed.TotalMilliseconds;
                    await SettleAsync();
                    recorder.Sample(i, enterMs + returnMs, $"진입 {enterMs:0}ms, 복귀 {returnMs:0}ms");
                    await Awaitable.WaitForSecondsAsync(IntervalSeconds);
                }
            }
            catch (Exception e)
            {
                recorder.Abort(e);
            }
            finally
            {
                Finish(recorder);
            }
        }

        private static bool TryBegin(string kind, out ContentTables content)
        {
            content = LobbyServices.Content;
            string reason = null;
            if (IsRunning)
                reason = "이미 반복 중이다";
            else if (SceneManager.GetActiveScene().name != SceneFlow.Lobby || UIManager.Instance == null)
                reason = "로비에서 시작한다";
            else if (content == null || content.Characters.Count == 0 || content.Chapters.Count == 0)
                reason = "데이터를 아직 읽지 않았다";
            else if (SceneFlow.IsLoading || UIManager.Instance.IsOpen<CharacterDetailPopup>())
                reason = "씬 전환이나 팝업을 끝낸 뒤 시작한다";

            if (reason != null)
            {
                LobbyLog.Warning($"[반복] {kind} 시작 안 함: {reason}");
                MessageToast.Show($"반복 계측: {reason}");
                return false;
            }

            IsRunning = true;
            LobbyLog.Info($"[반복] {kind} {DefaultCount}회 시작");
            return true;
        }

        private static void Finish(Recorder recorder)
        {
            try
            {
                recorder.Close();
            }
            catch (Exception e)
            {
                LobbyLog.Error($"[반복] 기록 저장 실패: {e.Message}");
            }

            IsRunning = false;
            Status = recorder.Summary;
            LobbyLog.Info($"[반복] {recorder.Summary}");
            MessageToast.Show(recorder.Summary);
        }

        /// <summary>보유 리소스 수가 일정 프레임 동안 바뀌지 않을 때까지 기다린다. 비동기 로드가 끝난 뒤에 기록하려는 것이다.</summary>
        private static async Awaitable SettleAsync()
        {
            var stable = 0;
            var last = ResourceCounters.LiveAssets;
            var deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (stable < SettleFrames && Time.realtimeSinceStartup < deadline)
            {
                await Awaitable.NextFrameAsync();
                stable = ResourceCounters.LiveAssets == last && !SceneFlow.IsLoading ? stable + 1 : 0;
                last = ResourceCounters.LiveAssets;
            }
        }

        private static async Awaitable<bool> WaitUntilAsync(Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    return false;

                await Awaitable.NextFrameAsync();
            }

            return true;
        }

        private readonly struct Row
        {
            public Row(int iteration, double elapsedMs, string note)
            {
                Iteration = iteration;
                ElapsedMs = elapsedMs;
                Note = note;
                LiveAssets = ResourceCounters.LiveAssets;
                LiveInstances = ResourceCounters.LiveInstances;
                TotalAcquired = ResourceCounters.TotalAcquired;
                StaleReleased = ResourceCounters.StaleReleased;
                LoadedBundles = AssetBundle.GetAllLoadedAssetBundles().Count();
                MonoUsed = Profiler.GetMonoUsedSizeLong();
                Allocated = Profiler.GetTotalAllocatedMemoryLong();
                Reserved = Profiler.GetTotalReservedMemoryLong();
            }

            public int Iteration { get; }
            public double ElapsedMs { get; }
            public string Note { get; }
            public int LiveAssets { get; }
            public int LiveInstances { get; }
            public int TotalAcquired { get; }
            public int StaleReleased { get; }
            public int LoadedBundles { get; }
            public long MonoUsed { get; }
            public long Allocated { get; }
            public long Reserved { get; }

            public const string Header = "iteration,elapsed_ms,live_assets,live_instances,total_acquired,stale_released,loaded_bundles,mono_used,allocated,reserved,note";

            public string ToCsv() =>
                $"{Iteration},{ElapsedMs:0.0},{LiveAssets},{LiveInstances},{TotalAcquired},{StaleReleased},{LoadedBundles},{MonoUsed},{Allocated},{Reserved},{Note}";
        }

        /// <summary>한 번의 반복 실행 결과. 행마다 로그를 남기고 끝날 때 CSV 로 쓴다.</summary>
        private sealed class Recorder
        {
            private readonly string _kind;
            private readonly List<Row> _rows = new();
            private Exception _error;

            public Recorder(string kind) => _kind = kind;

            public string Summary { get; private set; }

            public void Sample(int iteration, double elapsedMs, string note = null)
            {
                var row = new Row(iteration, elapsedMs, note);
                _rows.Add(row);
                LobbyLog.Info($"[반복] {_kind} {row.ToCsv()}");
            }

            public void Abort(Exception e)
            {
                _error = e;
                LobbyLog.Error($"[반복] {_kind} 중단: {e.Message}");
            }

            public void Write()
            {
                var path = Path.Combine(Application.persistentDataPath, $"repeat-{_kind}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                var csv = new StringBuilder().AppendLine(Row.Header);
                foreach (var row in _rows)
                    csv.AppendLine(row.ToCsv());

                File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
                LobbyLog.Info($"[반복] {_kind} 기록 {path}");
            }

            public void Close()
            {
                Summary = $"{_kind} 반복 실패: {_error?.Message}";
                if (_rows.Count == 0)
                    return;

                Write();
                var first = _rows[0];
                var last = _rows[^1];
                var done = _rows.Count - 1;
                var balanced = first.LiveAssets == last.LiveAssets && first.LiveInstances == last.LiveInstances && first.LoadedBundles == last.LoadedBundles;
                var verdict = _error != null ? $"중단({done}회)" : balanced ? "정상" : "확인 필요";
                Summary = $"{_kind} {done}/{DefaultCount}회 {verdict}: 보유 리소스 {first.LiveAssets}→{last.LiveAssets}, " +
                          $"인스턴스 {first.LiveInstances}→{last.LiveInstances}, 번들 {first.LoadedBundles}→{last.LoadedBundles}, " +
                          $"할당 {first.Allocated / 1048576.0:0.0}→{last.Allocated / 1048576.0:0.0}MB";
            }
        }
    }
}
