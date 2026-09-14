#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using LitJson;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// These are real operation/Backend lease/Stage entry calls with only UPost and unrelated rewards replaced by memory fakes.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void MailReceive_ManifestValidationIsWholeAndIndependentOfMutableDisplayItems()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateAccountRuntimeFixture(out _);
            var query = fixture.Manager.RestoredGameData.Query;
            string raw = new JArray(
                MailRewardJson("STAFF02", 1),
                new JObject { ["item"] = new JObject { ["itemName"] = "Gold" }, ["itemCount"] = 3 },
                new JObject { ["item"] = new JObject { ["itemName"] = "{\"itemID\":\"Dia\",\"chartFileName\":\"memory\"}" }, ["itemCount"] = 2 }
            ).ToString(Formatting.None);
            Assert.That(MailRewardParser.TryParse(raw, out var rewards, out var error), Is.True, error);
            CollectionAssert.AreEqual(new[] { "STAFF02", "Gold", "Dia" }, rewards.Select(item => item.Id));
            CollectionAssert.AreEqual(new[] { 1, 3, 2 }, rewards.Select(item => item.Count));
            string mailJson = new JObject { ["inDate"] = "manifest-mail", ["title"] = "Memory test", ["items"] = JArray.Parse(raw) }.ToString(Formatting.None);
            var mail = new MailData(PostType.Admin, JsonMapper.ToObject(mailJson), query);
            Assert.That(mail.HasValidRewardManifest, Is.True, mail.RewardManifestError);
            string frozen = mail.RawRewardManifestJson;
            mail.Items[0].ItemName = "Gold"; mail.Items[0].ItemCount = 999;
            mail.Items.Clear();
            Assert.That(mail.RewardManifest[0].Id, Is.EqualTo("STAFF02"));
            Assert.That(mail.RewardManifest[0].Count, Is.EqualTo(1));
            Assert.That(mail.RawRewardManifestJson, Is.EqualTo(frozen));
            Assert.That(mail.SourceQuery, Is.SameAs(query));
            foreach (string bad in new[]
            {
                null, "", "{}", "{broken", "null",
                "[{\"item\":{\"itemID\":\"Gold\"},\"itemCount\":-1}]",
                "[{\"item\":{\"itemID\":\"Gold\"},\"itemCount\":0}]",
                "[{\"item\":{\"itemID\":\"Gold\"},\"itemCount\":1.5}]",
                "[{\"item\":{\"itemID\":\"Gold\"},\"itemCount\":2147483648}]",
                "[{\"item\":{\"itemID\":\"Gold\"},\"itemCount\":1},{\"item\":{\"itemID\":\"\"},\"itemCount\":1}]"
            })
            {
                Assert.That(MailRewardParser.TryParse(bad, out var partial, out error), Is.False, bad);
                Assert.That(partial, Is.Null, "Invalid manifests must not expose an applicable prefix");
                Assert.That(error, Is.Not.Null.And.Not.Empty);
            }
            Assert.That(MailRewardParser.TryParse("[]", out var empty, out error), Is.True, error);
            Assert.That(empty, Is.Empty);
            Assert.That(MailRewardParser.TryParseResponse("{\"postItems\":[]}", out empty, out error), Is.True, error);
            Assert.That(empty, Is.Empty);
            Assert.That(fixture.Game.Writes, Is.Zero);
        }
    }

    [Test]
    public void MailReceive_PreflightRejectsUnreadySourcesAndEveryRewardBeforeAnyPublicCall()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string reason in new[] { "null-list", "null-target", "source-missing", "unknown-item", "invalidated", "sending-save", "indeterminate-save" })
            {
                var mail = CreateMailCase();
                var target = MailTarget(mail, "preflight", "STAFF02");
                IReadOnlyList<MailReceiveTarget> targets = new[] { target };
                if (reason == "null-list") targets = null;
                if (reason == "null-target") targets = new MailReceiveTarget[] { null };
                if (reason == "source-missing") targets = new[] { new MailReceiveTarget(PostType.Admin, "missing-source", new[] { new MailRewardItem("STAFF02", 1) }, null) };
                if (reason == "unknown-item") targets = new[] { target, MailTarget(mail, "unknown", "UNREGISTERED_MEMORY_REWARD") };
                if (reason == "invalidated") mail.Fixture.Manager.InvalidateGameDataRestore();
                if (reason.EndsWith("save", StringComparison.Ordinal))
                {
                    mail.Fixture.Manager.RequestGameDataAutosave();
                    if (reason == "indeterminate-save")
                    {
                        var coordinator = mail.Fixture.Manager.CurrentGameDataSaveCoordinator;
                        coordinator.MarkResponseMissing(coordinator.CurrentIdentity, "Memory-only missing save response");
                    }
                }
                Assert.That(mail.Operation.TryStart(targets, out _), Is.False, reason);
                Assert.That(mail.Transport.Calls, Is.Empty, reason);
                Assert.That(mail.Operation.PublicCallCount, Is.Zero, reason);
                Assert.That(mail.Environment.ApplyCalls, Is.Zero, reason);
                Assert.That(mail.MemoryNotifications, Is.Zero, reason);
                Assert.That(mail.Fixture.Stage.Runtime[EStage.Stage1].SaveData().GiveStaffList, Is.Empty, reason);
            }
            var refused = CreateMailCase();
            refused.Environment.RefuseValidation = true;
            Assert.That(refused.Operation.TryStart(new[] { MailTarget(refused, "refused", "STAFF02") }, out _), Is.False);
            Assert.That(refused.Operation.GetKnownEntry(PostType.Admin, "refused", refused.Fixture.Account), Is.Null);
            Assert.That(refused.Transport.Calls, Is.Empty);
            var oldSource = CreateMailCase();
            RestoreGameData(oldSource.Fixture, true); // Same account and row, genuinely newer restore query.
            Assert.That(oldSource.Fixture.Manager.CurrentMailReceiveQuery, Is.Not.SameAs(oldSource.Query));
            Assert.That(oldSource.Operation.TryStart(new[] { MailTarget(oldSource, "old-source", "STAFF02") }, out _), Is.False);
            Assert.That(oldSource.Transport.Calls, Is.Empty);
            oldSource.Query = oldSource.Fixture.Manager.CurrentMailReceiveQuery;
            Assert.That(oldSource.Operation.TryStart(new[] { MailTarget(oldSource, "new-source", "STAFF02") }, out var oldError), Is.True, oldError);
            Assert.That(oldSource.Transport.Calls.Count, Is.EqualTo(1), "Rejecting an unsent stale source must release only its own unused reservation");
            var reentrantSave = CreateMailCase();
            GameDataSaveRequest queued = null;
            reentrantSave.Environment.OnValidate = _ =>
            {
                if (queued == null) queued = reentrantSave.Fixture.Manager.RequestGameDataAutosave();
            };
            Assert.That(reentrantSave.Operation.TryStart(new[] { MailTarget(reentrantSave, "preflight-save", "STAFF02") }, out var queueError), Is.True, queueError);
            Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted));
            Assert.That(reentrantSave.Fixture.Game.Writes, Is.Zero);
            Assert.That(reentrantSave.Fixture.Game.LatestCalls, Is.Zero);
            reentrantSave.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
            Assert.That(reentrantSave.Fixture.Game.Writes, Is.EqualTo(1));
            AssertSavedAccount(reentrantSave.Fixture.Game.WriteValues.Single(), 2, true, 55);
            var changedDuringValidation = CreateMailCase();
            changedDuringValidation.Environment.OnValidate = _ => changedDuringValidation.Fixture.Manager.InvalidateGameDataRestore();
            Assert.That(changedDuringValidation.Operation.TryStart(new[] { MailTarget(changedDuringValidation, "changed-preflight", "STAFF02") }, out _), Is.False);
            Assert.That(changedDuringValidation.Transport.Calls, Is.Empty);
            Assert.That(changedDuringValidation.Environment.ApplyCalls, Is.Zero);
            Assert.That(changedDuringValidation.Fixture.Game.Writes, Is.Zero);

            // Exercise the native adapter's evidence check with real detached Stage applications, not a fake ready flag.
            var evidenceMethod = typeof(MailManager).GetMethod("HasStageApplicationEvidence",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(evidenceMethod, Is.Not.Null);
            var ownerProperty = typeof(StaffStageRuntimeSnapshot).GetProperty("OwnerToken",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(ownerProperty, Is.Not.Null);
            foreach (bool common in new[] { false, true })
            {
                var stages = CreateRuntimeFixture(common);
                stages.Manager.LoadAllStageData(true);
                var source = stages.Manager.StageMigrationCollection;
                var stageInfo = stages.Stage.Runtime[EStage.Stage1];
                object owner = ownerProperty.GetValue(stageInfo.CaptureStaffRuntimeSnapshot());
                Func<StaffStageApplicationResult, StaffStageMigrationCollection, StaffStageRuntimeSnapshot, bool> hasEvidence =
                    (application, current, now) => (bool)evidenceMethod.Invoke(null,
                        new object[] { source, application, current, source.Query, EStage.Stage1, owner, now });
                var pendingApplication = source.Applications.Single(item => item.Stage == EStage.Stage1);
                Assert.That(hasEvidence(pendingApplication, source, stageInfo.CaptureStaffRuntimeSnapshot()), Is.False);
                bool? duringApplication = null;
                stages.Stage.BeforeApply = stage =>
                {
                    if (stage == EStage.Stage1)
                        duringApplication = hasEvidence(source.Applications.Single(item => item.Stage == stage),
                            source, stageInfo.CaptureStaffRuntimeSnapshot());
                };
                ReplyRuntimeRows(stages);
                Assert.That(duringApplication, Is.False, "A received row is not evidence of completed Stage application");
                var applied = source.Applications.Single(item => item.Stage == EStage.Stage1);
                Assert.That(applied.Status, Is.EqualTo(StaffStageApplicationStatus.Succeeded));
                Assert.That(hasEvidence(applied, source, stageInfo.CaptureStaffRuntimeSnapshot()), Is.True);
                Assert.That(hasEvidence(pendingApplication, source, stageInfo.CaptureStaffRuntimeSnapshot()), Is.False,
                    "The protection must retain the exact successful application, not replace an earlier pending capture");
                Assert.That(hasEvidence(applied, source, new StageInfo().CaptureStaffRuntimeSnapshot()), Is.False,
                    "A different detached Stage object cannot reuse the original owner's proof");
                if (common) Assert.That(source.Status, Is.EqualTo(StaffStageMigrationCollectionStatus.NotRequired),
                    "Existing common staff data does not remove the furniture/kitchen Stage application requirement");
                stages.Manager.LoadAllStageData(true);
                var newer = stages.Manager.StageMigrationCollection;
                Assert.That(newer.Query, Is.SameAs(source.Query));
                Assert.That(hasEvidence(applied, newer, stageInfo.CaptureStaffRuntimeSnapshot()), Is.False,
                    "A new Stage round under the same GameData query must not reuse previous application evidence");
                Assert.That(stages.Game.Writes, Is.Zero);
            }
        }
    }

    [Test]
    public void MailReceive_SingleCompletionIsMemoryOnlyOnceAndQueuedSaveContainsTheGrantedStaff()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var mail = CreateMailCase();
            int saveCompletions = 0;
            GameDataSaveRequest save = null;
            mail.OnApplied = entry => save = mail.Fixture.Manager.RequestGameDataAutosave(response => saveCompletions++);
            var target = MailTarget(mail, "single", "STAFF02");
            Assert.That(mail.Operation.TryStart(new[] { target }, out var error), Is.True, error);
            Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.Receiving));
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1));
            Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.Staff.Any(staff => staff.Id == "STAFF02"), Is.False);
            Assert.That(mail.Operation.TryStart(new[] { target }, out _), Is.False);
            Assert.That(mail.Operation.TryStart(new[] { MailTarget(mail, "different", "STAFF03") }, out _), Is.False);
            var response = MailSuccess("STAFF02", 1);
            mail.Transport.Calls[0].Reply(response);
            Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            Assert.That(mail.Operation.Entries.Single().Status, Is.EqualTo(MailReceiveEntryStatus.AppliedInMemory));
            Assert.That(mail.Operation.Entries.Single().RawResponse, Is.EqualTo(response.RawJson));
            Assert.That(mail.Operation.Entries.Single().Progress.Single().Result.AppliedCount, Is.EqualTo(1));
            Assert.That(mail.Operation.Entries.Single().Progress.Single().Result.IsComplete, Is.True);
            Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
            Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(1));
            AssertAccountLevels(mail.Fixture, "STAFF02", 1);
            Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(save, Is.Not.Null);
            Assert.That(save.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
            Assert.That(saveCompletions, Is.Zero, "MemoryApplied must not masquerade as persistence success");
            Assert.That(mail.Fixture.Game.Writes, Is.EqualTo(1));
            AssertSavedAccount(mail.Fixture.Game.WriteValues[0], 2, true, 55);
            mail.Transport.Calls[0].Reply(response);
            mail.Transport.Calls[0].Reply(new MailReceiveResponse(false, null, "Duplicate conflicting response"));
            Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
            Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(1));
            Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(1));
            Assert.That(mail.Fixture.Game.Writes, Is.EqualTo(1));
            mail.Fixture.Game.WriteReplies.Single()(Bro("204", ""));
            Assert.That(saveCompletions, Is.EqualTo(1));
            Assert.That(mail.Operation.GetKnownEntry(PostType.Admin, "single", mail.Fixture.Account), Is.SameAs(mail.Operation.Entries.Single()));
            Assert.That(mail.Operation.TryStart(new[] { target }, out _), Is.False, "Already consumed mail must not be sent again");
        }
    }

    [Test]
    public void MailReceive_AdminAndCouponBatchIsSequentialFrozenAndDoesNotSaveAnIntermediatePrefix()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var mail = CreateMailCase();
            var targets = new List<MailReceiveTarget>
            { MailTarget(mail, "admin", "STAFF02"), MailTarget(mail, "coupon", "STAFF02", PostType.Coupon) };
            mail.OnApplied = _ => mail.Fixture.Manager.RequestGameDataAutosave();
            Assert.That(mail.Operation.TryStart(targets, out var error), Is.True, error);
            targets.Clear(); targets.Add(MailTarget(mail, "arrived-later", "STAFF03"));
            Assert.That(mail.Operation.Entries.Count, Is.EqualTo(2));
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1));
            var first = mail.Transport.Calls[0];
            first.Reply(MailSuccess("STAFF02", 1));
            Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(2));
            Assert.That(mail.Fixture.Game.Writes, Is.Zero, "Ordinary autosaves must wait while the batch lease protects a partial prefix");
            first.Reply(MailSuccess("STAFF02", 1));
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(2));
            Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
            mail.Transport.Calls[1].Reply(MailSuccess("STAFF02", 1));
            Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            Assert.That(mail.MemoryNotifications, Is.EqualTo(2));
            Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(2));
            Assert.That(mail.Operation.Entries.All(entry => entry.Status == MailReceiveEntryStatus.AppliedInMemory), Is.True);
            Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(2));
            Assert.That(mail.Fixture.Game.Writes, Is.EqualTo(1), "Pending autosaves may coalesce only after the complete memory batch");
            AssertSavedAccount(mail.Fixture.Game.WriteValues[0], 2, true, 55);
            Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.Staff.Count(staff => staff.Id == "STAFF02"), Is.EqualTo(1));
            Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(mail.Operation.GetKnownEntry(PostType.Admin, "arrived-later", mail.Fixture.Account), Is.Null);
        }
    }

    [Test]
    public void MailReceive_UnknownOrLostResponsesNeverRetryApplyOrUnlockForAnotherOperation()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string mode in new[] { "null", "failure", "throw", "missing" })
            {
                var mail = CreateMailCase();
                if (mode == "throw") mail.Transport.OnSend = _ => throw new InvalidOperationException("Memory UPost boundary threw");
                mail.Operation.TryStart(new[] { MailTarget(mail, "uncertain", "STAFF02") }, out _);
                if (mode == "null") mail.Transport.Calls[0].Reply(null);
                if (mode == "failure") mail.Transport.Calls[0].Reply(new MailReceiveResponse(false, "{\"error\":\"unverified\"}", "Timeout/failure is not proof of non-consumption"));
                if (mode == "missing") Assert.That(mail.Operation.MarkResponseMissing("Memory-only response loss"), Is.True);
                Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.Indeterminate), mode);
                Assert.That(mail.Operation.Entries.Single().Status, Is.EqualTo(MailReceiveEntryStatus.Indeterminate), mode);
                Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(1), mode);
                Assert.That(mail.Environment.ApplyCalls, Is.Zero, mode);
                Assert.That(mail.MemoryNotifications, Is.Zero, mode);
                Assert.That(mail.Fixture.Game.Writes, Is.Zero, mode);
                Assert.That(mail.Operation.TryStart(new[] { MailTarget(mail, "other", "STAFF02") }, out _), Is.False, mode);
                var replacement = new MailReceiveOperation(mail.Environment, new MemoryMailTransport());
                Assert.That(replacement.TryStart(new[] { MailTarget(mail, "other-controller", "STAFF02") }, out _), Is.False, mode);
                var queued = mail.Fixture.Manager.RequestGameDataAutosave();
                Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted), mode);
                Assert.That(mail.Fixture.Game.Writes, Is.Zero, "Unlike an uncertain GameData send, a mail lease retains ordinary saves without transmitting them");
                Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1), mode);
                if (mode == "missing")
                {
                    mail.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
                    Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
                    Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(1), "A late matching success is not another consume call");
                    Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(1));
                    Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
                    Assert.That(mail.Fixture.Game.Writes, Is.EqualTo(1));
                    AssertSavedAccount(mail.Fixture.Game.WriteValues.Single(), 2, true, 55);
                    Assert.That(queued.Status, Is.EqualTo(GameDataSaveRequestStatus.Sending));
                }
            }
        }
    }

    [Test]
    public void MailReceive_ConsumedButUnappliedOrPartialRewardsRemainExplicitAndNeverRepeatTheAppliedPrefix()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string mode in new[] { "rejected", "partial", "unknown", "throw", "mismatched-response" })
            {
                var mail = CreateMailCase();
                if (mode != "mismatched-response") mail.Environment.ApplyOverride = reward =>
                {
                    if (mode == "rejected") return MailRewardApplyResult.Rejected("Known memory precondition failure");
                    if (mode == "unknown") return MailRewardApplyResult.Unknown(0, "Memory apply outcome unavailable");
                    Assert.That(mail.Fixture.Stage.Runtime[EStage.Stage1].GiveStaff("STAFF02"), Is.True);
                    if (mode == "throw") throw new InvalidOperationException("Failure after detached memory mutation");
                    return MailRewardApplyResult.Partial(1, "Only the first unit was applied");
                };
                var target = new MailReceiveTarget(PostType.Admin, "partial", new[] { new MailRewardItem("STAFF02", 2) }, mail.Query);
                Assert.That(mail.Operation.TryStart(new[] { target, MailTarget(mail, "must-not-start", "STAFF03") }, out var error), Is.True, error);
                mail.Transport.Calls[0].Reply(MailSuccess(mode == "mismatched-response" ? "Gold" : "STAFF02", 2));
                Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.ConsumedPendingApplication), mode);
                Assert.That(mail.Operation.Entries[0].Status, Is.EqualTo(MailReceiveEntryStatus.ConsumedPendingApplication), mode);
                Assert.That(mail.MemoryNotifications, Is.Zero, mode);
                Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1), mode);
                Assert.That(mail.Operation.Entries[1].Status, Is.EqualTo(MailReceiveEntryStatus.Pending), mode);
                Assert.That(mail.Fixture.Game.Writes, Is.Zero, mode);
                if (mode == "partial")
                {
                    Assert.That(mail.Operation.Entries[0].Progress.Single().Result.AppliedCount, Is.EqualTo(1));
                    Assert.That(mail.Operation.Entries[0].Progress.Single().Result.IsComplete, Is.False);
                    AssertAccountLevels(mail.Fixture, "STAFF02", 1);
                }
                if (mode == "throw")
                {
                    Assert.That(mail.Operation.Entries[0].Progress.Single().Result.OutcomeUnknown, Is.True);
                    AssertAccountLevels(mail.Fixture, "STAFF02", 1);
                }
                if (mode == "mismatched-response") Assert.That(mail.Environment.ApplyCalls, Is.Zero);
                int applied = mail.Environment.ApplyCalls;
                mail.Transport.Calls[0].Reply(MailSuccess("STAFF02", 2));
                Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(applied), mode);
                Assert.That(mail.Operation.TryStart(new[] { target }, out _), Is.False, mode);
                Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
                Assert.That(mail.Fixture.Manager.RequestGameDataAutosave().Status, Is.EqualTo(GameDataSaveRequestStatus.Accepted), mode);
                Assert.That(mail.Fixture.Game.Writes, Is.Zero, "Partial memory rewards are not sent by the queued ordinary save");
            }
        }
    }

    [Test]
    public void MailReceive_StaleSessionResponseCannotGrantToNewAccountAndLegacyStaffStillUseLocalOwnership()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string changed in new[] { "query", "same-account", "different-account" })
            {
                var mail = CreateMailCase();
                Assert.That(mail.Operation.TryStart(new[] { MailTarget(mail, "late", "STAFF02") }, out var error), Is.True, error);
                if (changed != "query")
                {
                    string account = mail.Fixture.Account;
                    mail.Fixture.Manager.LogOut();
                    Authenticate(mail.Fixture, changed == "different-account" ? Unique("mail-next-account") : account);
                }
                RestoreGameData(mail.Fixture, true);
                string current = JsonConvert.SerializeObject(mail.Fixture.Manager.StaffRuntime.Snapshot);
                mail.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
                Assert.That(mail.Environment.ApplyCalls, Is.Zero, changed);
                Assert.That(mail.MemoryNotifications, Is.Zero, changed);
                Assert.That(JsonConvert.SerializeObject(mail.Fixture.Manager.StaffRuntime.Snapshot), Is.EqualTo(current), changed);
                Assert.That(mail.Fixture.Game.Writes, Is.Zero, changed);
                Assert.That(mail.Operation.Status == MailReceiveStatus.Indeterminate || mail.Operation.Status == MailReceiveStatus.ConsumedPendingApplication, Is.True, changed);
            }
            var legacy = CreateMailCase(common: false);
            Assert.That(legacy.Operation.TryStart(new[] { MailTarget(legacy, "legacy", "STAFF02") }, out var reason), Is.True, reason);
            legacy.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
            Assert.That(legacy.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            Assert.That(legacy.Fixture.Stage.Runtime[EStage.Stage1].IsGiveStaff("STAFF02"), Is.True);
            Assert.That(legacy.Fixture.Stage.Runtime[EStage.Stage2].IsGiveStaff("STAFF02"), Is.False);
            Assert.That(legacy.Fixture.Manager.StaffRuntime.Snapshot, Is.Null);
            Assert.That(legacy.Fixture.Manager.StaffRuntime.Mode, Is.EqualTo(StaffAccountRuntimeMode.Legacy));
            Assert.That(legacy.Fixture.Game.Writes, Is.Zero);
            Assert.That(legacy.Wallet.CostCommits, Is.Zero);
        }
    }

    [Test]
    public void MailReceive_MixedRewardsKeepEarlierCurrencyExactlyOnceWhenLaterStaffApplicationFails()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string mode in new[] { "normal", "staff-rejected", "staff-throw" })
            {
                var mail = CreateMailCase();
                var rewards = new[] { new MailRewardItem("Gold", 10), new MailRewardItem("Dia", 5), new MailRewardItem("STAFF02", 2) };
                if (mode != "normal") mail.Environment.ApplyOverride = reward =>
                {
                    if (reward.Id != "STAFF02") return mail.Environment.ApplyNormally(reward);
                    if (mode == "staff-rejected") return MailRewardApplyResult.Rejected("Staff entry refused after preceding currencies");
                    throw new InvalidOperationException("Staff entry threw after preceding currencies");
                };
                var target = new MailReceiveTarget(PostType.Admin, "mixed", rewards, mail.Query);
                Assert.That(mail.Operation.TryStart(new[] { target }, out var error), Is.True, error);
                var response = new MailReceiveResponse(true, new JObject { ["postItems"] = new JArray(rewards.Select(reward => MailRewardJson(reward.Id, reward.Count))) }.ToString(Formatting.None));
                mail.Transport.Calls[0].Reply(response);
                Assert.That(mail.Wallet.Gold, Is.EqualTo(1000010));
                Assert.That(mail.Wallet.Diamonds, Is.EqualTo(115));
                Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(3));
                Assert.That(mail.Operation.Entries[0].Progress.Count, Is.EqualTo(3));
                CollectionAssert.AreEqual(new[] { 10, 5 }, mail.Operation.Entries[0].Progress.Take(2).Select(progress => progress.Result.AppliedCount));
                Assert.That(mail.Operation.Entries[0].Progress.Take(2).All(progress => progress.Result.IsComplete), Is.True);
                Assert.That(mail.Operation.Status, Is.EqualTo(mode == "normal" ? MailReceiveStatus.MemoryApplied : MailReceiveStatus.ConsumedPendingApplication));
                Assert.That(mail.MemoryNotifications, Is.EqualTo(mode == "normal" ? 1 : 0));
                Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.Staff.Any(staff => staff.Id == "STAFF02"), Is.EqualTo(mode == "normal"));
                Assert.That(mail.Operation.Entries[0].Progress[2].Result.AppliedCount, Is.EqualTo(mode == "normal" ? 2 : 0));
                if (mode == "staff-throw") Assert.That(mail.Operation.Entries[0].Progress[2].Result.OutcomeUnknown, Is.True);
                mail.Transport.Calls[0].Reply(response);
                Assert.That(mail.Wallet.Gold, Is.EqualTo(1000010));
                Assert.That(mail.Wallet.Diamonds, Is.EqualTo(115));
                Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(3));
                Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(1));
                Assert.That(mail.Fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
                Assert.That(mail.Fixture.Game.Writes, Is.Zero);
            }
        }
    }

    [Test]
    public void MailReceive_ImmediateDuplicateRepliesAndCompletionReentryNeverStartAnotherRequest()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var mail = CreateMailCase();
            int refused = 0;
            mail.OnApplied = entry =>
            {
                if (!mail.Operation.TryStart(new[] { MailTarget(mail, "reentrant", "STAFF03") }, out _)) refused++;
            };
            mail.Transport.OnSend = call =>
            {
                call.Reply(MailSuccess("STAFF02", 1));
                call.Reply(MailSuccess("STAFF02", 1));
            };
            Assert.That(mail.Operation.TryStart(new[] { MailTarget(mail, "sync", "STAFF02") }, out var error), Is.True, error);
            Assert.That(mail.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            Assert.That(mail.MemoryNotifications, Is.EqualTo(1));
            Assert.That(refused, Is.EqualTo(1));
            Assert.That(mail.Environment.ApplyCalls, Is.EqualTo(1));
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1));
            Assert.That(mail.Operation.PublicCallCount, Is.EqualTo(1));
            AssertAccountLevels(mail.Fixture, "STAFF02", 1);
            Assert.That(mail.Operation.CanStart, Is.True, "An immediate response must leave the completed operation startable after the stack unwinds");

            var nested = CreateMailCase();
            bool entered = false;
            bool acceptedInsideOuterNotification = true;
            nested.Operation = new MailReceiveOperation(nested.Environment, nested.Transport,
                entry => nested.MemoryNotifications++, operation =>
                {
                    if (entered || operation.Status != MailReceiveStatus.Indeterminate) return;
                    entered = true;
                    nested.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
                    acceptedInsideOuterNotification = operation.TryStart(new[] { MailTarget(nested, "nested-start", "STAFF03") }, out _);
                });
            Assert.That(nested.Operation.TryStart(new[] { MailTarget(nested, "nested-notify", "STAFF02") }, out var nestedError), Is.True, nestedError);
            Assert.That(nested.Operation.MarkResponseMissing(), Is.True);
            Assert.That(entered, Is.True);
            Assert.That(acceptedInsideOuterNotification, Is.False, "The nested completion notification must preserve the outer observer's reentry fence");
            Assert.That(nested.MemoryNotifications, Is.EqualTo(1));
            Assert.That(nested.Environment.ApplyCalls, Is.EqualTo(1));
            Assert.That(nested.Transport.Calls.Count, Is.EqualTo(1));
            Assert.That(nested.Operation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            Assert.That(nested.Operation.CanStart, Is.True);
        }
    }

    [Test]
    public void MailReceive_ManagerBoundaryKeepsMemoryReceiptAndUiCompletionSeparateFromSaving()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var mail = CreateMailCase();
            var manager = (MailManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MailManager));
            var first = MailDataFor(mail, "manager-admin", PostType.Admin);
            var second = MailDataFor(mail, "manager-coupon", PostType.Coupon);
            Field(typeof(MailManager), "_mailList").SetValue(manager, new List<MailData> { first, second });
            int recorded = 0, received = 0, allReceived = 0, completed = 0, failed = 0;
            manager.ConfigureReceiveBoundary(mail.Environment, mail.Transport,
                () => mail.Fixture.Manager.CurrentMailReceiveQuery, mail.Fixture.Manager.IsCurrentGameDataQuery, _ => false,
                _ => { recorded++; mail.Fixture.Manager.RequestGameDataAutosave(); });
            manager.OnMailReceived += _ => received++;
            manager.OnAllMailReceived += () => allReceived++;
            manager.ReceiveAllAvailableMailAsync(() => completed++, () => failed++);
            var originalOperation = manager.CurrentReceiveOperation;
            Assert.That(manager.GetReceiveStatusText(first), Does.Contain("처리 중"));
            Assert.That(manager.CanReceive(first), Is.False);
            Assert.That(first.IsReceived, Is.False);
            Assert.That(completed, Is.Zero);
            Assert.That(mail.Transport.Calls.Count, Is.EqualTo(1), "Both post types share one sequential operation");
            mail.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
            Assert.That(first.IsReceived, Is.True); Assert.That(second.IsReceived, Is.False);
            Assert.That(recorded, Is.EqualTo(1)); Assert.That(received, Is.EqualTo(1));
            Assert.That(completed, Is.Zero); Assert.That(allReceived, Is.Zero);
            Assert.That(mail.Fixture.Game.Writes, Is.Zero);
            mail.Transport.Calls[1].Reply(MailSuccess("STAFF02", 1));
            Assert.That(second.IsReceived, Is.True);
            Assert.That(recorded, Is.EqualTo(2)); Assert.That(received, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1)); Assert.That(allReceived, Is.EqualTo(1)); Assert.That(failed, Is.Zero);
            Assert.That(manager.CurrentReceiveOperation, Is.SameAs(originalOperation));
            Assert.That(manager.GetReceiveStatusText(first), Does.Contain("저장 확인 별도"));
            Assert.That(mail.Fixture.Game.Writes, Is.EqualTo(1), "The memory completion callback does not wait for or claim this save response");
            foreach (var call in mail.Transport.Calls.ToArray()) call.Reply(MailSuccess("STAFF02", 1));
            Assert.That(recorded, Is.EqualTo(2)); Assert.That(received, Is.EqualTo(2)); Assert.That(completed, Is.EqualTo(1));

            var rejected = CreateMailCase();
            var rejectedManager = (MailManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MailManager));
            var pending = MailDataFor(rejected, "manager-pending", PostType.Admin);
            Field(typeof(MailManager), "_mailList").SetValue(rejectedManager, new List<MailData> { pending });
            int pendingRecords = 0, pendingSuccess = 0, pendingFailure = 0;
            rejected.Environment.ApplyOverride = _ => MailRewardApplyResult.Rejected("Memory apply refused");
            rejectedManager.ConfigureReceiveBoundary(rejected.Environment, rejected.Transport,
                () => rejected.Fixture.Manager.CurrentMailReceiveQuery, rejected.Fixture.Manager.IsCurrentGameDataQuery,
                _ => false, _ => pendingRecords++);
            rejectedManager.ReceiveMailAsync(pending, _ => pendingSuccess++, () => pendingFailure++);
            rejected.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
            Assert.That(pending.IsReceived, Is.False);
            Assert.That(rejectedManager.IsReceiveDisplayComplete(pending), Is.False);
            Assert.That(rejectedManager.GetReceiveStatusText(pending), Does.Contain("반영 확인"));
            Assert.That(pendingRecords, Is.Zero); Assert.That(pendingSuccess, Is.Zero); Assert.That(pendingFailure, Is.EqualTo(1));
            rejected.Transport.Calls[0].Reply(MailSuccess("STAFF02", 1));
            Assert.That(pendingFailure, Is.EqualTo(1)); Assert.That(pendingRecords, Is.Zero);
            Assert.That(rejected.Fixture.Game.Writes, Is.Zero);

            // Every UI subscriber is an external reentry boundary, including an observer of a final memory state.
            foreach (string boundary in new[] { "refreshed-observer", "all-success-observer" })
            {
                var changing = CreateMailCase();
                var changingManager = (MailManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MailManager));
                var oldMail = MailDataFor(changing, "changing-ui-" + boundary, PostType.Admin);
                Field(typeof(MailManager), "_mailList").SetValue(changingManager, new List<MailData> { oldMail });
                int memoryRecords = 0, successCalls = 0, failureCalls = 0, allEvents = 0, invalidations = 0;
                changingManager.ConfigureReceiveBoundary(changing.Environment, changing.Transport,
                    () => changing.Fixture.Manager.CurrentMailReceiveQuery, changing.Fixture.Manager.IsCurrentGameDataQuery,
                    _ => false, _ => memoryRecords++);
                changingManager.OnAllMailReceived += () => allEvents++;
                changingManager.OnMailListRefreshed += () =>
                {
                    if (boundary != "refreshed-observer" || invalidations != 0
                        || changingManager.CurrentReceiveOperation.Status != MailReceiveStatus.MemoryApplied) return;
                    invalidations++;
                    changing.Fixture.Manager.InvalidateGameDataRestore();
                };
                changingManager.ReceiveAllAvailableMailAsync(() =>
                {
                    successCalls++;
                    if (boundary != "all-success-observer") return;
                    invalidations++;
                    changing.Fixture.Manager.InvalidateGameDataRestore();
                }, () => failureCalls++);
                changing.Transport.Calls.Single().Reply(MailSuccess("STAFF02", 1));
                Assert.That(invalidations, Is.EqualTo(1), boundary);
                Assert.That(memoryRecords, Is.EqualTo(1), "The old account's memory grant finished before the observer switched sessions");
                Assert.That(oldMail.IsReceived, Is.True, boundary);
                Assert.That(changingManager.CurrentReceiveOperation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied), boundary);
                Assert.That(successCalls, Is.EqualTo(boundary == "all-success-observer" ? 1 : 0), boundary);
                Assert.That(failureCalls, Is.Zero, "Session invalidation is not a new completion notification for the old UI");
                Assert.That(allEvents, Is.Zero, "Do not send the old OnAllMailReceived event after an earlier subscriber changed the session");
                Assert.That(changing.Fixture.Game.Writes, Is.Zero);
                changing.Transport.Calls.Single().Reply(MailSuccess("STAFF02", 1));
                Assert.That(memoryRecords, Is.EqualTo(1));
                Assert.That(successCalls, Is.EqualTo(boundary == "all-success-observer" ? 1 : 0));
                Assert.That(failureCalls, Is.Zero); Assert.That(allEvents, Is.Zero);
            }

            var throwing = CreateMailCase();
            var throwingManager = (MailManager)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MailManager));
            var throwingMail = MailDataFor(throwing, "throwing-refreshed-observer", PostType.Admin);
            Field(typeof(MailManager), "_mailList").SetValue(throwingManager, new List<MailData> { throwingMail });
            int throwingRecords = 0, throwingSuccess = 0, throwingFailure = 0, throwingAll = 0, observerThrows = 0, afterThrow = 0;
            throwingManager.ConfigureReceiveBoundary(throwing.Environment, throwing.Transport,
                () => throwing.Fixture.Manager.CurrentMailReceiveQuery, throwing.Fixture.Manager.IsCurrentGameDataQuery,
                _ => false, _ => throwingRecords++);
            throwingManager.OnMailListRefreshed += () =>
            {
                if (throwingManager.CurrentReceiveOperation.Status != MailReceiveStatus.MemoryApplied) return;
                observerThrows++;
                throw new InvalidOperationException("Detached UI observer failure");
            };
            throwingManager.OnMailListRefreshed += () =>
            {
                if (throwingManager.CurrentReceiveOperation.Status == MailReceiveStatus.MemoryApplied) afterThrow++;
            };
            throwingManager.OnAllMailReceived += () => throwingAll++;
            throwingManager.ReceiveAllAvailableMailAsync(() => throwingSuccess++, () => throwingFailure++);
            throwing.Transport.Calls.Single().Reply(MailSuccess("STAFF02", 1));
            Assert.That(observerThrows, Is.EqualTo(1)); Assert.That(afterThrow, Is.EqualTo(1));
            Assert.That(throwingRecords, Is.EqualTo(1)); Assert.That(throwingSuccess, Is.EqualTo(1));
            Assert.That(throwingFailure, Is.Zero); Assert.That(throwingAll, Is.EqualTo(1));
            Assert.That(throwingMail.IsReceived, Is.True);
            Assert.That(throwingManager.CurrentReceiveOperation.Status, Is.EqualTo(MailReceiveStatus.MemoryApplied));
            AssertAccountLevels(throwing.Fixture, "STAFF02", 1);
            Assert.That(throwing.Fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(55));
            Assert.That(throwing.Fixture.Game.Writes, Is.Zero);
            throwing.Transport.Calls.Single().Reply(MailSuccess("STAFF02", 1));
            Assert.That(observerThrows, Is.EqualTo(1)); Assert.That(afterThrow, Is.EqualTo(1));
            Assert.That(throwingSuccess, Is.EqualTo(1)); Assert.That(throwingRecords, Is.EqualTo(1));
            Assert.That(throwingAll, Is.EqualTo(1)); Assert.That(throwingFailure, Is.Zero);
        }
    }

    private MailCase CreateMailCase(bool common = true)
    {
        var fixture = CreateAccountRuntimeFixture(out var wallet, common);
        var result = new MailCase { Fixture = fixture, Wallet = wallet, Query = fixture.Manager.CurrentMailReceiveQuery };
        result.Environment = new MemoryMailEnvironment(fixture, wallet);
        result.Transport = new MemoryMailTransport();
        result.Operation = new MailReceiveOperation(result.Environment, result.Transport, entry =>
        { result.MemoryNotifications++; result.OnApplied?.Invoke(entry); });
        return result;
    }
    private static MailReceiveTarget MailTarget(MailCase mail, string id, string reward, PostType type = PostType.Admin) =>
        new MailReceiveTarget(type, id, new[] { new MailRewardItem(reward, 1) }, mail.Query);
    private static JObject MailRewardJson(string id, int count) => new JObject
    { ["item"] = new JObject { ["itemID"] = id }, ["itemCount"] = count };
    private static MailReceiveResponse MailSuccess(string id, int count) => new MailReceiveResponse(true,
        new JObject { ["postItems"] = new JArray(MailRewardJson(id, count)) }.ToString(Formatting.None));
    private static MailData MailDataFor(MailCase mail, string id, PostType type) => new MailData(type,
        JsonMapper.ToObject(new JObject { ["inDate"] = id, ["title"] = "Detached manager mail", ["items"] = new JArray(MailRewardJson("STAFF02", 1)) }.ToString(Formatting.None)), mail.Query);
    private sealed class MailCase
    {
        public Fixture Fixture;
        public MemoryStaffWallet Wallet;
        public GameDataRestoreQuery Query;
        public MemoryMailEnvironment Environment;
        public MemoryMailTransport Transport;
        public MailReceiveOperation Operation;
        public int MemoryNotifications;
        public Action<MailReceiveEntry> OnApplied;
    }
    private sealed class MemoryMailTransport : IMailReceiveTransport
    {
        public readonly List<MailCall> Calls = new List<MailCall>();
        public Action<MailCall> OnSend;
        public void ReceiveOnce(MailReceiveTarget target, Action<MailReceiveResponse> response)
        { var call = new MailCall { Target = target, Reply = response }; Calls.Add(call); OnSend?.Invoke(call); }
        public sealed class MailCall { public MailReceiveTarget Target; public Action<MailReceiveResponse> Reply; }
    }
    private sealed class MemoryMailEnvironment : IMailReceiveEnvironment
    {
        private readonly Fixture _fixture;
        private readonly MemoryStaffWallet _wallet;
        public int ApplyCalls;
        public bool RefuseValidation;
        public Action<IReadOnlyList<MailRewardItem>> OnValidate;
        public Func<MailRewardItem, MailRewardApplyResult> ApplyOverride;
        public MemoryMailEnvironment(Fixture fixture, MemoryStaffWallet wallet) { _fixture = fixture; _wallet = wallet; }
        public bool TryAcquire(out IMailReceiveProtection protection, out string error)
        {
            bool success = _fixture.Manager.TryAcquireMailSaveLease(out var lease, out error);
            protection = lease; return success;
        }
        public bool TryValidate(IReadOnlyList<MailRewardItem> rewards, out string error)
        {
            error = null;
            OnValidate?.Invoke(rewards);
            if (RefuseValidation) { error = "Explicit memory validation refusal"; return false; }
            if (rewards == null) { error = "No reward manifest"; return false; }
            foreach (var reward in rewards)
            {
                if (reward == null || reward.Count <= 0 || (reward.Id != "Gold" && reward.Id != "Dia" && !_fixture.Game.Catalog.Any(staff => staff.Id == reward.Id)))
                { error = "The memory test only accepts currencies and registered staff rewards"; return false; }
            }
            return true;
        }
        public MailRewardApplyResult Apply(MailRewardItem reward)
        {
            ApplyCalls++;
            if (ApplyOverride != null) return ApplyOverride(reward);
            return ApplyNormally(reward);
        }
        public MailRewardApplyResult ApplyNormally(MailRewardItem reward)
        {
            if (reward.Id == "Gold") { _wallet.GoldValue += reward.Count; return MailRewardApplyResult.Applied(reward.Count); }
            if (reward.Id == "Dia") { _wallet.DiamondValue += reward.Count; return MailRewardApplyResult.Applied(reward.Count); }
            // Existing ownership rewards call Give once; acknowledged quantity is not a new-owned count.
            if (!_fixture.Stage.Runtime[EStage.Stage1].GiveStaff(reward.Id))
                return MailRewardApplyResult.Rejected("Detached staff grant rejected");
            return MailRewardApplyResult.Applied(reward.Count);
        }
    }
}
#endif
