using System;
using System.Collections.Generic;

namespace LobbyFlow.Content
{
    /// <summary>
    /// 파싱과 검증을 마친 게임 데이터. Boot 패치가 끝난 뒤 한 번 만들어 앱이 켜져 있는 동안 유지한다(SPEC 5.6).
    /// 리소스는 들고 있지 않고 주소만 가진다. 리소스 로드·반환은 그것을 쓰는 화면이 한다.
    /// </summary>
    public sealed class ContentTables
    {
        public const string CharactersFile = "characters.csv";
        public const string ChaptersFile = "chapters.csv";
        public const string CostumesFile = "costumes.csv";

        private readonly List<CharacterRecord> _characters = new();
        private readonly List<ChapterRecord> _chapters = new();
        private readonly List<CostumeRecord> _costumes = new();
        private readonly Dictionary<string, CharacterRecord> _characterById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ChapterRecord> _chapterById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CostumeRecord> _costumeById = new(StringComparer.Ordinal);

        private ContentTables()
        {
        }

        public IReadOnlyList<CharacterRecord> Characters => _characters;
        public IReadOnlyList<ChapterRecord> Chapters => _chapters;
        public IReadOnlyList<CostumeRecord> Costumes => _costumes;
        public EventNotice Notice { get; private set; } = EventNotice.Empty;

        /// <summary>네 파일을 파싱하고 서로의 참조까지 검증한다. 문제가 있으면 <see cref="ContentDataException"/>.</summary>
        public static ContentTables Parse(string charactersCsv, string chaptersCsv, string costumesCsv, string noticeText)
        {
            var tables = new ContentTables();

            var characters = CsvTable.Parse(charactersCsv, CharactersFile);
            characters.RequireColumns("id", "name", "role", "description", "color", "portrait", "prefab");
            foreach (var row in characters.Rows)
                Add(tables._characters, tables._characterById, new CharacterRecord(row), row);

            var chapters = CsvTable.Parse(chaptersCsv, ChaptersFile);
            chapters.RequireColumns("id", "name", "scene", "icon", "download_label");
            foreach (var row in chapters.Rows)
                Add(tables._chapters, tables._chapterById, new ChapterRecord(row), row);

            var costumes = CsvTable.Parse(costumesCsv, CostumesFile);
            costumes.RequireColumns("id", "character_id", "name", "seasonal", "prefab");
            foreach (var row in costumes.Rows)
            {
                var costume = new CostumeRecord(row);
                if (!tables._characterById.ContainsKey(costume.CharacterId))
                    throw row.Error($"없는 캐릭터 '{costume.CharacterId}' 를 가리킨다");

                Add(tables._costumes, tables._costumeById, costume, row);
            }

            if (tables._characters.Count == 0)
                throw new ContentDataException(CharactersFile, 1, "캐릭터가 하나도 없다");
            if (tables._chapters.Count == 0)
                throw new ContentDataException(ChaptersFile, 1, "챕터가 하나도 없다");

            tables.Notice = EventNotice.Parse(noticeText);
            return tables;
        }

        public CharacterRecord FindCharacter(string id) => Find(_characterById, id);
        public ChapterRecord FindChapter(string id) => Find(_chapterById, id);
        public CostumeRecord FindCostume(string id) => Find(_costumeById, id);

        /// <summary>캐릭터의 코스튬을 파일 순서대로 돌려준다.</summary>
        public List<CostumeRecord> CostumesOf(string characterId)
        {
            var result = new List<CostumeRecord>();
            foreach (var costume in _costumes)
            {
                if (costume.CharacterId == characterId)
                    result.Add(costume);
            }

            return result;
        }

        private static void Add<T>(List<T> list, Dictionary<string, T> byId, T record, CsvRow row) where T : class
        {
            var id = record switch
            {
                CharacterRecord c => c.Id,
                ChapterRecord c => c.Id,
                CostumeRecord c => c.Id,
                _ => throw new ArgumentException(typeof(T).Name),
            };

            if (!byId.TryAdd(id, record))
                throw row.Error($"id '{id}' 가 두 번 나온다");

            list.Add(record);
        }

        private static T Find<T>(Dictionary<string, T> byId, string id) where T : class =>
            !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var record) ? record : null;
    }
}
