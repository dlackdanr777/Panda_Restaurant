#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Optional, explicit evidence capture of the preview RenderTexture. No desktop capture,
/// simulation stepping or asset import. The external encoder is supplied by the verifier.
/// Timestamp gaps repeat the last frame at 60 FPS rather than speeding up the action.
/// </summary>
public sealed class GachaCollectionVideoRecorder : IDisposable
{
    private const int Fps = 60;
    private readonly object _gate = new object();
    private readonly Queue<Frame> _frames = new Queue<Frame>();
    private readonly Stack<byte[]> _pool = new Stack<byte[]>();
    private sealed class Frame { public int index; public byte[] bytes; }
    private readonly Process _encoder;
    private readonly Task _writer;
    private readonly Task<string> _stderr;
    private readonly string _path;
    private readonly double _started, _duration;
    private int _lastRequested = -1, _pending, _captured, _skipped, _written, _duplicates;
    private bool _stopping, _disposed;
    private string _error;
    public bool IsFinished => _writer.IsCompleted;

    [Serializable] private sealed class Evidence
    {
        public string file, startedUtc, finishedUtc, error, method;
        public int width, height, savedFps, encodedFrames, capturedFrames, repeatedFrames, skippedCaptures, encoderExitCode;
        public double durationSeconds;
    }
    private readonly Evidence _evidence;

    public GachaCollectionVideoRecorder(string encoderPath, string outputPath, int width, int height, double seconds)
    {
        if (!SystemInfo.supportsAsyncGPUReadback) throw new NotSupportedException("Async GPU readback is required for recording.");
        if (!File.Exists(encoderPath)) throw new FileNotFoundException("Supply the evidence-only FFmpeg executable.", encoderPath);
        _path = Path.GetFullPath(outputPath); Directory.CreateDirectory(Path.GetDirectoryName(_path));
        _duration = seconds; _started = EditorApplication.timeSinceStartup;
        _evidence = new Evidence { file = _path, startedUtc = DateTime.UtcNow.ToString("O"), width = width, height = height,
            savedFps = Fps, durationSeconds = seconds,
            method = "Actual preview Camera.Render frames; asynchronous GPU readback; timestamp-based 60 FPS encoding. Missing render frames repeat; no simulation advancement by recorder. Performance measurements are separate from recording." };
        for (int i = 0; i < 6; i++) _pool.Push(new byte[width * height * 4]);
        _encoder = new Process { StartInfo = new ProcessStartInfo {
            FileName = Path.GetFullPath(encoderPath), UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true,
            Arguments = "-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgba -video_size " + width + "x" + height +
                " -framerate " + Fps + " -i pipe:0 -vf vflip -an -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \"" + _path + "\""
        }};
        _encoder.Start(); _stderr = _encoder.StandardError.ReadToEndAsync();
        _writer = Task.Run((Action)WriteFrames);
    }

    // Called only after the preview camera really rendered. Never called from a simulation tick.
    public void Capture(RenderTexture source, double now)
    {
        if (_disposed || _stopping) return;
        if (now - _started >= _duration) { Stop(); return; }
        int index = (int)((now - _started) * Fps);
        if (index <= _lastRequested) return;
        _lastRequested = index;
        byte[] bytes;
        lock (_gate)
        {
            if (_pool.Count == 0) { _skipped++; return; }
            bytes = _pool.Pop(); _pending++;
        }
        AsyncGPUReadback.Request(source, 0, TextureFormat.RGBA32, request => {
            lock (_gate)
            {
                _pending--;
                if (request.hasError) { _error = "GPU readback failed"; _pool.Push(bytes); }
                else { request.GetData<byte>().CopyTo(bytes); _frames.Enqueue(new Frame { index = index, bytes = bytes }); _captured++; }
                Monitor.PulseAll(_gate);
            }
        });
    }

    public void Stop()
    {
        lock (_gate) { _stopping = true; Monitor.PulseAll(_gate); }
    }

    private void WriteFrames()
    {
        byte[] previous = null;
        try
        {
            var stream = _encoder.StandardInput.BaseStream;
            int last = -1;
            while (true)
            {
                Frame frame;
                lock (_gate)
                {
                    while (_frames.Count == 0 && !(_stopping && _pending == 0)) Monitor.Wait(_gate, 100);
                    if (_frames.Count == 0 && _stopping && _pending == 0) break;
                    frame = _frames.Dequeue();
                }
                if (frame.index <= last) { lock (_gate) _pool.Push(frame.bytes); continue; }
                byte[] filler = previous ?? frame.bytes;
                while (last + 1 < frame.index) { stream.Write(filler, 0, filler.Length); last++; _written++; _duplicates++; }
                stream.Write(frame.bytes, 0, frame.bytes.Length); last++; _written++;
                if (previous != null) lock (_gate) _pool.Push(previous);
                previous = frame.bytes;
            }
            int total = (int)Math.Round(_duration * Fps);
            while (previous != null && _written < total)
            { stream.Write(previous, 0, previous.Length); _written++; _duplicates++; }
            stream.Flush(); _encoder.StandardInput.Close();
            if (!_encoder.WaitForExit(20000)) throw new TimeoutException("Evidence encoder did not finish.");
            _evidence.encoderExitCode = _encoder.ExitCode;
            if (_encoder.ExitCode != 0) _error = _stderr.GetAwaiter().GetResult();
        }
        catch (Exception error) { _error = error.ToString(); }
        finally
        {
            _evidence.finishedUtc = DateTime.UtcNow.ToString("O"); _evidence.error = _error;
            _evidence.encodedFrames = _written; _evidence.capturedFrames = _captured;
            _evidence.repeatedFrames = _duplicates; _evidence.skippedCaptures = _skipped;
            // Serialize on main-thread PollCompletion, not from a worker using Unity APIs.
        }
    }

    public void PollCompletion()
    {
        if (_disposed || !_writer.IsCompleted) return;
        File.WriteAllText(Path.ChangeExtension(_path, ".json"), JsonUtility.ToJson(_evidence, true));
        if (!string.IsNullOrEmpty(_error)) UnityEngine.Debug.LogError("COLLECTION_VIDEO_FAILED: " + _error);
        else UnityEngine.Debug.Log("COLLECTION_VIDEO_COMPLETED: " + _path);
        _encoder.Dispose(); _disposed = true;
    }

    public void Dispose()
    {
        Stop();
        if (_pending > 0) AsyncGPUReadback.WaitAllRequests();
        // Encoder work is bounded and never contains Unity calls.
        if (_writer.Wait(30000)) PollCompletion();
    }
}
#endif
