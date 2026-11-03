using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LobbyFlow.Content
{
    /// <summary>
    /// 머리글이 있는 CSV(SPEC 5.6). 쉼표·줄바꿈·따옴표가 든 값은 큰따옴표로 감싸고 따옴표는 두 번 쓴다.
    /// 빈 줄과 <c>#</c> 으로 시작하는 줄은 건너뛴다.
    /// </summary>
    public sealed class CsvTable
    {
        private readonly Dictionary<string, int> _columns;

        private CsvTable(string source, string[] header, List<CsvRow> rows)
        {
            Source = source;
            Header = header;
            Rows = rows;
            _columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < header.Length; i++)
            {
                if (!_columns.TryAdd(header[i], i))
                    throw new ContentDataException(source, 1, $"머리글 '{header[i]}' 이 두 번 나온다");
            }
        }

        /// <summary>오류 메시지에 쓰는 원본 이름(파일 주소).</summary>
        public string Source { get; }
        public IReadOnlyList<string> Header { get; }
        public IReadOnlyList<CsvRow> Rows { get; }

        public static CsvTable Parse(string text, string source)
        {
            var records = ReadRecords(text ?? string.Empty, source);
            if (records.Count == 0)
                throw new ContentDataException(source, 1, "머리글이 없다");

            var header = records[0].Fields;
            for (var i = 0; i < header.Length; i++)
                header[i] = header[i].Trim();

            var table = new CsvTable(source, header, new List<CsvRow>(records.Count - 1));
            var rows = (List<CsvRow>)table.Rows;
            for (var i = 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Fields.Length != header.Length)
                    throw new ContentDataException(source, record.Line, $"열 수가 머리글({header.Length})과 다르다: {record.Fields.Length}");

                rows.Add(new CsvRow(table, record.Fields, record.Line));
            }

            return table;
        }

        /// <summary>필요한 열이 모두 있는지 확인한다. 없으면 <see cref="ContentDataException"/>.</summary>
        public void RequireColumns(params string[] columns)
        {
            foreach (var column in columns)
            {
                if (!_columns.ContainsKey(column))
                    throw new ContentDataException(Source, 1, $"'{column}' 열이 없다");
            }
        }

        internal int IndexOf(string column) => _columns.TryGetValue(column, out var index) ? index : -1;

        private readonly struct Record
        {
            public Record(string[] fields, int line)
            {
                Fields = fields;
                Line = line;
            }

            public string[] Fields { get; }
            public int Line { get; }
        }

        private static List<Record> ReadRecords(string text, string source)
        {
            var records = new List<Record>();
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var quotedRecordStart = false;
            var line = 1;
            var recordLine = 1;

            // BOM 이 붙은 파일도 같은 머리글로 읽히게 한다.
            var start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

            void EndRecord()
            {
                fields.Add(field.ToString());
                field.Clear();

                var empty = fields.Count == 1 && fields[0].Length == 0;
                var comment = !quotedRecordStart && fields[0].StartsWith("#", StringComparison.Ordinal);
                if (!empty && !comment)
                    records.Add(new Record(fields.ToArray(), recordLine));

                fields.Clear();
                quotedRecordStart = false;
            }

            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        if (c == '\n')
                            line++;
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        if (fields.Count == 0 && field.Length == 0)
                            quotedRecordStart = true;
                        inQuotes = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        EndRecord();
                        line++;
                        recordLine = line;
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (inQuotes)
                throw new ContentDataException(source, recordLine, "닫히지 않은 따옴표가 있다");

            EndRecord();
            return records;
        }
    }

    public readonly struct CsvRow
    {
        private readonly CsvTable _table;
        private readonly string[] _values;

        internal CsvRow(CsvTable table, string[] values, int line)
        {
            _table = table;
            _values = values;
            Line = line;
        }

        /// <summary>원본 파일의 줄 번호. 오류 메시지에 쓴다.</summary>
        public int Line { get; }

        /// <summary>앞뒤 공백을 뗀 값. 없는 열이면 <see cref="ContentDataException"/>.</summary>
        public string Get(string column)
        {
            var index = _table.IndexOf(column);
            if (index < 0)
                throw Error($"'{column}' 열이 없다");

            return _values[index].Trim();
        }

        /// <summary>비어 있으면 오류.</summary>
        public string GetRequired(string column)
        {
            var value = Get(column);
            if (value.Length == 0)
                throw Error($"'{column}' 값이 비어 있다");

            return value;
        }

        public bool GetBool(string column)
        {
            var value = Get(column);
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Length == 0)
                return false;

            throw Error($"'{column}' 값 '{value}' 은 true/false 가 아니다");
        }

        /// <summary><c>#RRGGBB</c> 형식.</summary>
        public Color GetColor(string column)
        {
            var value = GetRequired(column);
            if (!ColorUtility.TryParseHtmlString(value, out var color))
                throw Error($"'{column}' 값 '{value}' 은 색이 아니다");

            return color;
        }

        public ContentDataException Error(string message) => new(_table.Source, Line, message);
    }
}
