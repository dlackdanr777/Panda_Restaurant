#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit local evidence requests. Uses the menu, shared view callbacks and memory service only.</summary>
[InitializeOnLoad]
public static class GachaCollectionReviewCapture
{
    private const string Output = "Logs/GachaReview20260917-2213";
    private const string Request = "Temp/GachaReview20260917-2213/review-request.txt";
    private const string Encoder = Output + "/tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe";
    private static readonly Queue<Step> Steps = new Queue<Step>();
    private sealed class Step { public Action Run; public double Delay; }
    private static double _next;
    private static string _command;
    private static GachaCollectionPreviewWindow _window;
    private static GachaCollectionPreviewSceneWitness _witness;
    private static JObject _report;
    static GachaCollectionReviewCapture() => EditorApplication.update += Tick;
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + .1;
        try
        {
            if (_command == null)
            {
                if (!File.Exists(Request)) return;
                string command = File.ReadAllText(Request).Trim(); File.Delete(Request); Begin(command); return;
            }
            if (Steps.Count == 0) { Finish(null); return; }
            var step = Steps.Dequeue(); step.Run();
            _witness.AssertUnchanged("review " + _command);
            _next = EditorApplication.timeSinceStartup + step.Delay;
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Add(Action action, double delay = 1.1) => Steps.Enqueue(new Step { Run = action, Delay = delay });
    private static void Begin(string command)
    {
        if (command != "ui" && command != "fairy-video" && command != "exchange-video") throw new ArgumentException("Unknown review capture command.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit-mode evidence only.");
        _command = command; _witness = GachaCollectionPreviewSceneWitness.Capture();
        _report = new JObject { ["command"] = command, ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["before"] = _witness.Summary };
        if (!EditorApplication.ExecuteMenuItem("Tools/Panda Restaurant/Gacha Collection/Open Integrated Offline Demo"))
            throw new InvalidOperationException("The existing menu did not execute.");
        _window = GachaCollectionPreviewWindow.ActiveWindow;
        _window.Focus(); _window.SetPreviewResolution(1920, 1080);
        if (command == "ui") BuildUi();
        else if (command == "fairy-video") BuildFairyVideo();
        else BuildExchangeVideo();
    }
    private static void BuildUi()
    {
        Add(() => { _window.Reset(0); _window.Draw(GachaMachineKind.Staff, null, true); }, 2.7);
        Add(() =>
        {
            var tx = _window.Economy.LastTransaction;
            Check(tx.Results.Count == 11 && tx.BaseDrawCount == 10 && tx.BonusDrawCount == 1, "10+1 result roles");
            Check(tx.Results[10].IsBonus && tx.Results[10].CounterBefore == 10 && tx.Results[10].CounterAfter == 10, "Bonus must not count");
            Check(_window.View.CollectionHud.CommittedCounter == 10, "Committed HUD must show 10");
            _report["zeroToTen"] = new JArray(tx.Results.Select((r, i) => new JObject { ["index"] = i + 1,
                ["id"] = r.Id, ["role"] = r.DrawRole.ToString(), ["before"] = r.CounterBefore, ["after"] = r.CounterAfter,
                ["guaranteed"] = r.IsGuaranteedSpecial, ["rank"] = r.Rank.ToString(), ["new"] = r.IsNew }));
            Capture("01-zero-plus-eleven-counter-ten");
        });
        Add(() => { _window.Reset(0); _window.OpenExchange(TokenExchangeTab.Staff); });
        Add(() => { CheckDisplay(GachaExchangeCategory.Staff); Capture("02-staff-1920x1080"); });
        Add(() => { _window.SetPreviewResolution(1280, 720); _window.position = new Rect(100, 100, 850, 590); _window.Repaint(); });
        Add(() => { CheckDisplay(GachaExchangeCategory.Staff); Capture("03-staff-1280x720"); });
        Add(() => { _report["smallWindow"] = "850x590; 1280x720 render target; fixed six slots checked";
            _window.position = new Rect(100, 100, 1100, 720); _window.SetPreviewResolution(1920, 1080);
            _window.View.CollectionExchange.SelectTab(TokenExchangeTab.Tickets); });
        Add(() => { Check(_window.Economy.GetDisplayedProducts(GachaExchangeCategory.Tickets).Count == 2, "Two fixed tickets"); Capture("04-tickets"); });
        Add(() => _window.View.CollectionExchange.SelectTab(TokenExchangeTab.Items));
        Add(() => { CheckDisplay(GachaExchangeCategory.Items); Capture("05-items"); });
        Add(() => _window.View.CollectionExchange.SelectTab(TokenExchangeTab.Staff));
        Add(() => { RecordRefresh("before", 3); Capture("06-refresh-three"); ClickRefresh(); });
        Add(() => { RecordRefresh("staffFirst", 2); Capture("07-refresh-two"); ClickRefresh(); });
        Add(() => { RecordRefresh("staffSecond", 1); Capture("08-refresh-one"); _window.View.CollectionExchange.SelectTab(TokenExchangeTab.Items); });
        Add(ClickRefresh);
        Add(() => { RecordRefresh("itemThird", 0); Capture("09-refresh-zero");
            Check(!RefreshButton().interactable, "Fourth refresh must be disabled"); });
        Add(() => { _window.View.CollectionExchange.SetVisible(false); _window.OpenExchange(TokenExchangeTab.Staff); });
        Add(() => { RecordRefresh("reopened", 0); Capture("10-reopen-preserved"); });
    }
    private static void CheckDisplay(GachaExchangeCategory category)
    {
        var products = _window.Economy.GetDisplayedProducts(category);
        Check(products.Count == 6 && products.Select(x => x.Id).Distinct().Count() == 6, "Six distinct retained products");
        Check(!_window.View.CollectionExchange.GetComponentsInChildren<ScrollRect>(true).Any(), "No scrolling product rows");
        Check(_window.View.CollectionExchange.GetComponentsInChildren<Button>(true).Count(x => x.name == "Close Exchange") == 1, "One close button");
        Check(!_window.View.CollectionExchange.IsEntering, "Entrance settled");
        if (category == GachaExchangeCategory.Staff) Check(products.All(x => GachaEconomyService.CanPurchaseStaff(x.Data.Rank)), "Allowed staff tiers only");
    }
    private static Button RefreshButton() => _window.View.CollectionExchange.GetComponentsInChildren<Button>(true).Single(x => x.name == "Refresh Display");
    private static void ClickRefresh()
    {
        Check(RefreshButton().interactable, "Refresh UI should be enabled");
        RefreshButton().onClick.Invoke();
    }
    private static void RecordRefresh(string name, int expected)
    {
        var service = _window.Economy;
        Check(service.GetRefreshStatus().Remaining == expected, "Expected shared remaining " + expected);
        if (_report["refresh"] == null) _report["refresh"] = new JObject();
        _report["refresh"][name] = new JObject { ["remaining"] = expected,
            ["staffVersion"] = service.GetExchangeDisplay(GachaExchangeCategory.Staff).Version,
            ["itemVersion"] = service.GetExchangeDisplay(GachaExchangeCategory.Items).Version,
            ["staffIds"] = new JArray(service.GetDisplayedProducts(GachaExchangeCategory.Staff).Select(x => x.Id)),
            ["itemIds"] = new JArray(service.GetDisplayedProducts(GachaExchangeCategory.Items).Select(x => x.Id)) };
    }
    private static void BuildFairyVideo()
    {
        Add(() => { _window.ShowFairies(); _window.Fairies.ResetOfflineSession(_window.Catalog.OfType<GachaItemData>()
            .Where(EnhancementFairyCatalog.IsEligible).OrderBy(x => x.Id, StringComparer.Ordinal).Take(12).Select(x => x.Id)); }, 2);
        Add(() => { Check(_window.Fairies.ActiveCount == 12, "Twelve active fairies"); _window.StartVideo(Encoder, Output + "/fairies-12-normal-speed.mp4", 12); }, 14);
        Add(() => { Check(!_window.IsRecordingVideo, "Video encoding should have finished"); Capture("11-fairies-12"); });
    }
    private static void BuildExchangeVideo()
    {
        Add(() => { _window.Reset(0); });
        Add(() => { _window.StartVideo(Encoder, Output + "/exchange-chain-entrance.mp4", 8); _window.OpenExchange(TokenExchangeTab.Staff); }, 2);
        Add(() => _window.View.CollectionExchange.SetVisible(false), .7);
        Add(() => _window.OpenExchange(TokenExchangeTab.Items), 2);
        Add(() => { _window.View.CollectionExchange.SetVisible(false); _window.OpenExchange(TokenExchangeTab.Staff); }, 5);
        Add(() => Check(!_window.IsRecordingVideo, "Entrance recording should finish"));
    }
    private static void Capture(string name) => _window.RenderToPng(Output + "/" + name + ".png");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(Exception error)
    {
        if (_report == null) _report = new JObject();
        try { _witness?.AssertUnchanged("review completion"); }
        catch (Exception preservation) { error = error == null ? preservation : new AggregateException(error, preservation); }
        _report["success"] = error == null; _report["finishedUtc"] = DateTime.UtcNow.ToString("O");
        _report["after"] = GachaCollectionPreviewSceneWitness.Capture().Summary; _report["error"] = error?.ToString();
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/review-" + (_command ?? "request") + ".json", _report.ToString());
        if (error != null) Debug.LogError("COLLECTION_REVIEW_FAILED: " + error);
        else Debug.Log("COLLECTION_REVIEW_COMPLETED: " + _command);
        Steps.Clear(); _command = null;
    }
}
#endif
