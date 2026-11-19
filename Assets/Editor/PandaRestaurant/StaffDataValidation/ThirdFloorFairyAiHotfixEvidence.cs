#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Muks.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Dynamic production-path observation in the existing SDK-free real Stage1 host. Never Play evidence.</summary>
public static class ThirdFloorFairyAiHotfixEvidence
{
    public const string OutputDirectory = "Temp/PopupStoreVerification/FairyAiHotfix";

    [MenuItem("Tools/Panda Restaurant/3층 요정 AI/현재 동작 동적 진단")]
    public static void RunCurrentDiagnostic() => RunObservation("current");

    [MenuItem("Tools/Panda Restaurant/3층 요정 AI/수정 후 동적 검증")]
    public static void RunAfterFix() => RunObservation("after-fix");

    [MenuItem("Tools/Panda Restaurant/3층 요정 AI/자연스러운 등장 30·60Hz 실측")]
    public static void RunFastEmergenceEvidence()
    {
        RequireSafeEditor();
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("NEW natural-emergence observation. No previous test or evidence result is reused.");
        report.AppendLine("Scope: saved Stage1, actual Floor3/Runtime Habitat/Brain, real catalog sprites and authored settings; memory-only ownership in the existing host.");
        report.AppendLine("The house owner/entrance/settings are registered with Habitat; this measures actual RequestHouseEmergence, NOT House.TryTapAtWorldPoint, Camera input, MonoBehaviour lifecycle, Play or GameView. Existing HouseTests separately exercise the actual input handler.");
        report.AppendLine("Each scenario advances actual logic at 30 or 60Hz. Request, visible Emerging, full appearance, first dispersion and full-body house-zone clearance are separate events.");
        report.AppendLine("Stages 1/2/3/4 request 1/2/3/3 at 0/.5/1/1.5s. Burst3 requests one group of three. Hardcap scenarios enqueue 12 at once; later groups share the existing queue and are explicitly measured as backlog, not isolated taps.");
        report.AppendLine("Temporary exit overlap is allowed. Raw bounds, deep-overlap pairs, house dwell and same-route/yielding durations remain recorded; no walking-state exemption erases a moving crowd.");
        report.AppendLine("Only the first planned exit uses the authored TapDispersalSpeedMultiplier. Ordinary AI and the existing calm return keep their prior speeds; the flag is included in frame rows.");
        report.AppendLine("3/6/12/30-second checkpoints report original visitors not fully clear and current house bodies. Width/speed finite-clearance checks do not imply acceptable crowding: raw residuals and longest actual stationary/yield waits remain explicit.");
        report.AppendLine("Motion CSV records actual floor-contact hop distance, maximum horizontal frame displacement/speed, visible lift and landing, first-walk speed, state-transition continuity and route-end time. Body/root X and rendered width are also recorded in frames; those visual values can change with scale/rotation.");
        report.AppendLine("The authored .18-second stagger/.45-second hop replaces the old .3-second burst target. A default initial multiplier of 1 uses the original calm walking speed; no occupancy admission gate is restored.");
        report.AppendLine("30Hz Burst3/hardcap/wide captures at 0/.1/.25/.45/.65/.85/1/3/6/12/30s are actual progressed renderer-only Edit Mode composites. Original material/mask/HUD/input and mobile FPS are NOT verified.");
        report.AppendLine("No account, SDK, reward, gacha transaction, ownership persistence, Scene save or Play execution.");
        foreach (int fps in new[] { 30, 60 })
        {
            ObserveFastScenario(fps, "stages", 40, false, report);
            ObserveFastScenario(fps, "burst3", 40, false, report);
            ObserveFastScenario(fps, "hardcap", 40, false, report);
            ObserveFastScenario(fps, "shortfall", 14, false, report);
            ObserveFastScenario(fps, "hardcap-wide", 24, true, report);
        }
        File.WriteAllText(Path.Combine(OutputDirectory, "natural-emergence-summary.txt"), report.ToString());
    }

    private static void ObserveFastScenario(int fps, string label, int owned, bool wide, StringBuilder report)
    {
        string prefix = "natural-emergence-" + fps + "hz-" + label;
        float dt = 1f / fps;
        using (var fixture = new FairyAiRuntimeStageFixture(owned, wide))
        using (var frames = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-frames.csv")))
        using (var pairs = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-pairs.csv")))
        using (var snapshots = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-snapshots.csv")))
        {
            var timing = new FairyFastEmergenceMetrics(fixture);
            var crowd = new FairyHouseCrowdMetrics(fixture);
            float start = fixture.Clock;
            frames.WriteLine("frame,elapsed,id,housePhase,alpha,houseScale,groundX,groundY,minX,maxX,minY,maxY,dispersing,dispersalTargetX,movementTargetX,intendedDirection,spacingYielding,eligible,birthProtected,nearHouse,zoneDwell,initialHouseDispersal,houseLift,rootX,bodyX,bodyWidth");
            pairs.WriteLine("elapsed,first,second,classification,overlapFraction,continuousRawSeconds,continuousSameDirectionSeconds,bothNearHouse,continuousNearHouseSameDirectionSeconds,continuousStationarySeconds");
            snapshots.WriteLine("frame,elapsed,active,pending,emerged,returning,bodyNear,longPresence,cohortSize,originalVisitors,originalVisitorsCleared,firstDispersals,leftExits,rightExits,nearSameRouteOverlap,nearCohortSeconds,wholeRoomSameRouteOverlap,wholeRoomCohortSeconds");
            report.AppendLine("\nScenario=" + label + " | fps=" + fps + " | owned=" + owned
                + " | interval=" + F(fixture.House.Settings.SafeSpawnInterval) + " | entranceDuration=" + F(fixture.House.Settings.SafeEntranceDuration)
                + " | firstDelay=" + F(fixture.House.Settings.SafeTapSpawnFirstDelay)
                + " | calmSpeed=" + F(fixture.CalmSpeed) + " | initialExitMultiplier=" + F(fixture.House.Settings.SafeTapDispersalSpeedMultiplier)
                + " | hardcap=" + fixture.Habitat.HouseHardCap);
            float[] captures = { 0f, .1f, .25f, .45f, .65f, .85f, 1f, 3f, 6f, 12f, 30f };
            int captureIndex = 0;
            bool shouldCapture = fps == 30 && (label == "burst3" || label.StartsWith("hardcap", StringComparison.Ordinal));
            for (int frame = 0; frame <= 30 * fps; frame++)
            {
                if (frame > 0) fixture.Step(dt);
                if (label == "stages" && frame <= fps * 3 / 2 && frame % (fps / 2) == 0)
                {
                    int stage = frame / (fps / 2) + 1;
                    timing.Request(stage, fixture.House.Settings.SpawnCountForStage(stage));
                }
                else if (frame == 0)
                {
                    if (label.StartsWith("hardcap", StringComparison.Ordinal))
                        for (int stage = 1; stage <= 5; stage++) timing.Request(stage, Mathf.Min(stage, 3));
                    else timing.Request(3, 3);
                }
                if (label == "shortfall" && frame == fps / 2) timing.Request(4, 3);
                if (label.StartsWith("hardcap", StringComparison.Ordinal) && frame == fps * 2) timing.Request(6, 3);
                timing.Observe();
                crowd.Observe(frame == 0 ? 0f : dt);
                float elapsed = fixture.Clock - start;
                foreach (var body in crowd.Bodies)
                {
                    object view = fixture.Views.Cast<object>().Single(value => ((EnhancementFairyBrain)EnhancementFairyStage1Host.Get(value, "Brain")).ItemId == body.Id);
                    frames.WriteLine(string.Join(",", frame, F(elapsed), Csv(body.Id), Csv(body.HousePhase), F(fixture.Body(body.Id).color.a),
                        F((float)EnhancementFairyStage1Host.Get(view, "HouseScale")), F(body.Ground.x), F(body.Ground.y),
                        F(body.Bounds.min.x), F(body.Bounds.max.x), F(body.Bounds.min.y), F(body.Bounds.max.y),
                        body.Dispersing, F(body.DispersalTarget.x), F(body.MovementTarget.x), F(body.IntendedDirection), body.SpacingYielding,
                        body.Eligible, body.BirthProtected, body.NearHouse, F(body.DwellSeconds),
                        (bool)EnhancementFairyStage1Host.Get(view, "InitialHouseDispersal"),
                        F((float)EnhancementFairyStage1Host.Get(view, "HouseLift")),
                        F(((Transform)EnhancementFairyStage1Host.Get(view, "Root")).position.x),
                        F(fixture.Body(body.Id).transform.position.x), F(body.Bounds.size.x)));
                }
                foreach (var pair in crowd.Pairs)
                    pairs.WriteLine(string.Join(",", F(elapsed), Csv(pair.First), Csv(pair.Second), Csv(pair.Classification),
                        F(pair.Ratio), F(pair.RawSeconds), F(pair.SameDirectionSeconds), pair.BothNearHouse,
                        F(pair.NearHouseSameDirectionSeconds), F(pair.StationarySeconds)));
                snapshots.WriteLine(string.Join(",", frame, F(elapsed), fixture.Habitat.ActiveCount, fixture.Habitat.PendingHouseSpawnCount,
                    fixture.Habitat.HouseEmergenceCount, fixture.Habitat.ReturningHouseCount, crowd.BodyNearCount, crowd.LongPresenceCount,
                    crowd.PersistentCohortSize, crowd.InitialVisitorCount, crowd.InitialVisitorsCleared, crowd.FirstDispersalCount,
                    crowd.LeftExits, crowd.RightExits, F(crowd.MaximumSameDirectionOverlapSeconds), F(crowd.MaximumLongCohortSeconds),
                    F(crowd.WholeRoomMaximumSameRouteOverlapSeconds), F(crowd.WholeRoomMaximumCohortSeconds)));
                if (frame == fps * 3 || frame == fps * 6 || frame == fps * 12 || frame == fps * 30)
                    report.AppendLine("Checkpoint " + (frame / fps) + "s: bodyNear=" + crowd.BodyNearCount
                        + " | originalVisitorsNotYetClear=" + (crowd.InitialVisitorCount - crowd.InitialVisitorsCleared)
                        + " | currentPersistentCohort=" + crowd.PersistentCohortSize + " | " + crowd.Describe());
                if (shouldCapture && captureIndex < captures.Length && frame >= Mathf.RoundToInt(captures[captureIndex] * fps))
                {
                    Capture(fixture.Host, fixture.Area, prefix + "-" + captures[captureIndex].ToString("0.00", CultureInfo.InvariantCulture) + "s", report);
                    captureIndex++;
                }
            }
            using (var events = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-events.csv")))
            {
                events.WriteLine("requestIndex,stage,requested,accepted,requestedAt,id,emergingAt,completedAt,dispersingAt,wholeBodyClearedAt,requestToStart,requestToComplete,alphaAtStart,scaleAtStart,alphaAtComplete,scaleAtComplete");
                for (int index = 0; index < timing.Requests.Count; index++)
                {
                    var request = timing.Requests[index];
                    if (request.Visitors.Count == 0)
                        events.WriteLine(string.Join(",", index, request.Stage, request.Requested, request.Accepted, F(request.RequestedAt - start), "none", "", "", "", "", "", "", "", "", "", ""));
                    foreach (var visitor in request.Visitors)
                        events.WriteLine(string.Join(",", index, request.Stage, request.Requested, request.Accepted, F(request.RequestedAt - start), Csv(visitor.Id),
                            F(visitor.StartedAt - start), EventTime(visitor.CompletedAt, start), EventTime(visitor.DispersingAt, start), EventTime(visitor.ClearedAt, start),
                            F(visitor.StartedAt - request.RequestedAt), visitor.CompletedAt < 0f ? "pending" : F(visitor.CompletedAt - request.RequestedAt),
                            F(visitor.AlphaAtStart), F(visitor.ScaleAtStart), F(visitor.AlphaAtCompletion), F(visitor.ScaleAtCompletion)));
                    report.AppendLine("Request stage=" + request.Stage + " requested/accepted=" + request.Requested + "/" + request.Accepted
                        + " at=" + F(request.RequestedAt - start) + " | startOffsets="
                        + string.Join("/", request.Visitors.Select(visitor => F(visitor.StartedAt - request.RequestedAt)))
                        + " | completeOffsets=" + string.Join("/", request.Visitors.Select(visitor => visitor.CompletedAt < 0f ? "pending" : F(visitor.CompletedAt - request.RequestedAt))));
                }
            }
            using (var motion = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-motion.csv")))
            {
                motion.WriteLine("id,houseStartX,houseStartY,houseEndX,houseEndY,actualHopDistance,emergencePathLength,maxEmergenceFrameDelta,maxEmergenceSpeed,maxHouseLift,landingLift,landingX,landingY,maxInitialFrameDelta,maxInitialSpeed,maxTransitionFrameDelta,maxTransitionSpeed,initialEndedAt,maxInitialStationarySeconds,maxInitialYieldSeconds,clearedAt");
                foreach (var visitor in timing.Visitors)
                    motion.WriteLine(string.Join(",", Csv(visitor.Id), F(visitor.HouseStart.x), F(visitor.HouseStart.y), F(visitor.HouseEnd.x), F(visitor.HouseEnd.y),
                        F(visitor.ActualHopDistance), F(visitor.EmergencePathLength), F(visitor.MaximumEmergenceStep), F(visitor.MaximumEmergenceSpeed),
                        F(visitor.MaximumHouseLift), F(visitor.HouseLiftAtCompletion), F(visitor.GroundAtCompletion.x), F(visitor.GroundAtCompletion.y),
                        F(visitor.MaximumInitialStep), F(visitor.MaximumInitialSpeed), F(visitor.MaximumTransitionStep), F(visitor.MaximumTransitionSpeed),
                        EventTime(visitor.InitialEndedAt, start), F(visitor.MaximumInitialStationarySeconds), F(visitor.MaximumInitialYieldSeconds), EventTime(visitor.ClearedAt, start)));
            }
            var measured = timing.Visitors.ToArray();
            report.AppendLine("Actual motion: maxHopDistance=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.ActualHopDistance))
                + " | maxEmergenceHorizontalSpeed=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.MaximumEmergenceSpeed))
                + " | maxEmergenceFrameDelta=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.MaximumEmergenceStep))
                + " | maxInitialWalkSpeed=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.MaximumInitialSpeed))
                + " | maxInitialStationaryWait=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.MaximumInitialStationarySeconds))
                + " | maxInitialYieldWait=" + F(measured.Length == 0 ? 0f : measured.Max(visitor => visitor.MaximumInitialYieldSeconds))
                + " | notFullyClearAt30s=" + measured.Count(visitor => visitor.ClearedAt < 0f)
                + " | initialRoutesStillActiveAt30s=" + measured.Count(visitor => visitor.DispersingAt >= 0f && visitor.InitialEndedAt < 0f));
            report.AppendLine("Actual peak=" + timing.PeakActive + " | accepted=" + timing.Requests.Sum(request => request.Accepted)
                + " | actual emerged=" + fixture.Habitat.HouseEmergenceCount + " | end pending=" + fixture.Habitat.PendingHouseSpawnCount);
            report.AppendLine(crowd.Describe());
            report.AppendLine("First direction reversals=" + crowd.ExitDirectionReversals + "; outward violations=" + crowd.ExitOutwardViolations
                + "; original visitors removed before full clearance=" + crowd.InitialVisitors.Values.Count(visitor => visitor.RemovedBeforeClearance));
        }
    }

    private static string EventTime(float value, float start) => value < 0f ? "pending" : F(value - start);

    [MenuItem("Tools/Panda Restaurant/3층 요정 AI/실제 Sprite 겹침 동적 재검증")]
    public static void RunCrowdRecheck()
    {
        RequireSafeEditor();
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Recheck scope: actual saved Stage1 runtime initialization, shared production furniture binding, actual house owner/entrance/settings, real catalog sprites, memory-only ownership.");
        report.AppendLine("Furniture activity AI was retired by the final approved design. Existing walking speeds, birth/return protections and house deadlines remain enabled; no position staging.");
        report.AppendLine("The existing house-return presentation seed remains repeatable; ordinary fairy movement and house scheduling still run.");
        report.AppendLine("Six 180-second observations: base12, +1, +3, +6 (18 target), +12 (24 target), +12 with the catalog's four widest normalized sprites in a 24-owned roster.");
        report.AppendLine("Requests follow 1/2/3/3/3 stages as needed. CSV and summary report actual admission/peak, so queue cancellation cannot masquerade as successful dispersion.");
        report.AppendLine("Body bounds are sampled every 1/30s. House zone uses actual sprite horizontal intersection with the configured radius; movement/dispersal does NOT erase dwell.");
        report.AppendLine("Deep pair overlap: >=25% of the narrower rendered body's width and >=20% vertical overlap. Every near-house overlap is logged, including protected presentations.");
        report.AppendLine("Eligible excludes only real birth protection, house emergence/return and replacement/fade. Opposite-direction and single-moving passes remain explicit classifications.");
        report.AppendLine("Same-route dispersal followers remain a moving cohort while yielding: movement target/direction and SpacingYielding are logged independently of actual per-frame movement. A paused follower does not reset continuous same-route overlap.");
        report.AppendLine("Pairs CSV also includes the entire room, with bothNearHouse reported separately. Whole-room same-route, stationary-pair and 3+ connected-cohort durations are diagnostic values, not a zero-overlap requirement for normal crossings.");
        report.AppendLine("Persistent cohort: >=3 connected eligible deep-overlap bodies, each continuously present >=3s, with same-direction or stationary edges. Allowed two residents and genuine transient passes are not forced to zero.");
        report.AppendLine("Raw long presence beyond two is also reported regardless movement, without silently treating all walking as an exception.");
        report.AppendLine("Natural appearance permits temporary doorway overlap. The former accelerated 12-second clearance/3-second cohort criteria do not apply. Actual full-body progress is checked against a finite actual-width/radius/calm-speed budget; this is a bounded-progress criterion, not visual approval. Raw overlap, 30-second residuals and whole-room cohorts must be reviewed separately.");
        report.AppendLine("Captures at 0/3/6/9/12/20/30/60/90s use actual progressed state. They are renderer-only Edit Mode composites (material/mask/HUD/input not verified), NOT actual GameView or Play evidence.");
        report.AppendLine("No account/SDK/transactions/operational ownership or scene save. Earlier baseline and after-fix artifacts are retained unchanged.");
        foreach (int extra in new[] { 0, 1, 3, 6, 12 }) ObserveCrowdScenario(extra, false, report);
        ObserveCrowdScenario(12, true, report);
        File.WriteAllText(Path.Combine(OutputDirectory, "crowd-recheck-summary.txt"), report.ToString());
    }

    private static void ObserveCrowdScenario(int extra, bool wide, StringBuilder report)
    {
        string label = extra == 0 ? "base12" : "add" + extra + "-target" + (12 + extra);
        if (wide) label += "-wide";
        string prefix = "crowd-recheck-" + label;
        using (var fixture = new FairyAiRuntimeStageFixture(extra == 0 ? 12 : wide ? 24 : 40, wide))
        using (var bodies = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-bodies.csv")))
        using (var pairs = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-pairs.csv")))
        using (var snapshots = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-snapshots.csv")))
        {
            bodies.WriteLine("elapsed,id,groundX,groundY,minX,maxX,minY,maxY,housePhase,dispersing,dispersalTargetX,dispersalTargetY,eligible,birthProtected,replacing,nearHouse,moving,stepX,zoneDwell,productionZoneSeconds,productionOverlapSeconds,exitSide,spacingHalfWidth,movementTargetX,movementTargetY,intendedDirection,directedWalking,spacingYielding");
            pairs.WriteLine("elapsed,first,second,classification,overlapFraction,continuousRawSeconds,continuousSameDirectionSeconds,bothNearHouse,continuousNearHouseSameDirectionSeconds,continuousStationarySeconds");
            snapshots.WriteLine("elapsed,active,houseQueue,houseEmergences,returning,bodyNear,longPresence,persistentCohort,houseResidents,dispersing,directionReversals,outwardViolations,firstDispersalMisses,firstDispersals,leftExits,rightExits,longSameDirectionOverlap,longCohortSeconds,longPresenceExcessSeconds,blockedSeconds,exitDecision");
            var metrics = new FairyHouseCrowdMetrics(fixture);
            float start = fixture.Clock;
            fixture.RequestHouseBurst(extra);
            metrics.Observe(0f);
            float maximumBodyWidth = 0f;
            string widestSeenId = "none";
            int[] captures = { 0, 3, 6, 9, 12, 20, 30, 60, 90 };
            int captureIndex = 0;
            report.AppendLine("\nScenario " + label + " | owned=" + UserInfo.GetGiveGachaItemCountDic().Count
                + " | floorWidth=" + F(fixture.Habitat.Settings.SafeGroundArea.width)
                + " | ground=" + fixture.Habitat.Settings.SafeGroundArea + " | entrance=" + fixture.Entrance
                + " | radius=" + F(fixture.House.Settings.SafeHouseCrowdingRadius)
                + " | calmSpeed=" + F(fixture.CalmSpeed));
            if (wide)
            {
                var ownedIds = UserInfo.GetGiveGachaItemCountDic();
                foreach (var item in fixture.Host.Catalog.Where(item => EnhancementFairyCatalog.IsEligible(item) && ownedIds.ContainsKey(item.Id))
                    .OrderByDescending(item => item.Sprite.bounds.size.x / Mathf.Max(.001f, item.Sprite.bounds.size.y)).Take(4))
                    report.AppendLine("Wide catalog item=" + item.Id + " | normalizedWidth="
                        + F(item.Sprite.bounds.size.x / Mathf.Max(.001f, item.Sprite.bounds.size.y)));
            }
            for (int frame = 0; frame <= 5400; frame++)
            {
                if (frame > 0) { fixture.Step(); metrics.Observe(1f / 30f); }
                float elapsed = fixture.Clock - start;
                foreach (var body in metrics.Bodies)
                {
                    if (body.Bounds.size.x > maximumBodyWidth) { maximumBodyWidth = body.Bounds.size.x; widestSeenId = body.Id; }
                    bodies.WriteLine(string.Join(",", F(elapsed), Csv(body.Id), F(body.Ground.x), F(body.Ground.y),
                        F(body.Bounds.min.x), F(body.Bounds.max.x), F(body.Bounds.min.y), F(body.Bounds.max.y),
                        Csv(body.HousePhase), body.Dispersing, F(body.DispersalTarget.x), F(body.DispersalTarget.y),
                        body.Eligible, body.BirthProtected, body.Replacing, body.NearHouse, body.Moving, F(body.StepX), F(body.DwellSeconds),
                        F(fixture.Habitat.GetHouseZoneSeconds(body.Id)), F(fixture.Habitat.GetHouseOverlapSeconds(body.Id)),
                        fixture.Habitat.GetHouseExitSide(body.Id), F(fixture.Habitat.GetSpacingHalfWidth(body.Id)),
                        F(body.MovementTarget.x), F(body.MovementTarget.y), F(body.IntendedDirection), body.DirectedWalking, body.SpacingYielding));
                }
                foreach (var pair in metrics.Pairs)
                    pairs.WriteLine(string.Join(",", F(elapsed), Csv(pair.First), Csv(pair.Second), Csv(pair.Classification),
                        F(pair.Ratio), F(pair.RawSeconds), F(pair.SameDirectionSeconds), pair.BothNearHouse,
                        F(pair.NearHouseSameDirectionSeconds), F(pair.StationarySeconds)));
                if (frame % 30 == 0 || frame == 5400)
                    snapshots.WriteLine(string.Join(",", F(elapsed), fixture.Habitat.ActiveCount, fixture.Habitat.PendingHouseSpawnCount,
                        fixture.Habitat.HouseEmergenceCount, fixture.Habitat.ReturningHouseCount, metrics.BodyNearCount,
                        metrics.LongPresenceCount, metrics.PersistentCohortSize, fixture.Habitat.HouseResidentCount,
                        fixture.Habitat.DispersingCount, metrics.ExitDirectionReversals, metrics.ExitOutwardViolations,
                        metrics.FirstDispersalDeadlineMisses, metrics.FirstDispersalCount, metrics.LeftExits, metrics.RightExits,
                        F(metrics.MaximumSameDirectionOverlapSeconds), F(metrics.MaximumLongCohortSeconds),
                        F(metrics.MaximumLongPresenceExcessSeconds), F(fixture.Habitat.HouseExitBlockedSeconds), Csv(fixture.Habitat.LastHouseExitDecision)));
                if (captureIndex < captures.Length && elapsed + .02f >= captures[captureIndex])
                {
                    Capture(fixture.Host, fixture.Area, prefix + "-" + captures[captureIndex] + "s", report);
                    captureIndex++;
                }
                if (frame == 900)
                    report.AppendLine("30-second residual: originalVisitorsNotFullyClear=" + (metrics.InitialVisitorCount - metrics.InitialVisitorsCleared)
                        + " | actualBodiesNearHouse=" + metrics.BodyNearCount + " | " + metrics.Describe());
            }
            report.AppendLine(metrics.Describe());
            report.AppendLine("Actual admitted=" + fixture.Habitat.HouseEmergenceCount + "/" + extra
                + " | actualPeak=" + metrics.PeakActive + "/" + (12 + extra)
                + " | finalActive=" + fixture.Habitat.ActiveCount + " | finalQueue=" + fixture.Habitat.PendingHouseSpawnCount
                + " | maxDrawnWidth=" + F(maximumBodyWidth) + " | widestSeenId=" + widestSeenId);
            bool admissions = metrics.PeakActive == 12 + extra && fixture.Habitat.HouseEmergenceCount == extra
                && metrics.FirstDispersalCount == extra;
            bool crowd = metrics.ExitDirectionReversals == 0 && metrics.ExitOutwardViolations == 0
                && metrics.FirstDispersalDeadlineMisses == 0 && metrics.InitialVisitorsCleared == extra
                && metrics.MaximumInitialVisitorClearanceSeconds <= metrics.InitialClearanceBudgetSeconds
                && (extra < 6 || metrics.LeftExits > 0 && metrics.RightExits > 0);
            report.AppendLine("Observed admission criterion=" + admissions + " | observed finite physical progress criterion=" + crowd
                + " | This is evidence, not a replacement for actual GameView inspection.");
            if (extra > 0)
            {
                float settleStarted = fixture.Clock;
                int peakReturning = 0;
                using (var settling = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-settle.csv")))
                {
                    settling.WriteLine("elapsed,active,returning,pending,returnDueAt,nextReturnAt,returningIds");
                    for (int frame = 0; frame < 30000 && (fixture.Habitat.ActiveCount > fixture.Habitat.BaseActiveLimit
                        || fixture.Habitat.ReturningHouseCount > 0 || fixture.Habitat.PendingHouseSpawnCount > 0); frame++)
                    {
                        fixture.Step();
                        peakReturning = Mathf.Max(peakReturning, fixture.Habitat.ReturningHouseCount);
                        if (frame % 30 == 0)
                            settling.WriteLine(string.Join(",", F(fixture.Clock - start), fixture.Habitat.ActiveCount,
                                fixture.Habitat.ReturningHouseCount, fixture.Habitat.PendingHouseSpawnCount,
                                F(fixture.Habitat.HouseReturnDueAt), F(fixture.Habitat.NextHouseReturnAt),
                                Csv(string.Join("|", fixture.Habitat.ReturningHouseItemIds))));
                    }
                    settling.WriteLine(string.Join(",", F(fixture.Clock - start), fixture.Habitat.ActiveCount,
                        fixture.Habitat.ReturningHouseCount, fixture.Habitat.PendingHouseSpawnCount,
                        F(fixture.Habitat.HouseReturnDueAt), F(fixture.Habitat.NextHouseReturnAt),
                        Csv(string.Join("|", fixture.Habitat.ReturningHouseItemIds))));
                }
                report.AppendLine("Additional bounded settle: seconds=" + F(fixture.Clock - settleStarted)
                    + " | endActive=" + fixture.Habitat.ActiveCount + " | endQueue=" + fixture.Habitat.PendingHouseSpawnCount
                    + " | peakConcurrentReturns=" + peakReturning + " | reachedBase="
                    + (fixture.Habitat.ActiveCount <= fixture.Habitat.BaseActiveLimit && fixture.Habitat.ReturningHouseCount == 0
                        && fixture.Habitat.PendingHouseSpawnCount == 0));
            }
        }
    }

    private static void RunObservation(string prefix)
    {
        RequireSafeEditor();
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Scope: real saved Stage1 + original Floor3.Start / Habitat / Brain / house and passive furniture spacing anchors.");
        report.AppendLine("Furniture activity AI, reservations, probability rolls, riding, sleeping and Zzz are retired. These measurements retain ordinary movement, house emergence/returns and physical crowd observations.");
        report.AppendLine("Stage1Host uses catalog-backed memory-only ownership and restores all adapters on disposal. No forced fairy position, account, transaction, persisted ownership or Scene save.");
        report.AppendLine("Three 300-second simulations request base12 / 18 / 24 residents; ordinary returns may reduce actual displayed counts.");
        report.AppendLine("Camera.Render snapshots are renderer-only EditMode composites, NOT actual Play/GameView, production materials/masks, input or mobile FPS verification.");
        foreach (int count in new[] { 12, 18, 24 }) ObserveScenario(count, report, prefix);
        File.WriteAllText(Path.Combine(OutputDirectory, prefix + "-summary.txt"), report.ToString());
    }

    private static void RequireSafeEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Fairy AI observation requires idle SDK-free Edit Mode outside Prefab Mode.");
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty)
                throw new InvalidOperationException("Fairy AI observation does not save or discard dirty user scenes.");
    }

    private static void ObserveScenario(int requestedCount, StringBuilder report, string prefix)
    {
        using (var fixture = new FairyAiRuntimeStageFixture(requestedCount == 12 ? 12 : 40))
        using (var snapshots = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-" + requestedCount + "-snapshots.csv")))
        using (var pairs = new StreamWriter(Path.Combine(OutputDirectory, prefix + "-" + requestedCount + "-pairs.csv")))
        {
            var metrics = new FairyHouseCrowdMetrics(fixture);
            fixture.RequestHouseBurst(requestedCount - 12);
            float start = fixture.Clock;
            snapshots.WriteLine("time,active,pooled,owned,houseQueue,returning,bodyNear,longPresence,cohortSize,dispersing,wholeRoomSameRouteOverlap,wholeRoomStationaryPairSeconds");
            pairs.WriteLine("time,first,second,classification,overlapFraction,continuousRawSeconds,continuousSameDirectionSeconds,bothNearHouse,continuousStationarySeconds");
            for (int frame = 0; frame <= 9000; frame++)
            {
                if (frame > 0) fixture.Step();
                metrics.Observe(frame == 0 ? 0f : 1f / 30f);
                float elapsed = fixture.Clock - start;
                foreach (var pair in metrics.Pairs)
                    pairs.WriteLine(string.Join(",", F(elapsed), Csv(pair.First), Csv(pair.Second), Csv(pair.Classification),
                        F(pair.Ratio), F(pair.RawSeconds), F(pair.SameDirectionSeconds), pair.BothNearHouse, F(pair.StationarySeconds)));
                if (frame % 30 == 0)
                    snapshots.WriteLine(string.Join(",", F(elapsed), fixture.Habitat.ActiveCount, fixture.Habitat.PooledObjectCount,
                        fixture.Habitat.OwnedTypeCount, fixture.Habitat.PendingHouseSpawnCount, fixture.Habitat.ReturningHouseCount,
                        metrics.BodyNearCount, metrics.LongPresenceCount, metrics.PersistentCohortSize, fixture.Habitat.DispersingCount,
                        F(metrics.WholeRoomMaximumSameRouteOverlapSeconds), F(metrics.WholeRoomMaximumStationaryPairSeconds)));
                if (frame == 0 || frame == 900 || frame == 1800 || frame == 5400 || frame == 9000)
                    Capture(fixture.Host, fixture.Area, prefix + "-" + requestedCount + "-" + (frame / 30) + "s-editmode", report);
            }
            report.AppendLine("requested=" + requestedCount + " | " + metrics.Describe()
                + " | finalActive=" + fixture.Habitat.ActiveCount + " | finalQueue=" + fixture.Habitat.PendingHouseSpawnCount);
        }
    }

    private static void Capture(EnhancementFairyStage1Host host, ThirdFloorFairyPlayArea area, string name, StringBuilder report)
    {
        Scene renderScene = default;
        RenderTexture target = null;
        var materials = new List<Material>();
        var previousTarget = RenderTexture.active;
        try
        {
            renderScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Dynamic fairy AI renderer-only evidence") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, renderScene);
            var region = new Bounds(new Vector3(host.Camera.transform.position.x, host.Camera.transform.position.y, 0f), new Vector3(55f, 40f, 60f));
            int sprites = 0;
            foreach (var original in host.SceneComponents<SpriteRenderer>())
            {
                if (!original.enabled || !original.gameObject.activeInHierarchy || original.sprite == null
                    || !original.bounds.Intersects(region) || original.GetComponentInParent<FloorLockGroup>() != null
                    || original.GetComponentInParent<FloorLocker>() != null) continue;
                var visual = new GameObject(original.name) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                visual.transform.SetParent(root.transform, false);
                visual.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                visual.transform.localScale = original.transform.lossyScale;
                var sprite = visual.AddComponent<SpriteRenderer>();
                sprite.sprite = original.sprite;
                sprite.color = original.color;
                sprite.flipX = original.flipX;
                sprite.flipY = original.flipY;
                sprite.drawMode = original.drawMode;
                sprite.size = original.size;
                sprite.sortingLayerID = original.sortingLayerID;
                sprite.sortingOrder = original.sortingOrder;
                var material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetTexture("_MainTex", original.sprite.texture);
                materials.Add(material);
                sprite.sharedMaterial = material;
                sprites++;
            }
            var cameraObject = new GameObject("1920x1080 dynamic state capture") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = renderScene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(renderScene);
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1920f / 1080f;
            camera.orthographicSize = host.Camera.orthographicSize; // Source already has production aspect correction.
            camera.transform.position = new Vector3(host.Camera.transform.position.x, host.Camera.transform.position.y, -30f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .12f, .12f);
            camera.cullingMask = 1 << 31;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                if (texture.GetPixels32().Where((pixel, index) => index % 31 == 0).Distinct().Take(16).Count() < 16)
                    throw new InvalidOperationException("Dynamic evidence camera produced an empty frame.");
                File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), texture.EncodeToPNG());
                report.AppendLine("Capture " + name + " | dynamic source frame copied to temporary unlit sprite renderers; NOT GameView"
                    + " | sprites=" + sprites + " | camera=" + camera.transform.position + " | ortho=" + F(camera.orthographicSize)
                    + " | resolution=1920x1080");
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            foreach (var material in materials) Object.DestroyImmediate(material);
            if (renderScene.IsValid()) EditorSceneManager.ClosePreviewScene(renderScene);
        }
    }

    private static string F(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
#endif
