using System;
using System.Diagnostics;
using System.Text;

namespace Dikozemie.Online
{
    // Frame-time accounting for the dedicated server. On 2026-11-04 the
    // arenas ran at 7-9 FPS whenever a player was waiting or watching, which
    // players feel as network lag. Every 30 seconds with players present the
    // server writes one line: frames per second, the slowest frame and how
    // many milliseconds per frame each part of this mod used, so a slow part
    // shows up in the log instead of being guessed.
    internal static class DikozemieServerPerf
    {
        internal enum Section
        {
            ArenaTick,
            SpawnTick,
            NavalTick,
            CryTick,
            RatingStore,
            Leaderboard,
            Count
        }

        private const double ReportSeconds = 30.0;
        private static readonly long[] Ticks = new long[(int)Section.Count];
        private static readonly int[] Calls = new int[(int)Section.Count];
        private static int _frames;
        private static double _frameSeconds;
        private static double _slowestFrame;
        private static double _sinceReport;

        internal static void Add(Section section, long elapsedTicks)
        {
            int index = (int)section;
            Ticks[index] += elapsedTicks;
            Calls[index]++;
        }

        internal static long Start() => Stopwatch.GetTimestamp();

        internal static void Stop(Section section, long started)
        {
            Add(section, Stopwatch.GetTimestamp() - started);
        }

        // ---- Main-thread watchdog (dedicated server only) ----------------
        // 2026-11-04 the naval arena stopped right after a two-player result:
        // no mod line, no engine line, and the process only exited 5 minutes
        // later, taking every arena down with it. The watchdog names the last
        // mod step that ran when frames stop, and ends a process that has
        // been frozen for two minutes so the supervisor restarts that arena
        // alone instead of players waiting on a dead one.
        private const double StallReportSeconds = 20.0;
        private const double StallKillSeconds = 120.0;
        private static long _lastFrameTimestamp;
        private static volatile string _breadcrumb = "start";
        private static System.Threading.Thread _watchdog;

        internal static void Crumb(string where)
        {
            _breadcrumb = where;
        }

        internal static void EnsureWatchdog(string logPath)
        {
            if (_watchdog != null)
            {
                return;
            }
            _watchdog = new System.Threading.Thread(() => WatchdogLoop(logPath))
            {
                IsBackground = true,
                Name = "DikozemieWatchdog"
            };
            _watchdog.Start();
        }

        private static void WatchdogLoop(string logPath)
        {
            bool reported = false;
            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                long last = System.Threading.Interlocked.Read(ref _lastFrameTimestamp);
                if (last == 0)
                {
                    continue;
                }
                double stalled = (Stopwatch.GetTimestamp() - last) /
                    (double)Stopwatch.Frequency;
                if (stalled < StallReportSeconds)
                {
                    reported = false;
                    continue;
                }
                if (!reported || stalled >= StallKillSeconds)
                {
                    reported = true;
                    WriteDirect(logPath,
                        "WATCHDOG main thread frozen for " + stalled.ToString("0") +
                        " s; last mod step: " + _breadcrumb +
                        (stalled >= StallKillSeconds
                            ? "; ending the process so this arena restarts"
                            : string.Empty));
                }
                if (stalled >= StallKillSeconds)
                {
                    try
                    {
                        Process.GetCurrentProcess().Kill();
                    }
                    catch
                    {
                        Environment.FailFast("Dikozemie arena main thread frozen");
                    }
                }
            }
        }

        // The normal trace takes a lock the frozen thread may hold.
        private static void WriteDirect(string path, string message)
        {
            string line = DateTimeOffset.UtcNow.ToString("O") + " " + message;
            Console.WriteLine("DikozemieOnline: " + message);
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // The console line above is the fallback.
            }
        }

        // Wall-clock frame gaps: the engine clamps a mission dt at 1/15 s, so
        // dt alone hides a frozen frame.
        private const double HitchSeconds = 0.1;
        private static int _hitches;
        private static double _slowestWallFrame;
        private static double _wallSeconds;
        private static readonly StringBuilder HitchSamples = new StringBuilder();

        // Called once per mission frame from the arena behaviour.
        internal static void Frame(float dt, int players)
        {
            long now = Stopwatch.GetTimestamp();
            long previous = System.Threading.Interlocked.Exchange(
                ref _lastFrameTimestamp, now);
            if (previous != 0)
            {
                double wall = (now - previous) / (double)Stopwatch.Frequency;
                _wallSeconds += wall;
                _slowestWallFrame = Math.Max(_slowestWallFrame, wall);
                if (wall >= HitchSeconds && players > 0)
                {
                    _hitches++;
                    if (_hitches <= 4)
                    {
                        HitchSamples.Append(' ')
                            .Append(DateTime.UtcNow.ToString("HH:mm:ss.fff"))
                            .Append('/')
                            .Append((wall * 1000.0).ToString("0"))
                            .Append("ms");
                    }
                }
            }
            _frames++;
            _frameSeconds += Math.Max(0f, dt);
            _slowestFrame = Math.Max(_slowestFrame, dt);
            _sinceReport += Math.Max(0f, dt);
            if (_sinceReport < ReportSeconds)
            {
                return;
            }

            if (players > 0 && _frames > 0)
            {
                var line = new StringBuilder();
                line.Append("perf ")
                    .Append(_sinceReport.ToString("0"))
                    .Append("s players=").Append(players)
                    .Append(" fps=").Append((_frames / Math.Max(0.001, _wallSeconds)).ToString("0.0"))
                    .Append(" slowestFrameMs=").Append((_slowestWallFrame * 1000.0).ToString("0"))
                    .Append(" hitches>100ms=").Append(_hitches)
                    .Append(HitchSamples.ToString());
                for (int index = 0; index < (int)Section.Count; index++)
                {
                    if (Calls[index] == 0)
                    {
                        continue;
                    }
                    double milliseconds = Ticks[index] * 1000.0 / Stopwatch.Frequency;
                    line.Append(' ')
                        .Append(((Section)index).ToString())
                        .Append('=')
                        .Append((milliseconds / _frames).ToString("0.00"))
                        .Append("ms/f");
                    if ((Section)index == Section.RatingStore)
                    {
                        line.Append(" (").Append(Calls[index]).Append(" locks)");
                    }
                }
                MissionMultiplayerDikozemieArena.Trace(line.ToString());
            }

            Array.Clear(Ticks, 0, Ticks.Length);
            Array.Clear(Calls, 0, Calls.Length);
            _frames = 0;
            _frameSeconds = 0;
            _slowestFrame = 0;
            _sinceReport = 0;
            _hitches = 0;
            _slowestWallFrame = 0;
            _wallSeconds = 0;
            HitchSamples.Clear();
        }
    }
}
