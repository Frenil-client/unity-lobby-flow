using LobbyFlow.Content;
using NUnit.Framework;

namespace LobbyFlow.Tests
{
    /// <summary>데이터 표 사이의 참조 검증(SPEC 5.6). 잘못된 데이터는 화면에 닿기 전에 파일 이름과 줄 번호로 거부한다.</summary>
    public sealed class ContentTablesTests
    {
        private const string Characters =
            "id,name,role,description,color,portrait,prefab\n" +
            "knight,나이트,탱커,설명,#5C8CD9,UI/portrait_knight.png,Characters/knight/Character_knight.prefab\n";

        private const string Chapters =
            "id,name,scene,icon,download_label\n" +
            "chapter01,챕터 1,Chapter01/Chapter01.unity,UI/chapter_chapter01.png,\n" +
            "chapter02,챕터 2,Chapter02/Chapter02.unity,UI/chapter_chapter02.png,chapter02\n";

        private const string Costumes =
            "id,character_id,name,seasonal,prefab\n" +
            "knight_winter,knight,겨울 축제,true,Costumes/Character_knight_winter.prefab\n";

        [Test]
        public void Parse_LinksRecordsAndNotice()
        {
            var tables = ContentTables.Parse(Characters, Chapters, Costumes, "제목\n본문 첫 줄\n본문 둘째 줄");

            Assert.That(tables.FindCharacter("knight").PrefabAddress, Is.EqualTo("Characters/knight/Character_knight.prefab"));
            Assert.That(tables.FindChapter("chapter01").RequiresDownload, Is.False);
            Assert.That(tables.FindChapter("chapter02").DownloadLabel, Is.EqualTo("chapter02"));
            Assert.That(tables.CostumesOf("knight"), Has.Count.EqualTo(1));
            Assert.That(tables.Notice.Title, Is.EqualTo("제목"));
            Assert.That(tables.Notice.Body, Is.EqualTo("본문 첫 줄\n본문 둘째 줄"));
        }

        [Test]
        public void Parse_RejectsCostumeOfUnknownCharacter()
        {
            const string costumes = "id,character_id,name,seasonal,prefab\nghost_winter,ghost,겨울,true,Costumes/x.prefab\n";

            var error = Assert.Throws<ContentDataException>(() => ContentTables.Parse(Characters, Chapters, costumes, string.Empty));

            Assert.That(error.FileName, Is.EqualTo(ContentTables.CostumesFile));
            Assert.That(error.Line, Is.EqualTo(2));
        }

        [Test]
        public void Parse_RejectsDuplicateId()
        {
            var characters = Characters + "knight,또 나이트,탱커,설명,#5C8CD9,UI/a.png,Characters/a.prefab\n";

            var error = Assert.Throws<ContentDataException>(() => ContentTables.Parse(characters, Chapters, Costumes, string.Empty));

            Assert.That(error.FileName, Is.EqualTo(ContentTables.CharactersFile));
            Assert.That(error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Parse_RejectsMissingColumn()
        {
            const string chapters = "id,name,scene\nchapter01,챕터 1,Chapter01/Chapter01.unity\n";

            Assert.Throws<ContentDataException>(() => ContentTables.Parse(Characters, chapters, Costumes, string.Empty));
        }

        [Test]
        public void Parse_RejectsEmptyCharacterTable()
        {
            const string characters = "id,name,role,description,color,portrait,prefab\n";

            Assert.Throws<ContentDataException>(() => ContentTables.Parse(characters, Chapters, Costumes, string.Empty));
        }

        [Test]
        public void EventNotice_EmptyTextIsEmpty()
        {
            Assert.That(EventNotice.Parse("  \n ").IsEmpty, Is.True);
        }
    }
}
