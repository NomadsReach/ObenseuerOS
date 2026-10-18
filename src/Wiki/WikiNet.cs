using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ObenseuerCore;
using UnityEngine.Networking;

namespace ObenseuerOS
{
    internal static class WikiNet
    {
        internal const string Busy = "busy";
        private const int MaxQueued = 8;
        private const int FloorIntervalMs = 500;
        private const int TimeoutSeconds = 20;
        private const int BodyBufferBytes = 64 * 1024;
        private const string UserAgent = "ObenseuerOS/0.1.0 (in-game wiki reader; +https://github.com/NomadsReach/Obenseuer-Modding-Framework)";
        private const string ConsentName = "consent";
        private const string BackoffName = "backoff";

        internal enum Lane
        {
            Front,
            Back
        }

        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly List<Request> Queued = new List<Request>();
        private static string root;
        private static bool enabled;
        private static bool consented;
        private static int intervalMs;
        private static int lastStart;
        private static long cooldownUntil;
        private static UnityWebRequest current;
        private static Body currentBody;
        private static Request currentRequest;

        internal static bool Online
        {
            get
            {
                return enabled && consented;
            }
        }

        internal static bool Consented
        {
            get
            {
                return consented;
            }
        }

        internal static long CooldownLeft
        {
            get
            {
                return Math.Max(0L, cooldownUntil - Now());
            }
        }

        internal static void Attach(string rootPath, bool isEnabled, int minIntervalMs)
        {
            root = rootPath;
            enabled = isEnabled;
            intervalMs = Math.Max(FloorIntervalMs, minIntervalMs);
            lastStart = Environment.TickCount - intervalMs;
            consented = File.Exists(Path.Combine(root, ConsentName));
            cooldownUntil = ReadCooldown();
        }

        internal static long Now()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalSeconds;
        }

        internal static void SetConsent(bool allow)
        {
            string file = Path.Combine(root, ConsentName);
            if (allow)
            {
                File.WriteAllText(file, "1", Encoding.ASCII);
                consented = true;
                return;
            }
            if (File.Exists(file)) File.Delete(file);
            consented = false;
            Drop();
        }

        internal static string Send(string url, int cap, bool json, Lane lane, Action<WikiReply> done)
        {
            if (!WikiTitle.Allowed(url) || WikiRobots.Refuses(url)) return WikiResponse.Blocked;
            if (CooldownLeft > 0) return WikiResponse.RateLimited;
            if (lane != Lane.Front && Queued.Count >= MaxQueued) return Busy;
            Request request = new Request { Url = url, Cap = cap, Json = json, Lane = lane, Done = done };
            if (lane == Lane.Front) Queued.Insert(0, request);
            else Queued.Add(request);
            return null;
        }

        internal static void Tick()
        {
            if (current != null)
            {
                if (current.isDone) Finish();
                return;
            }
            if (Queued.Count == 0 || !Online || Environment.TickCount - lastStart < Math.Max(intervalMs, WikiRobots.DelayMs) || Now() < cooldownUntil) return;
            Request next = Queued[0];
            Queued.RemoveAt(0);
            Start(next);
        }

        internal static void Drop()
        {
            List<Request> dropped = new List<Request>(Queued);
            Queued.Clear();
            if (current != null)
            {
                dropped.Insert(0, currentRequest);
                current.Abort();
                current.Dispose();
                current = null;
                currentBody = null;
                currentRequest = null;
            }
            foreach (Request request in dropped) request.Done(new WikiReply { Kind = WikiResponse.Dropped });
        }

        private static void Start(Request request)
        {
            if (!WikiTitle.Allowed(request.Url))
            {
                request.Done(new WikiReply { Kind = WikiResponse.Blocked });
                return;
            }
            Body body = new Body(request.Cap);
            UnityWebRequest web = new UnityWebRequest(request.Url, "GET", body, null);
            web.timeout = TimeoutSeconds;
            web.redirectLimit = 0;
            web.SetRequestHeader("User-Agent", UserAgent);
            lastStart = Environment.TickCount;
            try
            {
                web.SendWebRequest();
            }
            catch (Exception e)
            {
                Log.Warn("wiki request could not start: " + e.Message);
                web.Dispose();
                request.Done(new WikiReply { Kind = WikiResponse.Network });
                return;
            }
            current = web;
            currentBody = body;
            currentRequest = request;
        }

        private static void Finish()
        {
            WikiReply reply = new WikiReply
            {
                Status = current.responseCode,
                Error = current.error,
                TooLarge = currentBody.TooLarge,
                RetryAfter = current.GetResponseHeader("Retry-After"),
                ContentType = current.GetResponseHeader("Content-Type"),
                Mitigated = current.GetResponseHeader("cf-mitigated"),
                Body = currentBody.Bytes
            };
            Request request = currentRequest;
            current.Dispose();
            current = null;
            currentBody = null;
            currentRequest = null;
            WikiResponse.Classify(reply, request.Json);
            if (request.Json || reply.Kind != WikiResponse.HttpError) Hold(reply);
            Log.Debug("wiki.request", new Uri(request.Url).AbsolutePath + " " + reply.Status + " " + reply.Kind + " " + (Environment.TickCount - lastStart) + " ms");
            request.Done(reply);
        }

        private static void Hold(WikiReply reply)
        {
            int seconds = WikiResponse.BackoffSeconds(reply.Kind, reply.RetryAfter);
            if (seconds <= 0) return;
            cooldownUntil = Now() + seconds;
            List<Request> held = new List<Request>(Queued);
            Queued.Clear();
            foreach (Request request in held) request.Done(new WikiReply { Kind = WikiResponse.RateLimited });
            try
            {
                File.WriteAllText(Path.Combine(root, BackoffName), cooldownUntil.ToString(CultureInfo.InvariantCulture), Encoding.ASCII);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static long ReadCooldown()
        {
            string file = Path.Combine(root, BackoffName);
            long until = 0;
            try
            {
                if (File.Exists(file)) long.TryParse(File.ReadAllText(file), NumberStyles.None, CultureInfo.InvariantCulture, out until);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return until;
        }

        private sealed class Request
        {
            internal string Url;
            internal int Cap;
            internal bool Json;
            internal Lane Lane;
            internal Action<WikiReply> Done;
        }

        private sealed class Body : DownloadHandlerScript
        {
            private readonly MemoryStream received = new MemoryStream();
            private readonly int cap;

            internal Body(int cap) : base(new byte[BodyBufferBytes])
            {
                this.cap = cap;
            }

            internal bool TooLarge { get; private set; }

            internal byte[] Bytes
            {
                get
                {
                    return received.ToArray();
                }
            }

            protected override bool ReceiveData(byte[] chunk, int length)
            {
                if (received.Length + length > cap)
                {
                    TooLarge = true;
                    return false;
                }
                received.Write(chunk, 0, length);
                return true;
            }
        }
    }
}
