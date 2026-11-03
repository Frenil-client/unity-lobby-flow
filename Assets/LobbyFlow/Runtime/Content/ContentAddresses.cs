namespace LobbyFlow.Content
{
    /// <summary>
    /// 코드가 직접 아는 Addressables 주소와 라벨. 나머지 리소스 주소는 데이터 파일이 갖는다(SPEC 5.6).
    /// 폴더 항목의 주소 아래 상대 경로가 파일 주소다(SPEC 5.2).
    /// </summary>
    public static class ContentAddresses
    {
        public const string CharactersCsv = "LocalData/" + ContentTables.CharactersFile;
        public const string ChaptersCsv = "LocalData/" + ContentTables.ChaptersFile;
        public const string CostumesCsv = "RemoteData/" + ContentTables.CostumesFile;
        public const string EventNotice = "RemoteData/event_notice.txt";
        public const string EventBanner = "Event/event_banner.png";
        public const string LobbyBgm = "Audio/LobbyBgm.wav";

        /// <summary>시작 시 받아야 하는 원격 콘텐츠(SPEC 5.3).</summary>
        public const string PatchBootLabel = "patch_boot";
    }
}
