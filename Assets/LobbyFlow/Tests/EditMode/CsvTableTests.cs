using LobbyFlow.Content;
using NUnit.Framework;
using UnityEngine;

namespace LobbyFlow.Tests
{
    /// <summary>CSV 파서(SPEC 5.6). 데이터 파일을 사람이 고치므로 잘못된 형식은 줄 번호와 함께 거부해야 한다.</summary>
    public sealed class CsvTableTests
    {
        [Test]
        public void Parse_ReadsQuotedFieldsWithCommasAndQuotes()
        {
            var table = CsvTable.Parse("id,text\na,\"쉼표, 그리고 \"\"따옴표\"\"\"\n", "test.csv");

            Assert.That(table.Rows.Count, Is.EqualTo(1));
            Assert.That(table.Rows[0].Get("text"), Is.EqualTo("쉼표, 그리고 \"따옴표\""));
        }

        [Test]
        public void Parse_IgnoresBomEmptyLinesAndComments()
        {
            var table = CsvTable.Parse("﻿id,name\r\n# 주석\r\n\r\na,에이\r\n", "test.csv");

            Assert.That(table.Header[0], Is.EqualTo("id"));
            Assert.That(table.Rows.Count, Is.EqualTo(1));
            Assert.That(table.Rows[0].Get("name"), Is.EqualTo("에이"));
        }

        [Test]
        public void Parse_RejectsRowWithWrongColumnCount()
        {
            var error = Assert.Throws<ContentDataException>(() => CsvTable.Parse("id,name\na,에이\nb\n", "test.csv"));

            Assert.That(error.FileName, Is.EqualTo("test.csv"));
            Assert.That(error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Parse_RejectsEmptyText()
        {
            Assert.Throws<ContentDataException>(() => CsvTable.Parse(string.Empty, "test.csv"));
        }

        [Test]
        public void RequireColumns_RejectsMissingColumn()
        {
            var table = CsvTable.Parse("id\na\n", "test.csv");

            Assert.Throws<ContentDataException>(() => table.RequireColumns("id", "name"));
        }

        [Test]
        public void GetRequired_RejectsEmptyValue()
        {
            var table = CsvTable.Parse("id,name\na,\n", "test.csv");

            Assert.Throws<ContentDataException>(() => table.Rows[0].GetRequired("name"));
        }

        [Test]
        public void GetColorAndBool_ParseValuesAndRejectBadOnes()
        {
            // 첫 칸이 # 이면 주석 줄이 되므로 색은 첫 열에 두지 않는다(데이터 파일의 color 열도 첫 열이 아니다).
            var table = CsvTable.Parse("flag,color,bad\ntrue,#FF8000,maybe\n", "test.csv");
            var row = table.Rows[0];

            Assert.That(row.GetColor("color"), Is.EqualTo(new Color(1f, 128f / 255f, 0f, 1f)));
            Assert.That(row.GetBool("flag"), Is.True);
            Assert.Throws<ContentDataException>(() => row.GetBool("bad"));
        }
    }
}
