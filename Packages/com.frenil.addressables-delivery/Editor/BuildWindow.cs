using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace AddressablesDelivery.Editor
{
    /// <summary>
    /// 프로필 전환, Addressables 빌드, 플레이어 빌드, 콘텐츠 서버, 업로드를 한 곳에서 하는 창.
    /// 기능은 각 정적 클래스에 있고, 이 창은 상태를 보여 주고 순서를 어기는 버튼을 막는다. CI 는 같은 메서드를 <see cref="CiBuild"/> 로 부른다.
    /// </summary>
    public sealed class BuildWindow : EditorWindow
    {
        public const string MenuPath = "Tools/Addressables Delivery/빌드 창";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        private readonly List<(string Label, string Result)> _connection = new();
        private bool _connectionChecking;
        private string _uploadBadge = string.Empty;
        private bool _buildCurrent;
        private string _buildState;
        private string _contentStatePath;
        private bool _hasContentState;
        private string _releaseNotes;
        private Vector2 _scroll;

        [MenuItem(MenuPath, priority = 0)]
        public static void Open() => GetWindow<BuildWindow>("Addressables Delivery");

        private static string[] Profiles => new[] { AddressablesBuild.LocalServerProfile, AddressablesBuild.RemoteProfile };

        private void OnEnable()
        {
            _releaseNotes = ContentUpload.DefaultReleaseNotes;
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
            var delivery = DeliverySettings.Current;
            if (delivery == null || AddressableAssetSettingsDefaultObject.Settings == null)
            {
                EditorGUILayout.HelpBox($"설정이 없다. {DeliveryProjectSetup.MenuPath} 을 먼저 실행한다.", MessageType.Warning);
                if (GUILayout.Button("프로젝트에 적용"))
                    Run(() => DeliveryProjectSetup.Apply());

                EditorGUILayout.EndScrollView();
                return;
            }

            DrawStatus(delivery);
            DrawAddressables();
            DrawPlayer();
            DrawContentServer();
            DrawRemote(delivery);
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatus(DeliverySettings delivery)
        {
            Header("상태");
            EditorGUILayout.LabelField("플랫폼", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.ObjectField("설정 에셋", delivery, typeof(DeliverySettings), false);

            var profiles = Profiles;
            var current = Array.IndexOf(profiles, AddressablesBuild.ActiveProfileName);
            var selected = EditorGUILayout.Popup("프로필", current, profiles);
            if (selected != current && selected >= 0)
                Run(() => AddressablesBuild.UseProfile(profiles[selected]));

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

            using (new EditorGUI.DisabledScope(AddressablesBuild.DynamicFontFolders().Length == 0))
            {
                if (GUILayout.Button("폰트 동적 데이터 비우기"))
                    Run(AddressablesBuild.ClearDynamicFontData);
            }
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
            Header($"콘텐츠 서버 ({AddressablesBuild.LocalServerProfile}, 포트 {ContentServer.Port})");
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

        private void DrawRemote(DeliverySettings delivery)
        {
            var isCcd = delivery.Storage == RemoteStorage.Ccd;
            Header(isCcd ? "CCD (다운로드는 키 없이, 업로드는 서비스 계정 키)" : "원격 저장소 (Custom)");
            if (isCcd)
            {
                var bucket = CcdLocation.Current;
                EditorGUILayout.LabelField("버킷", $"{bucket.Name} ({CcdLocation.Environment})");
                EditorGUILayout.LabelField("배지", $"출시 빌드 {CcdLocation.ReleaseBadge}, 개발 빌드 기본 {CcdLocation.TestBadges[0]}");
            }
            else
            {
                EditorGUILayout.LabelField("받는 주소", delivery.CustomLoadUrl);
            }

            using (new EditorGUI.DisabledScope(_connectionChecking))
            {
                if (GUILayout.Button(_connectionChecking ? "확인 중..." : "연결 확인 (카탈로그 해시 받기)"))
                    CheckConnection(isCcd);
            }

            foreach (var line in _connection)
                EditorGUILayout.LabelField(line.Label, line.Result);

            EditorGUILayout.Space();
            if (!ContentUpload.CanUpload(out var reason))
            {
                EditorGUILayout.HelpBox($"업로드할 수 없다: {reason} 버전을 바꿔 보는 테스트는 {AddressablesBuild.LocalServerProfile} 프로필로 한다.", MessageType.Info);
                return;
            }

            var isRemote = AddressablesBuild.ActiveProfileName == AddressablesBuild.RemoteProfile;
            if (!isRemote)
                EditorGUILayout.HelpBox($"{AddressablesBuild.RemoteProfile} 프로필로 바꾸고 전체 빌드한 뒤 올린다.", MessageType.None);

            _releaseNotes = EditorGUILayout.TextField("릴리스 메모", _releaseNotes);
            _uploadBadge = EditorGUILayout.TextField(isCcd ? "추가 배지 (선택)" : "{badge} 값 (선택)", _uploadBadge);

            using (new EditorGUI.DisabledScope(!isRemote || !_buildCurrent))
            {
                var badge = string.IsNullOrWhiteSpace(_uploadBadge) ? null : _uploadBadge.Trim();
                var message = isCcd
                    ? $"ServerData 를 CCD '{CcdLocation.Current.Name}' 버킷({CcdLocation.Environment})에 올리고 새 릴리스를 만듭니다. " +
                      $"latest{(badge != null ? $", {badge}" : "")} 배지가 새 릴리스로 옮겨집니다."
                    : "설정 에셋의 업로드 명령으로 ServerData 를 올립니다.";
                if (GUILayout.Button("현재 플랫폼 업로드") && EditorUtility.DisplayDialog("업로드", message, "업로드", "취소"))
                {
                    var notes = _releaseNotes;
                    Run(() =>
                    {
                        if (ContentUpload.Upload(notes, badge))
                            _releaseNotes = ContentUpload.DefaultReleaseNotes;
                    });
                }
            }
        }

        /// <summary>공개 주소에서 카탈로그 해시를 받아 본다. CCD 는 고를 수 있는 배지마다 본다. 키가 필요 없다.</summary>
        private async void CheckConnection(bool isCcd)
        {
            _connectionChecking = true;
            _connection.Clear();
            var catalogHash = $"catalog_{AddressableAssetSettingsDefaultObject.Settings?.PlayerBuildVersion}.hash";
            var badges = isCcd ? CcdLocation.TestBadges : new string[] { null };
            foreach (var badge in badges)
            {
                string result;
                try
                {
                    using var response = await Http.GetAsync(ContentUpload.FileUrl(catalogHash, badge));
                    result = response.IsSuccessStatusCode
                        ? $"연결됨, 카탈로그 해시 {(await response.Content.ReadAsStringAsync()).Trim()}"
                        : $"{(int)response.StatusCode} {(response.StatusCode == System.Net.HttpStatusCode.NotFound ? "(파일 또는 릴리스 없음)" : response.ReasonPhrase)}";
                }
                catch (Exception e)
                {
                    result = $"실패: {e.GetBaseException().Message}";
                }

                _connection.Add((badge ?? "주소", result));
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
