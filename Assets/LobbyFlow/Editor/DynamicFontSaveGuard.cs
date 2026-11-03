using TMPro;
using UnityEditor;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// 콘텐츠 폴더의 동적 TMP 폰트 에셋은 디스크에 항상 비운 상태로 저장한다(SPEC 5.2).
    /// 에디터가 TMP 글자를 그릴 때마다 쓴 글자가 아틀라스에 쌓이고, 다음 저장 때 그대로 파일에 써져
    /// 폰트 에셋이 git 변경으로 남고 로컬 폰트 번들 해시가 흔들린다. 저장 직전에 비우면 화면은 TMP 가 필요한 글자를 다시 채운다.
    /// </summary>
    public sealed class DynamicFontSaveGuard : AssetModificationProcessor
    {
        private const string ContentRoot = "Assets/LobbyFlowContent/";

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                if (!path.StartsWith(ContentRoot) || !path.EndsWith(".asset"))
                    continue;

                var fontAsset = AssetDatabase.LoadMainAssetAtPath(path) as TMP_FontAsset;
                if (fontAsset == null || fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                    continue;

                // 글자 표가 비어 있어도 거르지 않는다. TMP 자체 정리(플레이어 빌드 전)는 글자 표만 비우고 자간 표를 남겨,
                // 그대로 저장하면 이 프로젝트가 비운 상태와 다른 파일이 된다. 비운 에셋을 다시 비워도 결과는 같다.
                fontAsset.ClearFontAssetData(true);
            }

            return paths;
        }
    }
}
