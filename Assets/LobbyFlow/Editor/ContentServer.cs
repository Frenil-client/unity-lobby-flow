using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// CCD 연결 전 패치 검증용 정적 파일 서버(SPEC 5.4 LocalServer 프로필). 프로젝트의 <c>ServerData/</c> 를 그대로 내보낸다.
    /// 요청마다 경로와 크기를 콘솔에 남겨, 패치 때 어떤 번들을 받았는지 확인하는 데 쓴다.
    /// 같은 네트워크의 기기에서 받으려면 Windows 방화벽에서 이 포트를 허용한다.
    /// </summary>
    public static class ContentServer
    {
        public const int Port = 8080;
        private const string StartMenu = "Lobby Flow/콘텐츠 서버/시작";
        private const string StopMenu = "Lobby Flow/콘텐츠 서버/중지";
        private const string LogPrefix = "[ContentServer]";
        private const string RunningKey = "LobbyFlow.ContentServer.Running";

        private static TcpListener _listener;
        private static Thread _thread;
        private static string _root;

        public static bool IsRunning => _listener != null;

        [InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            // 스크립트를 다시 읽으면(Play 진입 포함) 소켓을 닫고, 다시 읽은 뒤 켜져 있던 서버를 이어서 연다.
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            EditorApplication.quitting += Stop;
            if (SessionState.GetBool(RunningKey, false))
                Open("다시 시작");
        }

        [MenuItem(StartMenu)]
        public static void Start()
        {
            SessionState.SetBool(RunningKey, true);
            Open("시작");
        }

        [MenuItem(StopMenu)]
        public static void Stop()
        {
            SessionState.EraseBool(RunningKey);
            if (!IsRunning)
                return;

            Close();
            Debug.Log($"{LogPrefix} 중지");
        }

        private static void Open(string reason)
        {
            if (IsRunning)
                return;

            _root = Path.GetFullPath("ServerData");
            Directory.CreateDirectory(_root);

            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
            }
            catch (SocketException e)
            {
                _listener = null;
                SessionState.EraseBool(RunningKey);
                Debug.LogError($"{LogPrefix} 포트 {Port} 를 열 수 없다: {e.Message}");
                return;
            }

            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "ContentServer" };
            _thread.Start();
            Debug.Log($"{LogPrefix} {reason} http://localhost:{Port}/ → {_root}");
        }

        private static void Close()
        {
            if (!IsRunning)
                return;

            _listener.Stop();
            _listener = null;
            _thread = null;
        }

        [MenuItem(StartMenu, true)]
        private static bool CanStart() => !IsRunning;

        [MenuItem(StopMenu, true)]
        private static bool CanStop() => IsRunning;

        private static void AcceptLoop()
        {
            var listener = _listener;
            while (listener != null)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    return; // Stop 으로 닫혔다
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => Serve(client));
            }
        }

        private static void Serve(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    var requestLine = ReadRequestLine(stream);
                    var parts = requestLine?.Split(' ');
                    if (parts == null || parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD"))
                    {
                        Respond(stream, 400, "Bad Request", null, false);
                        return;
                    }

                    var relative = Uri.UnescapeDataString(parts[1].Split('?')[0]).TrimStart('/');
                    var path = Path.GetFullPath(Path.Combine(_root, relative));

                    // ServerData 밖으로 나가는 경로는 거절한다.
                    if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    {
                        Debug.LogWarning($"{LogPrefix} 404 {relative}");
                        Respond(stream, 404, "Not Found", null, false);
                        return;
                    }

                    var body = File.ReadAllBytes(path);
                    Respond(stream, 200, "OK", body, parts[0] == "HEAD");
                    Debug.Log($"{LogPrefix} 200 {relative} {body.Length:N0} bytes");
                }
                catch (IOException)
                {
                    // 받는 쪽이 끊었다.
                }
            }
        }

        private static string ReadRequestLine(NetworkStream stream)
        {
            // 머리글 끝(빈 줄)까지 읽고 첫 줄만 쓴다. 요청 본문은 받지 않는다.
            var buffer = new StringBuilder();
            var last = new char[4];
            while (true)
            {
                var b = stream.ReadByte();
                if (b < 0)
                    break;

                var c = (char)b;
                buffer.Append(c);
                last[0] = last[1];
                last[1] = last[2];
                last[2] = last[3];
                last[3] = c;
                if (last[0] == '\r' && last[1] == '\n' && last[2] == '\r' && last[3] == '\n')
                    break;
            }

            var text = buffer.ToString();
            var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
            return lineEnd > 0 ? text.Substring(0, lineEnd) : null;
        }

        private static void Respond(NetworkStream stream, int code, string reason, byte[] body, bool headOnly)
        {
            var length = body?.Length ?? 0;
            var header = $"HTTP/1.1 {code} {reason}\r\nContent-Type: application/octet-stream\r\nContent-Length: {length}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (body != null && !headOnly)
                stream.Write(body, 0, body.Length);
        }
    }
}
