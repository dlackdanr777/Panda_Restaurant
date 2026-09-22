#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in measurements around the real preview update and Camera.Render calls.
/// The host chooses the population, focuses its window and advances normally;
/// this recorder neither drives the simulation nor requests an extra repaint.
/// </summary>
public static class EnhancementFairyFrameProbe
{
    private const int Capacity = 50000;
    private static readonly double TickMilliseconds = 1000d / Stopwatch.Frequency;
    private static Frame[] _updates, _renders;
    private static int _updateCount, _renderCount;
    private static string _label, _startedUtc, _outputPath;
    private static int _requestedPopulation, _width, _height;
    private static long _startedTicks;
    private static double _startedEditorTime;
    private static int _lastPopulation;
    private static bool _allocationAvailable;
    public static bool IsRecording { get; private set; }

    public readonly struct Work
    {
        internal readonly long Ticks, Bytes;
        internal Work(long ticks, long bytes) { Ticks = ticks; Bytes = bytes; }
    }

    [Serializable]
    public struct Frame
    {
        public double elapsedMilliseconds;
        public double workMilliseconds;
        public long managedBytes;
        public int activeFairies;
        public bool foreground;
    }

    [Serializable]
    public sealed class Statistics
    {
        public int count;
        public double observedHertz;
        public double averageIntervalMilliseconds;
        public double p95IntervalMilliseconds;
        public double p99IntervalMilliseconds;
        public double longestIntervalMilliseconds;
        public int intervalsOver25ms, intervalsOver33ms, intervalsOver50ms, intervalsOver100ms;
        public double averageWorkMilliseconds, p95WorkMilliseconds, longestWorkMilliseconds;
        public long managedBytes;
        public double managedBytesPerCall;
        public int minimumActiveFairies, maximumActiveFairies, foregroundSamples;
    }

    [Serializable]
    public sealed class Report
    {
        public string label, startedUtc, finishedUtc, unityVersion;
        public int requestedPopulation, width, height;
        public bool playing, allocationCounterAvailable;
        public double observedSeconds;
        public string scope = "Real Editor preview update and Camera.Render entry intervals; foreground is recorded per sample, not assumed. Work is main-thread wall time and managed allocations inside the corresponding call. Recording adds no simulation/repaint. GPU completion, native allocations and presentation/display scanout are not measured. Raw interval timestamps use the host's monotonic Editor clock; work costs use Stopwatch.";
        public Statistics simulation, rendering;
        public Frame[] simulationFrames, renderFrames;
    }

    public static void Begin(string label, int activeFairies, int width = 1920, int height = 1080)
    {
        if (IsRecording) throw new InvalidOperationException("A fairy frame probe is already recording.");
        _updates = new Frame[Capacity]; _renders = new Frame[Capacity];
        _updateCount = _renderCount = 0;
        _label = label; _requestedPopulation = activeFairies; _width = width; _height = height;
        _startedUtc = DateTime.UtcNow.ToString("O");
        _allocationAvailable = ProbeAllocationCounter();
        _startedTicks = Stopwatch.GetTimestamp();
        _startedEditorTime = EditorApplication.timeSinceStartup;
        IsRecording = true;
    }

    public static void Begin(string label, string outputPath, int activeFairies = 0, int width = 1920, int height = 1080)
    { Begin(label, activeFairies, width, height); _outputPath = outputPath; }

    public static void RecordSimulation(double now, double elapsedMs, long allocated, int activeCount)
    {
        _lastPopulation = activeCount;
        RecordMeasured(_updates, ref _updateCount, now, elapsedMs, allocated, activeCount);
    }

    public static void RecordRender(double now, double elapsedMs, long allocated)
        => RecordMeasured(_renders, ref _renderCount, now, elapsedMs, allocated, _lastPopulation);

    private static void RecordMeasured(Frame[] frames, ref int count, double now, double elapsedMs, long allocated, int activeCount)
    {
        if (!IsRecording || frames == null || count >= frames.Length) return;
        frames[count++] = new Frame
        {
            elapsedMilliseconds = (now - _startedEditorTime) * 1000d, workMilliseconds = elapsedMs,
            managedBytes = _allocationAvailable ? allocated : -1, activeFairies = activeCount,
            foreground = EditorWindow.focusedWindow == GachaCollectionPreviewWindow.ActiveWindow &&
                UnityEditorInternal.InternalEditorUtility.isApplicationActive
        };
    }

    public static Report Finish() => Complete(_outputPath);

    public static Work BeginWork() => !IsRecording ? default : new Work(Stopwatch.GetTimestamp(),
        _allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0);

    public static void RecordSimulation(Work work, int activeFairies, bool foreground)
    {
        if (!IsRecording || work.Ticks == 0) return;
        Record(_updates, ref _updateCount, work, activeFairies, foreground);
    }

    public static void RecordRender(Work work, int activeFairies, bool foreground)
    {
        if (!IsRecording || work.Ticks == 0) return;
        Record(_renders, ref _renderCount, work, activeFairies, foreground);
    }

    private static void Record(Frame[] frames, ref int count, Work work, int activeFairies, bool foreground)
    {
        long finished = Stopwatch.GetTimestamp();
        long bytes = _allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - work.Bytes : -1;
        if (count >= frames.Length) return;
        frames[count++] = new Frame
        {
            elapsedMilliseconds = (work.Ticks - _startedTicks) * TickMilliseconds,
            workMilliseconds = (finished - work.Ticks) * TickMilliseconds,
            managedBytes = bytes, activeFairies = activeFairies, foreground = foreground
        };
    }

    public static Report Complete(string outputPath)
    {
        if (!IsRecording) throw new InvalidOperationException("No fairy frame probe is recording.");
        double seconds = (Stopwatch.GetTimestamp() - _startedTicks) / (double)Stopwatch.Frequency;
        IsRecording = false;
        var report = new Report
        {
            label = _label, requestedPopulation = _requestedPopulation, width = _width, height = _height,
            startedUtc = _startedUtc, finishedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            playing = Application.isPlaying, allocationCounterAvailable = _allocationAvailable, observedSeconds = seconds,
            simulation = Summarize(_updates, _updateCount), rendering = Summarize(_renders, _renderCount),
            simulationFrames = Copy(_updates, _updateCount), renderFrames = Copy(_renders, _renderCount)
        };
        if (!string.IsNullOrEmpty(outputPath))
        {
            string path = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            WriteCsv(Path.ChangeExtension(path, null) + "-simulation.csv", report.simulationFrames);
            WriteCsv(Path.ChangeExtension(path, null) + "-render.csv", report.renderFrames);
        }
        return report;
    }

    public static void Cancel() { IsRecording = false; _updates = _renders = null; _updateCount = _renderCount = 0; }

    private static void WriteCsv(string path, Frame[] frames)
    {
        var csv = new StringBuilder("frame,elapsed_ms,interval_ms,work_ms,managed_bytes,active_fairies,foreground\n");
        for (int i = 0; i < frames.Length; i++)
        {
            Frame value = frames[i];
            csv.Append(i).Append(',').Append(value.elapsedMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append((i == 0 ? 0 : value.elapsedMilliseconds - frames[i - 1].elapsedMilliseconds).ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.workMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',').Append(value.managedBytes).Append(',')
                .Append(value.activeFairies).Append(',').Append(value.foreground ? 1 : 0).Append('\n');
        }
        File.WriteAllText(path, csv.ToString());
    }

    private static Frame[] Copy(Frame[] source, int count)
    {
        var result = new Frame[count]; Array.Copy(source, result, count); return result;
    }

    private static Statistics Summarize(Frame[] frames, int count)
    {
        var result = new Statistics { count = count, managedBytes = _allocationAvailable ? 0 : -1 };
        if (count == 0) return result;
        var intervals = new double[Math.Max(0, count - 1)];
        var costs = new double[count];
        result.minimumActiveFairies = int.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Frame frame = frames[i]; costs[i] = frame.workMilliseconds;
            result.averageWorkMilliseconds += frame.workMilliseconds;
            result.longestWorkMilliseconds = Math.Max(result.longestWorkMilliseconds, frame.workMilliseconds);
            if (_allocationAvailable) result.managedBytes += frame.managedBytes;
            result.minimumActiveFairies = Math.Min(result.minimumActiveFairies, frame.activeFairies);
            result.maximumActiveFairies = Math.Max(result.maximumActiveFairies, frame.activeFairies);
            if (frame.foreground) result.foregroundSamples++;
            if (i == 0) continue;
            double interval = frames[i].elapsedMilliseconds - frames[i - 1].elapsedMilliseconds;
            intervals[i - 1] = interval;
            result.averageIntervalMilliseconds += interval;
            result.longestIntervalMilliseconds = Math.Max(result.longestIntervalMilliseconds, interval);
            if (interval > 25) result.intervalsOver25ms++;
            if (interval > 1000d / 30d) result.intervalsOver33ms++;
            if (interval > 50) result.intervalsOver50ms++;
            if (interval > 100) result.intervalsOver100ms++;
        }
        Array.Sort(intervals); Array.Sort(costs);
        result.averageWorkMilliseconds /= count;
        result.managedBytesPerCall = _allocationAvailable ? result.managedBytes / (double)count : -1;
        result.p95WorkMilliseconds = Percentile(costs, .95);
        if (intervals.Length > 0)
        {
            result.averageIntervalMilliseconds /= intervals.Length;
            result.observedHertz = 1000d / result.averageIntervalMilliseconds;
            result.p95IntervalMilliseconds = Percentile(intervals, .95);
            result.p99IntervalMilliseconds = Percentile(intervals, .99);
        }
        return result;
    }

    private static double Percentile(double[] values, double percentile) => values.Length == 0 ? 0
        : values[Math.Min(values.Length - 1, Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1))];

    private static bool ProbeAllocationCounter()
    {
        try
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var retained = new byte[8192]; retained[0] = 1;
            bool counted = GC.GetAllocatedBytesForCurrentThread() - before >= retained.Length;
            GC.KeepAlive(retained); return counted;
        }
        catch (NotSupportedException) { return false; }
    }
}
#endif
