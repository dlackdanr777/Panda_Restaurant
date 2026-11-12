#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class PopupStoreBackendFunctionTransportTests
{
    private const string Protocol = PopupStoreFunctionDevelopmentConfiguration.SupportedProtocol;
    private const string RequestId = "offline-request";
    private static string Command => new JObject { ["Protocol"] = Protocol, ["RequestId"] = RequestId, ["Operation"] = 0 }.ToString();
    private static string Reply(string protocol = Protocol, string requestId = RequestId)
        => new JObject { ["result"] = new JObject { ["Protocol"] = protocol, ["RequestId"] = requestId, ["Status"] = 0 } }.ToString();

    [Test]
    public void DefaultAndIncompleteConfigurationCannotCallSdk()
    {
        PopupStoreFunctionTransportResult result = null;
        new PopupStoreBackendFunctionTransport().Send(Command, value => result = value);
        Assert.That(result.Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.Unavailable));
        var fixture = new Fixture(new PopupStoreFunctionDevelopmentConfiguration(true, "development", "dev_offline"));
        fixture.Transport.Send(Command, fixture.Results.Add);
        Assert.That(fixture.Calls, Is.Zero);
        Assert.That(fixture.Results[0].Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.Unavailable));
    }

    [Test]
    public void ActualParamContainsObjectAndCallbacksAreDispatchedOnce()
    {
        var fixture = new Fixture();
        fixture.Transport.Send(Command, fixture.Results.Add);
        var serialized = JObject.Parse(fixture.Body.GetJson());
        Assert.That(serialized["request"].Type, Is.EqualTo(JTokenType.Object));
        Assert.That((string)serialized["request"]["RequestId"], Is.EqualTo(RequestId));
        fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, Reply()));
        fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, Reply()));
        Assert.That(fixture.Results, Is.Empty);
        fixture.Drain();
        Assert.That(fixture.Results.Count, Is.EqualTo(1));
        Assert.That(fixture.Results[0].Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.ResponseReceived));
        Assert.That(JObject.Parse(fixture.Results[0].ResponseJson)["result"], Is.Null);
        Assert.That(fixture.Timeout.IsDisposed, Is.True);
    }

    [Test]
    public void TimeoutDoesNotRetryAndLateSuccessCannotCompleteAgain()
    {
        var fixture = new Fixture();
        fixture.Transport.Send(Command, fixture.Results.Add);
        fixture.Timeout.Fire();
        fixture.Drain();
        fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, Reply()));
        fixture.Drain();
        Assert.That(fixture.Calls, Is.EqualTo(1));
        Assert.That(fixture.Results.Count, Is.EqualTo(1));
        Assert.That(fixture.Results[0].Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.Indeterminate));
        Assert.That(fixture.Results[0].ErrorCode, Is.EqualTo("timeout"));
    }

    [TestCase(false, "{}")]
    [TestCase(true, "not-json")]
    [TestCase(true, "{\"result\":{\"Protocol\":\"wrong\",\"RequestId\":\"offline-request\"}}")]
    [TestCase(true, "{\"result\":{\"Protocol\":\"popup-store.dev-server-01.v1\",\"RequestId\":\"other\"}}")]
    public void EveryPostSendFailureIsIndeterminate(bool success, string json)
    {
        var fixture = new Fixture();
        fixture.Transport.Send(Command, fixture.Results.Add);
        fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(success, json));
        fixture.Drain();
        Assert.That(fixture.Results[0].Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.Indeterminate));
        Assert.That(fixture.Results[0].ResponseJson, Is.Null);
    }

    [Test]
    public void SynchronousInvokeExceptionIsRedactedAndAmbiguous()
    {
        var fixture = new Fixture { ThrowOnInvoke = true };
        fixture.Transport.Send(Command, fixture.Results.Add);
        fixture.Drain();
        Assert.That(fixture.Results[0].Outcome, Is.EqualTo(PopupStoreFunctionTransportOutcome.Indeterminate));
        Assert.That(fixture.Results[0].ErrorCode, Is.EqualTo("invoke_outcome_unknown"));
        Assert.That(fixture.Results[0].ResponseJson, Is.Null);
    }

    [Test]
    public void AuthEpochChangeSuppressesQueuedResponseEvenForSameAccount()
    {
        var fixture = new Fixture();
        fixture.Transport.Send(Command, fixture.Results.Add);
        fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, Reply()));
        fixture.CurrentEpoch = new object();
        fixture.Drain();
        Assert.That(fixture.Results, Is.Empty);
        Assert.That(fixture.Timeout.IsDisposed, Is.True);
    }

    [Test]
    public void WorkerReplyNeverCallsConsumerUntilOwningThreadDrains()
    {
        var fixture = new Fixture();
        int callbackThread = -1;
        fixture.Transport.Send(Command, value => callbackThread = Thread.CurrentThread.ManagedThreadId);
        var worker = new Thread(() => fixture.Callback(new PopupStoreBackendFunctionTransport.NativeResponse(true, Reply())));
        worker.Start(); worker.Join();
        Assert.That(callbackThread, Is.EqualTo(-1));
        fixture.Drain();
        Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
    }

    [Test]
    public void InvalidRequestOrStaleSessionIsRejectedBeforeInvoke()
    {
        var fixture = new Fixture();
        fixture.Transport.Send("{\"Protocol\":\"wrong\",\"RequestId\":\"id\"}", fixture.Results.Add);
        fixture.CurrentEpoch = new object();
        fixture.Transport.Send(Command, fixture.Results.Add);
        Assert.That(fixture.Calls, Is.Zero);
        Assert.That(fixture.Results.TrueForAll(x => x.Outcome == PopupStoreFunctionTransportOutcome.RejectedBeforeSend), Is.True);
    }

    private sealed class Fixture
    {
        internal readonly PopupStoreBackendFunctionTransport Transport;
        internal readonly List<PopupStoreFunctionTransportResult> Results = new List<PopupStoreFunctionTransportResult>();
        internal readonly Queue<Action> Dispatch = new Queue<Action>();
        internal readonly FakeTimeout Timeout = new FakeTimeout();
        internal object CurrentEpoch = new object();
        internal int Calls;
        internal Param Body;
        internal bool ThrowOnInvoke;
        internal Action<PopupStoreBackendFunctionTransport.NativeResponse> Callback;
        internal Fixture(PopupStoreFunctionDevelopmentConfiguration configuration = null)
        {
            object capturedEpoch = CurrentEpoch;
            Transport = new PopupStoreBackendFunctionTransport(configuration
                ?? new PopupStoreFunctionDevelopmentConfiguration(true, "development", "dev_offline", Protocol, true, true, true),
                () => ReferenceEquals(capturedEpoch, CurrentEpoch), () => true,
                action => { lock (Dispatch) Dispatch.Enqueue(action); },
                (function, body, callback) =>
                {
                    ++Calls; Body = body; Callback = callback;
                    if (ThrowOnInvoke) throw new InvalidOperationException("sensitive-body-must-not-escape");
                },
                (callback, delay) => { Timeout.Callback = callback; return Timeout; });
        }
        internal void Drain()
        {
            while (true)
            {
                Action action;
                lock (Dispatch) { if (Dispatch.Count == 0) return; action = Dispatch.Dequeue(); }
                action();
            }
        }
    }
    private sealed class FakeTimeout : IDisposable
    {
        internal Action Callback;
        internal bool IsDisposed;
        internal void Fire() { if (!IsDisposed) Callback(); }
        public void Dispose() { IsDisposed = true; }
    }
}
#endif
