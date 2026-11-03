using LobbyFlow.Loading;
using UnityEngine;

namespace LobbyFlow.Content
{
    /// <summary><c>characters.csv</c> 한 줄. 로컬 데이터다.</summary>
    public sealed class CharacterRecord : IPreviewSource
    {
        internal CharacterRecord(CsvRow row)
        {
            Id = row.GetRequired("id");
            Name = row.GetRequired("name");
            Role = row.Get("role");
            Description = row.Get("description");
            ThemeColor = row.GetColor("color");
            PortraitAddress = row.GetRequired("portrait");
            PrefabAddress = row.GetRequired("prefab");
        }

        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public string Description { get; }
        public Color ThemeColor { get; }
        public string PortraitAddress { get; }
        public string PrefabAddress { get; }

        string IPreviewSource.PreviewKey => Id;
        string IPreviewSource.PreviewAddress => PrefabAddress;
    }

    /// <summary><c>costumes.csv</c> 한 줄. 원격 데이터라 패치로 줄이 늘거나 값이 바뀐다.</summary>
    public sealed class CostumeRecord : IPreviewSource
    {
        internal CostumeRecord(CsvRow row)
        {
            Id = row.GetRequired("id");
            CharacterId = row.GetRequired("character_id");
            Name = row.GetRequired("name");
            Seasonal = row.GetBool("seasonal");
            PrefabAddress = row.GetRequired("prefab");
        }

        public string Id { get; }
        public string CharacterId { get; }
        public string Name { get; }
        public bool Seasonal { get; }
        public string PrefabAddress { get; }

        string IPreviewSource.PreviewKey => Id;
        string IPreviewSource.PreviewAddress => PrefabAddress;
    }

    /// <summary><c>chapters.csv</c> 한 줄. 로컬 데이터지만 원격 챕터 씬 주소를 가리킬 수 있다.</summary>
    public sealed class ChapterRecord
    {
        internal ChapterRecord(CsvRow row)
        {
            Id = row.GetRequired("id");
            Name = row.GetRequired("name");
            SceneAddress = row.GetRequired("scene");
            IconAddress = row.GetRequired("icon");
            DownloadLabel = row.Get("download_label");
        }

        public string Id { get; }
        public string Name { get; }
        public string SceneAddress { get; }
        public string IconAddress { get; }

        /// <summary>진입 전에 받을 원격 라벨. 비어 있으면 로컬 챕터다.</summary>
        public string DownloadLabel { get; }

        public bool RequiresDownload => !string.IsNullOrEmpty(DownloadLabel);
    }

    /// <summary><c>event_notice.txt</c>. 첫 줄이 제목, 나머지가 본문이다.</summary>
    public sealed class EventNotice
    {
        public static readonly EventNotice Empty = new(string.Empty, string.Empty);

        private EventNotice(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public string Body { get; }
        public bool IsEmpty => Title.Length == 0 && Body.Length == 0;

        public static EventNotice Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Empty;

            var normalized = text.TrimStart('﻿').Replace("\r\n", "\n").Trim();
            var lineEnd = normalized.IndexOf('\n');
            return lineEnd < 0
                ? new EventNotice(normalized, string.Empty)
                : new EventNotice(normalized.Substring(0, lineEnd).Trim(), normalized.Substring(lineEnd + 1).Trim());
        }
    }
}
