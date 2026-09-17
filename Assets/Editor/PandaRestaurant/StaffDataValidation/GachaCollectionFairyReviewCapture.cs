#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit Temp-request-only foreground probe. Never enters Play or edits the user's scenes.</summary>
[InitializeOnLoad]
public static class GachaCollectionFairyReviewCapture
{
    public const string DirectoryPath = "Logs/GachaReview20260917-2213";
    private static readonly string RequestPath = Path.Combine("Temp/GachaReview20260917-2213", "fairy-request.txt");
    private static GachaCollectionPreviewWindow _window;
    private static GachaCollectionPreviewSceneWitness _witness;
    private static string _phase, _started;
    private static double _nextPoll, _deadline;
    private static int _population, _stage;
    private static readonly List<string> Outputs = new List<string>();
    public static bool IsRunning => _stage != 0;

    [Serializable] private sealed class Result
    {
        public bool success;
        public string phase, startedUtc, finishedUtc, witnessBefore, witnessAfter, error;
        public string[] outputs;
    }

    static GachaCollectionFairyReviewCapture() => EditorApplication.update += Tick;

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        double now = EditorApplication.timeSinceStartup;
        if (_stage == 0)
        {
            if (now < _nextPoll) return;
            _nextPoll = now + .25;
            if (!File.Exists(RequestPath)) return;
            string request = File.ReadAllText(RequestPath).Trim(); File.Delete(RequestPath);
            try { Begin(request); } catch (Exception error) { Fail(error); }
            return;
        }
        try
        {
            if (_window == null || !_window.IsReady) throw new InvalidOperationException("The measured preview was closed.");
            if (now < _deadline) return;
            if (_stage == 1)
            {
                string file = Path.Combine(DirectoryPath, "fairy-" + _phase + "-" + _population + ".json");
                EnhancementFairyFrameProbe.Begin(_phase, file, _population);
                _deadline = now + 10; _stage = 2;
            }
            else
            {
                var report = EnhancementFairyFrameProbe.Finish();
                Outputs.Add("fairy-" + _phase + "-" + _population + ".json");
                if (report.simulation.count < 2 || report.rendering.count < 2)
                    throw new InvalidOperationException("The host did not record actual simulation/render frames.");
                if (report.simulation.minimumActiveFairies != _population || report.simulation.maximumActiveFairies != _population)
                    throw new InvalidOperationException("The requested fairy population was not maintained.");
                _witness.AssertUnchanged("foreground fairy measurement");
                if (_population == 1) PreparePopulation(12);
                else Complete();
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Begin(string phase)
    {
        if (phase != "baseline" && phase != "improved") throw new InvalidOperationException("Use baseline or improved.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit-mode preview required.");
        Directory.CreateDirectory(DirectoryPath);
        _phase = phase; _started = DateTime.UtcNow.ToString("O"); Outputs.Clear();
        _witness = GachaCollectionPreviewSceneWitness.Capture();
        _window = GachaCollectionPreviewWindow.Open();
        _window.Focus();
        _witness.AssertUnchanged("foreground fairy preview opened");
        PreparePopulation(1);
    }

    private static void PreparePopulation(int population)
    {
        _population = population;
        _window.ShowFairies();
        _window.Fairies.ResetOfflineSession(_window.Catalog.OfType<GachaItemData>()
            .Where(EnhancementFairyCatalog.IsEligible).OrderBy(item => item.Id, StringComparer.Ordinal)
            .Take(population).Select(item => item.Id));
        _window.Focus(); _window.Repaint();
        _deadline = EditorApplication.timeSinceStartup + 2; _stage = 1;
    }

    private static void Complete()
    {
        _stage = 0;
        _witness.AssertUnchanged("both populations measured");
        WriteResult(null);
        Debug.Log("FAIRY_REVIEW_CAPTURE_COMPLETED: " + _phase);
    }

    private static void Fail(Exception error)
    {
        _stage = 0; EnhancementFairyFrameProbe.Cancel();
        try { _witness?.AssertUnchanged("fairy measurement failure"); }
        catch (Exception preservationError) { error = new AggregateException(error, preservationError); }
        WriteResult(error);
        Debug.LogError("FAIRY_REVIEW_CAPTURE_FAILED: " + error);
    }

    private static void WriteResult(Exception error)
    {
        Directory.CreateDirectory(DirectoryPath);
        var result = new Result
        {
            success = error == null, phase = _phase, startedUtc = _started, finishedUtc = DateTime.UtcNow.ToString("O"),
            witnessBefore = _witness?.Summary, witnessAfter = GachaCollectionPreviewSceneWitness.Capture().Summary,
            error = error?.ToString(), outputs = Outputs.ToArray()
        };
        File.WriteAllText(Path.Combine(DirectoryPath, "fairy-" + (_phase ?? "request") + "-verification.json"), JsonUtility.ToJson(result, true));
    }
}
#endif
