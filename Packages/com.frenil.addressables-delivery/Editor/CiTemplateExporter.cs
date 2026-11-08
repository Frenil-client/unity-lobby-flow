using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AddressablesDelivery.Editor
{
    /// <summary>
    /// 패키지의 <c>Templates~</c> 에 있는 Jenkins 파이프라인과 CI 스크립트를 프로젝트 루트에 쓴다. Jenkins 는 저장소 루트의 파일만 읽으므로
    /// 패키지 안에 둘 수 없다. Unity 버전, 플랫폼, 업로드 인증 같은 값은 현재 프로젝트와 설정 에셋에서 채운다.
    /// 이미 있는 파일은 덮어쓰지 않고 옆에 <c>.new</c> 로 써서 비교하게 한다.
    /// </summary>
    public static class CiTemplateExporter
    {
        public const string MenuPath = "Tools/Addressables Delivery/CI 파일 내보내기";

        private const string LogPrefix = "[Delivery]";
        private const string AgentLabel = "unity-windows";

        private static readonly string[] Templates = { "Jenkinsfile", "ci/unity.ps1", "ci/integrity.ps1" };

        private const string CcdCredentials =
            "// CCD 서비스 계정 키. Jenkins Credentials 에 Secret text 두 개(ID 는 아래와 같게)로 등록한다.\n" +
            "def withUploadCredentials(Closure body) {\n" +
            "    withCredentials([\n" +
            "        string(credentialsId: 'ugs-service-key-id', variable: 'UGS_CLI_SERVICE_KEY_ID'),\n" +
            "        string(credentialsId: 'ugs-service-secret-key', variable: 'UGS_CLI_SERVICE_SECRET_KEY'),\n" +
            "    ]) {\n" +
            "        body()\n" +
            "    }\n" +
            "}";

        private const string CustomCredentials =
            "// 업로드 명령(설정 에셋의 Custom Upload Command)이 쓰는 키를 Jenkins Credentials 에서 환경 변수로 넣는다. 예(AWS CLI):\n" +
            "//     withCredentials([string(credentialsId: 'aws-access-key-id', variable: 'AWS_ACCESS_KEY_ID'),\n" +
            "//                      string(credentialsId: 'aws-secret-access-key', variable: 'AWS_SECRET_ACCESS_KEY')]) { body() }\n" +
            "def withUploadCredentials(Closure body) {\n" +
            "    body()\n" +
            "}";

        [MenuItem(MenuPath, priority = 20)]
        private static void ExportFromMenu() => Export(out _);

        /// <param name="written">쓴 파일 경로(프로젝트 루트 기준).</param>
        /// <returns>템플릿을 찾아 썼으면 true.</returns>
        public static bool Export(out List<string> written)
        {
            written = new List<string>();
            var templateRoot = TemplateRoot();
            if (templateRoot == null)
            {
                Debug.LogError($"{LogPrefix} 패키지의 Templates~ 폴더를 찾지 못했다.");
                return false;
            }

            var delivery = DeliverySettings.Current;
            if (delivery == null)
            {
                Debug.LogError($"{LogPrefix} 설정 에셋이 없다. {DeliveryProjectSetup.MenuPath} 을 먼저 실행한다.");
                return false;
            }

            var values = Values(delivery);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (var template in Templates)
            {
                var text = File.ReadAllText(Path.Combine(templateRoot, template)).Replace("\r\n", "\n");
                foreach (var pair in values)
                    text = text.Replace(pair.Key, pair.Value);

                var target = Path.Combine(projectRoot, template);
                if (File.Exists(target))
                    target += ".new";

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                // Windows PowerShell 5.1 은 BOM 없는 UTF-8 스크립트를 ANSI 로 읽어 한글이 깨지므로 ps1 에는 BOM 을 붙인다.
                File.WriteAllText(target, text, new UTF8Encoding(target.EndsWith(".ps1") || target.EndsWith(".ps1.new")));
                written.Add(Path.GetRelativePath(projectRoot, target).Replace('\\', '/'));
            }

            Debug.Log($"{LogPrefix} CI 파일을 썼다: {string.Join(", ", written)}" +
                      (written.Any(w => w.EndsWith(".new")) ? ". 이미 있던 파일은 .new 로 썼으니 비교해 합친다." : string.Empty));
            return true;
        }

        private static Dictionary<string, string> Values(DeliverySettings delivery)
        {
            // CCD 는 버킷을 채운 플랫폼만, Custom 은 Windows 와 Android 를 고를 수 있게 한다.
            var platforms = new List<string>();
            if (delivery.Storage == RemoteStorage.Custom || !string.IsNullOrEmpty(delivery.WindowsBucket.Id))
                platforms.Add("StandaloneWindows64");
            if (delivery.Storage == RemoteStorage.Custom || !string.IsNullOrEmpty(delivery.AndroidBucket.Id))
                platforms.Add("Android");
            if (platforms.Count == 0)
                platforms.Add("StandaloneWindows64");

            // 기준 파일은 이미 전체 빌드한 플랫폼만 검사한다. 새 플랫폼은 첫 전체 빌드 뒤 integrity.ps1 에 더한다.
            var baselines = new[] { "Windows", "Android", "iOS" }
                .Where(f => File.Exists($"Assets/AddressableAssetsData/{f}/addressables_content_state.bin"));

            return new Dictionary<string, string>
            {
                ["{{UNITY_VERSION}}"] = Application.unityVersion,
                ["{{AGENT_LABEL}}"] = AgentLabel,
                ["{{PLATFORM_CHOICES}}"] = Quote(platforms),
                ["{{BASELINE_FOLDERS}}"] = Quote(baselines),
                ["{{FONT_FOLDERS}}"] = Quote(AddressablesBuild.DynamicFontFolders()),
                ["{{UPLOAD_CREDENTIALS}}"] = delivery.Storage == RemoteStorage.Ccd ? CcdCredentials : CustomCredentials,
            };
        }

        private static string Quote(IEnumerable<string> items) => string.Join(", ", items.Select(i => $"'{i}'"));

        private static string TemplateRoot()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CiTemplateExporter).Assembly);
            var root = package != null ? Path.Combine(package.resolvedPath, "Templates~") : null;
            return root != null && Directory.Exists(root) ? root : null;
        }
    }
}
