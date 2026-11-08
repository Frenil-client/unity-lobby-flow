using AddressablesDelivery.Loading;
using UnityEngine;

namespace LobbyFlow.Content
{
    /// <summary>데이터 파일 네 개를 Addressables 로 읽어 <see cref="ContentTables"/> 를 만든다. 파싱이 끝나면 텍스트 핸들은 바로 반환한다.</summary>
    public static class ContentLoader
    {
        public static async Awaitable<ContentTables> LoadAsync()
        {
            using var assets = new AssetHandleSet("Content");

            var characters = await LoadTextAsync(assets, ContentAddresses.CharactersCsv);
            var chapters = await LoadTextAsync(assets, ContentAddresses.ChaptersCsv);
            var costumes = await LoadTextAsync(assets, ContentAddresses.CostumesCsv);
            var notice = await LoadTextAsync(assets, ContentAddresses.EventNotice);

            var tables = ContentTables.Parse(characters, chapters, costumes, notice);
            LobbyLog.Info($"데이터 로드 완료: 캐릭터 {tables.Characters.Count}, 챕터 {tables.Chapters.Count}, 코스튬 {tables.Costumes.Count}");
            return tables;
        }

        private static async Awaitable<string> LoadTextAsync(AssetHandleSet assets, string address)
        {
            var asset = await assets.LoadAsync<TextAsset>(address);
            if (asset == null)
                throw new ContentDataException(address, 0, "파일을 불러오지 못했다");

            return asset.text;
        }
    }
}
