using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using LobbyFlow.Patching;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// 프로필 전환, Addressables 빌드, 플레이어 빌드, 콘텐츠 서버, CCD 업로드를 한 곳에서 하는 창(SPEC 5.7).
    /// 기능은 각 정적 클래스에 있고, 이 창은 상태를 보여 주고 순서를 어기는 버튼을 막는다. Jenkins 는 같은 메서드를 직접 부른다.
    /// </summary>
    public sealed class BuildWindow : EditorWindow
    {
        private static readonly string[] Profiles = { AddressablesBuild.LocalServerProfile, AddressablesBuild.CcdProfile };
        private static readonly string[] UploadBadges = { "없음 (latest 만)", "v1", "v2" };
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        private readonly List<(string Badge, string Result)> _connection = new();
        private bool _connectionChecking;
        private int _uploadBadge;
        private bool _buildCurrent;
        private string _buildState;
        private string _contentStatePath;
        private bool _hasContentState;
        private string _releaseNotes;
        private Vector2 _scroll;

        [MenuItem("Lobby Flow/빌드 창")]
        public static void Open() => GetWindow<BuildWindow>("Lobby Flow 빌드");

        private void OnEnable()
        {
            _releaseNotes = CcdUpload.DefaultReleaseNotes;
            Refresh();
        }

        private void OnFocus() => Refresh();

        private void Refresh()
        {
            _buildCurrent = AddressablesBuild.IsBuildCurrent(out _buildState);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            _contentStatePath = settings != null ? ContentUpdateScript.GetContentStateDataPath(false, settings) : null;
            _hasContentState = !string.IsNullOrEmpty(_contentStatePath) && File.Exists(_contentStatePath);
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawStatus();
            DrawAddressables();
            DrawPlayer();
            DrawContentServer();
            DrawCcd();
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatus()
        {
            Header("상태");
            EditorGUILayout.LabelField("플랫폼", EditorUserBuildSettings.activeBuildTarget.ToString());

            var current = Array.IndexOf(Profiles, AddressablesBuild.ActiveProfileName);
            var selected = EditorGUILayout.Popup("프로필", current, Profiles);
            if (selected != current && selected >= 0)
                Run(() => AddressablesBuild.UseProfile(Profiles[selected]));

            EditorGUILayout.HelpBox($"Addressables 빌드: {_buildState}", _buildCurrent ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.LabelField("업데이트 기준", _hasContentState ? _contentStatePath : "없음(전체 빌드 전)");
        }

        private void DrawAddressables()
        {
            Header("Addressables");
            if (GUILayout.Button("전체 빌드 (기준 갱신)") &&
                EditorUtility.DisplayDialog("Addressables 전체 빌드",
                    "콘텐츠 업데이트 기준(addressables_content_state.bin)이 새로 만들어지고, 원격 빌드 폴더의 이전 번들은 지워집니다. 이미 배포한 플레이어가 있다면 그 기준을 덮어씁니다.",
                    "빌드", "취소"))
                Run(() => AddressablesBuild.BuildFull());

            using (new EditorGUI.DisabledScope(!_hasContentState))
            {
                if (GUILayout.Button("콘텐츠 업데이트 빌드"))
                    Run(() => AddressablesBuild.BuildContentUpdate());
            }

            if (GUILayout.Button("폰트 동적 데이터 비우기"))
                Run(AddressablesBuild.ClearDynamicFontData);
        }

        private void DrawPlayer()
        {
            Header("플레이어 (개발 빌드)");
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (!_buildCurrent)
                EditorGUILayout.HelpBox("이 플랫폼의 Addressables 빌드가 현재 프로필과 맞지 않아 플레이어를 빌드할 수 없다.", MessageType.None);

            using (new EditorGUI.DisabledScope(!_buildCurrent || target != BuildTarget.StandaloneWindows64))
            {
                if (GUILayout.Button("Windows 개발 빌드"))
                    Run(() => RevealIfBuilt(PlayerBuild.BuildWindowsDevelopment(out var path), path));
            }

            using (new EditorGUI.DisabledScope(!_buildCurrent || target != BuildTarget.Android))
            {
                if (GUILayout.Button("Android 개발 빌드 (APK)"))
                    Run(() => RevealIfBuilt(PlayerBuild.BuildAndroidDevelopment(false, out var path), path));

                if (GUILayout.Button("Android 개발 빌드 후 기기 실행"))
                    Run(() => PlayerBuild.BuildAndroidDevelopment(true, out _));
            }

            EditorGUILayout.LabelField("플랫폼 전환은 File ▸ Build Profiles 에서 한다.", EditorStyles.miniLabel);
        }

        private void DrawContentServer()
        {
            Header($"콘텐츠 서버 (LocalServer, 포트 {ContentServer.Port})");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(ContentServer.IsRunning ? "실행 중" : "꺼짐", GUILayout.Width(60));
                if (GUILayout.Button(ContentServer.IsRunning ? "중지" : "시작"))
                {
                    if (ContentServer.IsRunning)
                        ContentServer.Stop();
                    else
                        ContentServer.Start();
                }
            }

            if (GUILayout.Button("Android 기기 연결 (adb reverse)"))
                Run(() => ContentServer.ReverseToAndroidDevice());
        }

        private void DrawCcd()
        {
            Header("CCD (다운로드는 누구나, 업로드는 작성자만)");
            var bucket = CcdConfig.Current;
            EditorGUILayout.LabelField("버킷", $"{bucket.Name} ({CcdConfig.Environment})");
            EditorGUILayout.LabelField("배지", $"출시 빌드 {CcdConfig.LatestBadge}, 개발 빌드는 테스트 메뉴에서 선택(처음 {CcdConfig.TestBadges[0]})");

            using (new EditorGUI.DisabledScope(_connectionChecking))
            {
                if (GUILayout.Button(_connectionChecking ? "확인 중..." : "CCD 연결 확인 (키 필요 없음)"))
                    CheckConnection(bucket);
            }

            foreach (var line in _connection)
                EditorGUILayout.LabelField(line.Badge, line.Result);

            EditorGUILayout.Space();
            if (!CcdUpload.HasServiceKey)
            {
                EditorGUILayout.HelpBox("서비스 계정 키(UGS_CLI_SERVICE_KEY_ID, UGS_CLI_SERVICE_SECRET_KEY)가 없어 업로드할 수 없다. 버전을 바꿔 보는 테스트는 LocalServer 프로필로 한다(docs/guides/local-patch-test.md).", MessageType.Info);
                return;
            }

            var isCcd = AddressablesBuild.ActiveProfileName == AddressablesBuild.CcdProfile;
            if (!isCcd)
                EditorGUILayout.HelpBox("CCD 프로필로 바꾸고 전체 빌드한 뒤 올린다.", MessageType.None);

            _releaseNotes = EditorGUILayout.TextField("릴리스 메모", _releaseNotes);
            _uploadBadge = EditorGUILayout.Popup("콘텐츠 버전 배지", _uploadBadge, UploadBadges);

            using (new EditorGUI.DisabledScope(!isCcd || !_buildCurrent))
            {
                var badge = _uploadBadge > 0 ? UploadBadges[_uploadBadge] : null;
                if (GUILayout.Button("현재 플랫폼 업로드와 릴리스") &&
                    EditorUtility.DisplayDialog("CCD 업로드",
                        $"ServerData 를 CCD '{bucket.Name}' 버킷({CcdConfig.Environment})에 올리고 새 릴리스를 만듭니다." +
                        $" {CcdConfig.LatestBadge}{(badge != null ? $", {badge}" : "")} 배지가 새 릴리스로 옮겨집니다.",
                        "업로드", "취소"))
                {
                    var notes = _releaseNotes;
                    Run(() =>
                    {
                        if (CcdUpload.UploadAndRelease(notes, badge))
                            _releaseNotes = CcdUpload.DefaultReleaseNotes;
                    });
                }
            }
        }

        /// <summary>현재 플랫폼 버킷의 배지마다 카탈로그 해시를 받아 본다. 공개 주소라 키 없이 된다.</summary>
        private async void CheckConnection(CcdBucket bucket)
        {
            _connectionChecking = true;
            _connection.Clear();
            var catalogHash = $"catalog_{AddressableAssetSettingsDefaultObject.Settings?.PlayerBuildVersion}.hash";
            foreach (var badge in CcdConfig.TestBadges)
            {
                string result;
                try
                {
                    using var response = await Http.GetAsync(CcdConfig.EntryUrl(bucket, badge, catalogHash));
                    result = response.IsSuccessStatusCode
                        ? $"연결됨, 카탈로그 해시 {(await response.Content.ReadAsStringAsync()).Trim()}"
                        : $"{(int)response.StatusCode} {(response.StatusCode == System.Net.HttpStatusCode.NotFound ? "(이 배지의 릴리스 없음)" : response.ReasonPhrase)}";
                }
                catch (Exception e)
                {
                    result = $"실패: {e.GetBaseException().Message}";
                }

                _connection.Add((badge, result));
            }

            _connectionChecking = false;
            if (this != null)
                Repaint();
        }

        private static void Header(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void RevealIfBuilt(bool built, string path)
        {
            if (built)
                EditorUtility.RevealInFinder(path);
        }

        /// <summary>빌드는 GUI 그리기 밖에서 돌린다. 그리기 중에 빌드하면 레이아웃 그룹이 깨진다.</summary>
        private void Run(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                action();
                if (this != null)
                    Refresh();
            };
        }
    }
}
