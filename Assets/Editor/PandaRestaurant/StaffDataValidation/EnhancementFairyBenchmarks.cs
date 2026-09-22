#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Explicit offline microbenchmark. It does not run until requested by the demo/test host.
/// Measures real managed behaviour and SpriteRenderer property work, excluding GPU and scene rendering.
/// </summary>
public static class EnhancementFairyBenchmarks
{
    [Serializable]
    public sealed class Sample
    {
        public string name;
        public int ownedTypes;
        public int activeFairies;
        public int pooledObjects;
        public int steps;
        public double simulatedSeconds;
        public double elapsedMilliseconds;
        public double millisecondsPerStep;
        public long managedBytes;
        public double managedBytesPerStep;
        public bool hiddenSimulationSuspended;
    }

    [Serializable]
    public sealed class Report
    {
        public string measuredUtc;
        public string unityVersion;
        public string platform;
        public bool editor;
        public bool playing;
        public int catalogEnhancementCount;
        public int warmupSteps;
        public bool managedAllocationCounterAvailable;
        public long allocationProbeBytes;
        public string scope = "Synchronous main-thread microbenchmark: behaviour and SpriteRenderer property updates only. GPU/render thread/actual frame rate/native allocations excluded. Managed bytes are GC.GetAllocatedBytesForCurrentThread; no forced GC.";
        public bool unityRandomStateUnchanged;
        public Sample[] samples;
    }

    /// <summary>Use the already detached demo catalogue; this never resolves ItemManager, UserInfo or the SDK.</summary>
    public static Report Run(IEnumerable<GachaItemData> catalog, int measuredSteps = 1200)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (measuredSteps < 1 || measuredSteps > 12000) throw new ArgumentOutOfRangeException(nameof(measuredSteps));
        var items = catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && item.Sprite != null)
            .GroupBy(item => item.Id, StringComparer.Ordinal).Select(group => group.First())
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        if (items.Count < 12) throw new InvalidOperationException("Supply at least 12 real enhancement items with sprites.");
        var sourceSettings = Resources.Load<EnhancementFairySettings>(EnhancementFairySettings.ResourcePath);
        if (sourceSettings == null) throw new InvalidOperationException("The checked-in fairy settings asset is required.");
        var settings = Object.Instantiate(sourceSettings);
        settings.hideFlags = HideFlags.HideAndDontSave;
        settings.MaxActive = 12;
        const int warmup = 120;
        const float dt = 1f / 60f;
        var randomBefore = UnityEngine.Random.state;
        bool allocationAvailable = ProbeAllocationCounter(out long probeBytes);
        try
        {
            var report = new Report
            {
                measuredUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                editor = Application.isEditor,
                playing = Application.isPlaying,
                catalogEnhancementCount = items.Count,
                warmupSteps = warmup,
                managedAllocationCounterAvailable = allocationAvailable,
                allocationProbeBytes = probeBytes,
                samples = new[]
                {
                    MeasureBrains(items, settings, warmup, measuredSteps, dt, allocationAvailable),
                    MeasureHabitat(items, settings, 12, false, warmup, measuredSteps, dt, allocationAvailable),
                    MeasureHabitat(items, settings, Mathf.Min(90, items.Count), false, warmup, measuredSteps, dt, allocationAvailable),
                    MeasureHabitat(items, settings, Mathf.Min(90, items.Count), true, warmup, measuredSteps, dt, allocationAvailable)
                },
                unityRandomStateUnchanged = randomBefore.Equals(UnityEngine.Random.state)
            };
            if (!report.unityRandomStateUnchanged)
                throw new InvalidOperationException("Fairy benchmark unexpectedly modified the Unity/gacha random state.");
            if (!allocationAvailable)
                report.scope += " This runtime did not count a retained 8 KiB allocation; managedBytes=-1 means unavailable, not zero allocation.";
            return report;
        }
        finally { Object.DestroyImmediate(settings); }
    }

    public static Report WriteReport(string outputPath, IEnumerable<GachaItemData> catalog, int measuredSteps = 1200)
    {
        var report = Run(catalog, measuredSteps);
        string path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return report;
    }

    private static Sample MeasureBrains(List<GachaItemData> items, EnhancementFairySettings settings,
        int warmup, int measuredSteps, float dt, bool allocationAvailable)
    {
        var brains = new EnhancementFairyBrain[12];
        for (int i = 0; i < brains.Length; i++) brains[i] = new EnhancementFairyBrain(items[i].Id, settings);
        for (int i = 0; i < warmup; i++) StepBrains(brains, dt);
        var timer = new Stopwatch();
        long startBytes = allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
        timer.Start();
        for (int i = 0; i < measuredSteps; i++) StepBrains(brains, dt);
        timer.Stop();
        long allocated = allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - startBytes : -1;
        return SampleFor("12 independent brains", 12, 12, 0, measuredSteps, dt, timer, allocated, false);
    }

    private static void StepBrains(EnhancementFairyBrain[] brains, float dt)
    {
        for (int i = 0; i < brains.Length; i++)
            brains[i].Step(dt, brains[(i + 1) % brains.Length].GroundPosition, true);
    }

    private static Sample MeasureHabitat(List<GachaItemData> items, EnhancementFairySettings settings,
        int ownedTypes, bool hidden, int warmup, int measuredSteps, float dt, bool allocationAvailable)
    {
        var host = new GameObject("Detached fairy microbenchmark") { hideFlags = HideFlags.HideAndDontSave };
        // Synchronous calls are completed and destroyed before the next rendered frame.
        host.transform.position = new Vector3(10000f, 10000f, 0f);
        try
        {
            var habitat = host.AddComponent<EnhancementFairyHabitat>();
            habitat.ConfigureOffline(items, items.Take(ownedTypes).Select(item => item.Id), settings);
            for (int i = 0; i < warmup; i++) habitat.AdvancePreview(dt);
            var witness = habitat.GetActiveBrain(0);
            if (hidden) habitat.SetFloorVisible(true, false);
            float simulationBefore = witness.ElapsedSeconds;
            var timer = new Stopwatch();
            long startBytes = allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
            timer.Start();
            for (int i = 0; i < measuredSteps; i++) habitat.AdvancePreview(dt);
            timer.Stop();
            long allocated = allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - startBytes : -1;
            bool suspended = hidden && !habitat.enabled && witness.ElapsedSeconds == simulationBefore;
            if (habitat.PooledObjectCount > 12 || habitat.OwnedTypeCount != ownedTypes || (hidden && !suspended))
                throw new InvalidOperationException("Fairy pool/ownership/visibility invariant failed during the benchmark.");
            return SampleFor(hidden ? "Hidden habitat" : "Visible habitat", ownedTypes, habitat.ActiveCount,
                habitat.PooledObjectCount, measuredSteps, dt, timer, allocated, suspended);
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static Sample SampleFor(string name, int owned, int active, int pool, int steps, float dt,
        Stopwatch timer, long allocated, bool suspended) => new Sample
    {
        name = name, ownedTypes = owned, activeFairies = active, pooledObjects = pool, steps = steps,
        simulatedSeconds = steps * (double)dt, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
        millisecondsPerStep = timer.Elapsed.TotalMilliseconds / steps,
        managedBytes = allocated, managedBytesPerStep = allocated < 0 ? -1 : allocated / (double)steps,
        hiddenSimulationSuspended = suspended
    };

    private static bool ProbeAllocationCounter(out long countedBytes)
    {
        try
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[8192];
            probe[0] = 1;
            countedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(probe);
            return countedBytes >= probe.Length;
        }
        catch (NotSupportedException) { countedBytes = -1; return false; }
    }
}
#endif
