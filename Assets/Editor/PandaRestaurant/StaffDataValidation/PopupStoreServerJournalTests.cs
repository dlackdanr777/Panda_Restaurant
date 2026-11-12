#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Muks.BackEnd;
using NUnit.Framework;

public sealed class PopupStoreServerJournalTests
{
    private string _root;
    private PopupStoreServerFileJournal _journal;
    private static PopupStorePendingServerRequest Request(string id = "stable-start")
        => new PopupStorePendingServerRequest(id, "{\"RequestId\":\"" + id + "\",\"Operation\":1}");
    [SetUp] public void SetUp()
    {
        _root = Path.GetFullPath(Path.Combine("Temp", "PopupStoreServerJournalTests", Guid.NewGuid().ToString("N")));
        _journal = new PopupStoreServerFileJournal(_root);
    }
    [TearDown] public void TearDown()
    {
        string allowed = Path.GetFullPath(Path.Combine("Temp", "PopupStoreServerJournalTests")) + Path.DirectorySeparatorChar;
        if (_root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_root)) Directory.Delete(_root, true);
    }
    [Test] public void EmptyReadDoesNotCreateDirectory()
    { Assert.IsNull(_journal.Read("account-a")); Assert.IsFalse(Directory.Exists(_root)); }
    [Test] public void PendingIdentitySurvivesNewJournalInstance()
    { _journal.Prepare("account-a", Request()); Assert.IsTrue(new PopupStoreServerFileJournal(_root).Read("account-a").Matches(Request())); }
    [Test] public void SameCommandPrepareIsIdempotent()
    { Assert.IsTrue(_journal.Prepare("account-a", Request())); Assert.IsFalse(_journal.Prepare("account-a", Request())); Assert.AreEqual(1, Directory.GetFiles(_root, "*.request").Length); }
    [Test] public void NewInstanceCannotClaimExistingPendingAsNewlyCreated()
    { Assert.IsTrue(_journal.Prepare("account-a", Request())); var other = new PopupStoreServerFileJournal(_root); Assert.IsFalse(other.Prepare("account-a", Request())); Assert.IsTrue(other.Read("account-a").Matches(Request())); }
    [Test] public void DifferentRequestCannotReplaceUnresolvedStart()
    { _journal.Prepare("account-a", Request()); Assert.Throws<InvalidOperationException>(() => _journal.Prepare("account-a", Request("new"))); Assert.IsTrue(_journal.Read("account-a").Matches(Request())); }
    [Test] public void SameIdDifferentPayloadCannotReplaceOriginal()
    { _journal.Prepare("account-a", Request()); Assert.Throws<InvalidOperationException>(() => _journal.Prepare("account-a", new PopupStorePendingServerRequest("stable-start", "{}"))); }
    [Test] public void DifferentAccountHasIndependentPartition()
    { _journal.Prepare("account-a", Request()); Assert.IsNull(_journal.Read("account-b")); _journal.Prepare("account-b", Request("b")); Assert.AreEqual("b", _journal.Read("account-b").RequestId); }
    [Test] public void AccountNamesDoNotBecomePaths()
    { _journal.Prepare("../../account-a", Request()); Assert.AreEqual(1, Directory.GetFiles(_root, "*.request").Length); Assert.IsFalse(Directory.GetFiles(_root).Any(p => Path.GetFileName(p).Contains("account-a"))); }
    [Test] public void ExactCompletionClearsOnlyItsAccount()
    { _journal.Prepare("account-a", Request()); _journal.Prepare("account-b", Request()); _journal.Complete("account-a", Request()); Assert.IsNull(_journal.Read("account-a")); Assert.IsNotNull(_journal.Read("account-b")); }
    [Test] public void CompletionIsIdempotent()
    { _journal.Prepare("account-a", Request()); _journal.Complete("account-a", Request()); _journal.Complete("account-a", Request()); Assert.IsNull(_journal.Read("account-a")); }
    [Test] public void WrongCompletionCannotDropPendingRequest()
    { _journal.Prepare("account-a", Request()); Assert.Throws<InvalidOperationException>(() => _journal.Complete("account-a", Request("other"))); Assert.IsNotNull(_journal.Read("account-a")); }
    [Test] public void CorruptChecksumFailsClosed()
    { _journal.Prepare("account-a", Request()); string path = Directory.GetFiles(_root, "*.request")[0]; var bytes = File.ReadAllBytes(path); bytes[40] ^= 1; File.WriteAllBytes(path, bytes); Assert.Throws<InvalidDataException>(() => _journal.Read("account-a")); Assert.Throws<InvalidDataException>(() => _journal.Prepare("account-a", Request("new"))); }
    [Test] public void TruncatedJournalFailsClosed()
    { _journal.Prepare("account-a", Request()); File.WriteAllBytes(Directory.GetFiles(_root, "*.request")[0], new byte[12]); Assert.Throws<InvalidDataException>(() => _journal.Read("account-a")); }
    [Test] public void MovingJournalToAnotherAccountDoesNotAuthorizeIt()
    { _journal.Prepare("account-a", Request()); _journal.Prepare("account-b", Request("b")); var paths = Directory.GetFiles(_root, "*.request"); File.Copy(paths[0], paths[1], true); Assert.Throws<InvalidDataException>(() => { _journal.Read("account-a"); _journal.Read("account-b"); }); }
    [Test] public void ConcurrentFileOwnerStopsPrepareBeforeSending()
    { _journal.Prepare("account-a", Request()); var guard = Directory.GetFiles(_root, "*.guard")[0]; using (new FileStream(guard, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.Throws<IOException>(() => _journal.Read("account-a")); }
    [TestCase(null)] [TestCase("")] [TestCase(" ")]
    public void MissingAccountRejected(string account) { Assert.Throws<ArgumentException>(() => _journal.Read(account)); }
    [Test] public void OversizedCommandRejected()
    { Assert.Throws<ArgumentException>(() => new PopupStorePendingServerRequest("a", new string('x', 32769))); }
    [Test] public void NoTemporaryPayloadRemainsAfterCommit()
    { _journal.Prepare("account-a", Request()); Assert.IsEmpty(Directory.GetFiles(_root, "*.pending")); }
}
#endif
