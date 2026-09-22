using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Muks.BackEnd
{
    /// <summary>
    /// Observes actual legacy application separately from raw-response collection.
    /// A valid preparation is only an immutable, unsaved candidate; it changes no runtime or saved data.
    /// </summary>
    public sealed partial class StaffStageMigrationCollection
    {
        private readonly StaffStageApplicationResult[] _applications = CreatePendingApplications();
        private readonly object[] _applicationOwners = new object[Scope.Length];
        private readonly bool[] _applicationCompleting = new bool[Scope.Length];
        private readonly string[] _applicationObservationErrors = new string[Scope.Length];
        private GameDataSaveTarget _preparationTarget;
        private Func<EStage, StaffStageRuntimeSnapshot> _readRuntimeStage;
        private Func<IReadOnlyList<StaffData>> _readPreparationCatalog;
        private bool _runtimeConfigured;
        private bool _validatingPreparation;
        private bool _validationReentered;

        public IReadOnlyList<StaffStageApplicationResult> Applications
        {
            get
            {
                RefreshCurrent();
                return Array.AsReadOnly((StaffStageApplicationResult[])_applications.Clone());
            }
        }

        // Configuration is fixed for this query/round and does not invoke external delegates.
        internal void ConfigureRuntime(GameDataSaveTarget target,
            Func<EStage, StaffStageRuntimeSnapshot> readStage, Func<IReadOnlyList<StaffData>> readCatalog)
        {
            if (_runtimeConfigured) return;
            _runtimeConfigured = true;
            _preparationTarget = target;
            _readRuntimeStage = readStage;
            _readPreparationCatalog = readCatalog;
        }

        internal bool BeginApplication(EStage stage)
        {
            int index = Array.IndexOf(Scope, stage);
            if (index < 0 || !RefreshCurrent()
                || _applications[index].Status != StaffStageApplicationStatus.Pending)
                return false;

            // Set the fence before observation or the real legacy Apply can reenter this collector.
            _applications[index] = new StaffStageApplicationResult(stage, StaffStageApplicationStatus.Applying);
            try
            {
                StaffStageRuntimeSnapshot before = _readRuntimeStage?.Invoke(stage);
                if (before == null || before.IsApplying)
                    _applicationObservationErrors[index] = "The Stage identity was unavailable before legacy application.";
                else
                    _applicationOwners[index] = before.OwnerToken;
            }
            catch
            {
                _applicationObservationErrors[index] = "The Stage identity could not be observed before legacy application.";
            }

            // Missing observation only blocks preparation, not otherwise-valid legacy loading.
            if (RefreshCurrent()) return true;
            _applications[index] = new StaffStageApplicationResult(stage, StaffStageApplicationStatus.Failed,
                error: "The Stage query/round was invalidated before legacy application.");
            return false;
        }

        internal void CompleteApplication(EStage stage, bool succeeded, string error = null)
        {
            int index = Array.IndexOf(Scope, stage);
            if (index < 0 || _applications[index].Status != StaffStageApplicationStatus.Applying
                || _applicationCompleting[index])
                return;
            _applicationCompleting[index] = true;
            try
            {
                if (!RefreshCurrent() || !succeeded)
                {
                    _applications[index] = new StaffStageApplicationResult(stage, StaffStageApplicationStatus.Failed,
                        error: error ?? "Legacy application did not complete in the current Stage query/round.");
                    return;
                }
                StaffStageRuntimeSnapshot after;
                try { after = _readRuntimeStage?.Invoke(stage); }
                catch
                {
                    _applications[index] = new StaffStageApplicationResult(stage, StaffStageApplicationStatus.Failed,
                        error: "The completed Stage state could not be observed.");
                    return;
                }
                if (!RefreshCurrent() || after == null || after.IsApplying
                    || _applicationObservationErrors[index] != null || _applicationOwners[index] == null
                    || !ReferenceEquals(_applicationOwners[index], after.OwnerToken))
                {
                    _applications[index] = new StaffStageApplicationResult(stage, StaffStageApplicationStatus.Failed,
                        error: _applicationObservationErrors[index]
                            ?? "The completed Stage is unavailable, still applying, replaced, or invalidated.");
                    return;
                }
                _applications[index] = new StaffStageApplicationResult(stage,
                    StaffStageApplicationStatus.Succeeded, snapshot: after);
            }
            finally { _applicationCompleting[index] = false; }
        }

        public StaffMigrationPreparationResult PrepareMigration() => ValidatePreparation(null, false);

        public StaffMigrationPreparationResult RevalidateMigration(StaffMigrationPreparation prior)
            => ValidatePreparation(prior, true);

        private StaffMigrationPreparationResult ValidatePreparation(StaffMigrationPreparation prior, bool revalidating)
        {
            if (_validatingPreparation)
            {
                _validationReentered = true;
                return PreparationFailure(StaffMigrationPreparationStatus.ReentrantValidation,
                    "Migration preparation was reentered during observation.");
            }
            _validatingPreparation = true;
            _validationReentered = false;
            try
            {
                StaffMigrationPreparationResult failure = CheckPreparationCurrent();
                if (failure != null) return failure;
                if (!_migrationRequired)
                    return PreparationFailure(StaffMigrationPreparationStatus.NotRequired,
                        "Existing common staff data does not require a Stage migration candidate.");
                if (!_runtimeConfigured || _preparationTarget == null || !_preparationTarget.IsValid
                    || !string.Equals(_preparationTarget.AccountInDate, Query.AccountInDate, StringComparison.Ordinal))
                    return PreparationFailure(StaffMigrationPreparationStatus.NotStarted,
                        "The runtime observations and restored GameData target have not been configured.");
                if (revalidating && (prior == null || !ReferenceEquals(prior.Owner, this)
                    || !ReferenceEquals(prior.Query, Query) || prior.Round != Round
                    || !_preparationTarget.Matches(prior.Target)))
                    return PreparationFailure(StaffMigrationPreparationStatus.Invalidated,
                        "The preparation belongs to a different query, round, target, or collector.");
                if (_status == StaffStageMigrationCollectionStatus.Blocked)
                    return PreparationFailure(StaffMigrationPreparationStatus.RawBlocked,
                        "The pre-correction Stage candidate is blocked; runtime values cannot repair it.");
                if (_status != StaffStageMigrationCollectionStatus.UnsavedMigrationCandidate
                    || _result == null || _result.Status != StaffAccountLoadStatus.UnsavedMigrationCandidate
                    || _result.MigrationCandidate == null || _result.ExistingData != null)
                    return PreparationFailure(StaffMigrationPreparationStatus.RawNotReady,
                        "All required raw Stage sources have not produced an unsaved candidate.");

                StaffAccountSaveData candidate = _result.MigrationCandidate;
                if (revalidating && !ReferenceEquals(prior.Candidate, candidate))
                    return PreparationFailure(StaffMigrationPreparationStatus.Invalidated,
                        "The original unsaved candidate is no longer the same candidate.");
                foreach (StaffStageApplicationResult application in _applications)
                    if (application.Status == StaffStageApplicationStatus.Applying)
                        return PreparationFailure(StaffMigrationPreparationStatus.ApplyInProgress,
                            "A required Stage is still applying its legacy state.");
                foreach (StaffStageApplicationResult application in _applications)
                {
                    if (application.Status == StaffStageApplicationStatus.Failed)
                        return PreparationFailure(StaffMigrationPreparationStatus.ApplyFailed,
                            "A required Stage legacy application or its independent observation failed.");
                    if (application.Status != StaffStageApplicationStatus.Succeeded)
                        return PreparationFailure(StaffMigrationPreparationStatus.ApplyPending,
                            "A required Stage legacy application has not completed.");
                }

                failure = ReadPreparationCatalog(out IReadOnlyDictionary<string, int> maximums);
                if (failure != null) return failure;
                for (int index = 0; index < Scope.Length; index++)
                {
                    failure = ValidateSource(index, maximums);
                    if (failure != null) return failure;
                }
                if (GameDataRestoreContext.ValidateStaffLevels(candidate.Staff, maximums, out _) != GameDataRestoreStatus.Ready)
                    return PreparationFailure(StaffMigrationPreparationStatus.SourceMismatch,
                        "The original candidate does not match the current registered level evidence.");

                // Each external observation is followed by query/reentrancy checks. Observe all three
                // stages again after the catalog callback so its side effects cannot bypass the baseline.
                failure = ReadPreparationStages(out StaffStageRuntimeSnapshot[] observed);
                if (failure != null) return failure;
                failure = ReadPreparationCatalog(out IReadOnlyDictionary<string, int> finalMaximums);
                if (failure != null) return failure;
                if (!SameMaximums(maximums, finalMaximums))
                    return PreparationFailure(StaffMigrationPreparationStatus.CatalogChanged,
                        "Registered level evidence changed during preparation.");
                failure = ReadPreparationStages(out StaffStageRuntimeSnapshot[] finalObserved);
                if (failure != null) return failure;
                for (int index = 0; index < Scope.Length; index++)
                    if (!observed[index].HasSameState(finalObserved[index]))
                        return PreparationFailure(StaffMigrationPreparationStatus.RuntimeChanged,
                            "A Stage state changed during preparation.");
                failure = CheckPreparationCurrent();
                if (failure != null) return failure;

                var evidence = new StaffMigrationStageEvidence[Scope.Length];
                for (int index = 0; index < Scope.Length; index++)
                    evidence[index] = new StaffMigrationStageEvidence(Scope[index], _stages[index].RowInDate,
                        finalObserved[index]);
                var preparation = new StaffMigrationPreparation(this, Query, _preparationTarget, Round,
                    candidate, evidence, finalMaximums);
                return new StaffMigrationPreparationResult(StaffMigrationPreparationStatus.Valid,
                    preparation: preparation);
            }
            finally { _validatingPreparation = false; }
        }

        private StaffMigrationPreparationResult CheckPreparationCurrent()
        {
            if (_validationReentered)
                return PreparationFailure(StaffMigrationPreparationStatus.ReentrantValidation,
                    "Migration preparation was reentered during observation.");
            bool current = RefreshCurrent();
            if (_validationReentered)
                return PreparationFailure(StaffMigrationPreparationStatus.ReentrantValidation,
                    "Migration preparation was reentered during current-session validation.");
            return current ? null : PreparationFailure(StaffMigrationPreparationStatus.Invalidated,
                "The account, GameData query, or Stage collection round is no longer current.");
        }

        private StaffMigrationPreparationResult ReadPreparationCatalog(out IReadOnlyDictionary<string, int> maximums)
        {
            maximums = null;
            GameDataRestoreStatus status;
            try
            {
                status = GameDataRestoreContext.TryBuildStaffMaximums(_readPreparationCatalog,
                    out maximums, out _);
            }
            catch { status = GameDataRestoreStatus.InvalidStaffCatalog; }
            StaffMigrationPreparationResult failure = CheckPreparationCurrent();
            if (failure != null) return failure;
            if (status != GameDataRestoreStatus.Ready || maximums == null)
                return PreparationFailure(StaffMigrationPreparationStatus.CatalogInvalid,
                    "Current registered staff and maximum-level evidence could not be validated.");
            return SameMaximums(_maximums, maximums) ? null
                : PreparationFailure(StaffMigrationPreparationStatus.CatalogChanged,
                    "Registered staff or maximum-level evidence changed after raw collection.");
        }

        private StaffMigrationPreparationResult ReadPreparationStages(out StaffStageRuntimeSnapshot[] snapshots)
        {
            snapshots = new StaffStageRuntimeSnapshot[Scope.Length];
            for (int index = 0; index < Scope.Length; index++)
            {
                StaffStageRuntimeSnapshot snapshot = null;
                bool readFailed = false;
                try { snapshot = _readRuntimeStage?.Invoke(Scope[index]); }
                catch { readFailed = true; }
                StaffMigrationPreparationResult failure = CheckPreparationCurrent();
                if (failure != null) return failure;
                if (readFailed || snapshot == null)
                    return PreparationFailure(StaffMigrationPreparationStatus.RuntimeUnavailable,
                        "A current Stage runtime snapshot could not be read.");
                if (snapshot.IsApplying)
                    return PreparationFailure(StaffMigrationPreparationStatus.ApplyInProgress,
                        "A required Stage is currently applying legacy state.");
                if (!_applications[index].Snapshot.HasSameState(snapshot))
                    return PreparationFailure(StaffMigrationPreparationStatus.RuntimeChanged,
                        "A Stage identity, change token, or staff value changed after legacy application.");
                snapshots[index] = snapshot;
            }
            return null;
        }

        private StaffMigrationPreparationResult ValidateSource(int index, IReadOnlyDictionary<string, int> maximums)
        {
            StaffStageRawResult raw = _stages[index];
            StaffStageRuntimeSnapshot baseline = _applications[index].Snapshot;
            if (raw == null || !raw.HasVerifiedRawRecords || string.IsNullOrWhiteSpace(raw.RowInDate)
                || baseline == null || baseline.IsApplying || raw.Records.Count != baseline.Staff.Count)
                return PreparationFailure(StaffMigrationPreparationStatus.SourceMismatch,
                    "The applied staff state does not correspond to its verified raw Stage row.");
            var rawById = new Dictionary<string, StaffStageRawRecord>(StringComparer.Ordinal);
            foreach (StaffStageRawRecord record in raw.Records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id) || !record.Level.HasValue
                    || rawById.ContainsKey(record.Id))
                    return PreparationFailure(StaffMigrationPreparationStatus.SourceMismatch,
                        "The raw Stage source cannot correspond one-to-one with its runtime dictionary.");
                rawById.Add(record.Id, record);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var staffLevels = new List<StaffAccountStaffRecord>();
            foreach (StaffStageRuntimeRecord record in baseline.Staff)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Id) || !record.Level.HasValue
                    || !string.Equals(record.DictionaryId, record.Id, StringComparison.Ordinal) || !seen.Add(record.Id)
                    || !rawById.TryGetValue(record.Id, out StaffStageRawRecord source)
                    || source.Level != record.Level
                    || !string.Equals(source.SkinIdPresent ? source.SkinId : string.Empty,
                        record.SkinId, StringComparison.Ordinal))
                    return PreparationFailure(StaffMigrationPreparationStatus.SourceMismatch,
                        "Applied staff ID, level, or skin differs from the uncorrected Stage source.");
                staffLevels.Add(new StaffAccountStaffRecord(record.Id, record.Level.Value));
            }
            return GameDataRestoreContext.ValidateStaffLevels(staffLevels, maximums, out _) == GameDataRestoreStatus.Ready
                ? null : PreparationFailure(StaffMigrationPreparationStatus.SourceMismatch,
                    "Applied staff values do not satisfy the shared registered-level validation.");
        }

        private static bool SameMaximums(IReadOnlyDictionary<string, int> first, IReadOnlyDictionary<string, int> second)
        {
            if (first == null || second == null || first.Count != second.Count) return false;
            foreach (KeyValuePair<string, int> item in first)
                if (!second.TryGetValue(item.Key, out int maximum) || maximum != item.Value) return false;
            return true;
        }

        private static StaffMigrationPreparationResult PreparationFailure(StaffMigrationPreparationStatus status, string error)
            => new StaffMigrationPreparationResult(status, error: error);

        private static StaffStageApplicationResult[] CreatePendingApplications()
        {
            var applications = new StaffStageApplicationResult[Scope.Length];
            for (int index = 0; index < Scope.Length; index++)
                applications[index] = new StaffStageApplicationResult(Scope[index], StaffStageApplicationStatus.Pending);
            return applications;
        }
    }

    public enum StaffStageApplicationStatus { Pending, Applying, Succeeded, Failed }

    public sealed class StaffStageApplicationResult
    {
        public EStage Stage { get; }
        public StaffStageApplicationStatus Status { get; }
        public string Error { get; }
        public StaffStageRuntimeSnapshot Snapshot { get; }
        internal StaffStageApplicationResult(EStage stage, StaffStageApplicationStatus status,
            string error = null, StaffStageRuntimeSnapshot snapshot = null)
        { Stage = stage; Status = status; Error = error; Snapshot = snapshot; }
    }

    public enum StaffMigrationPreparationStatus
    {
        Valid, NotStarted, NotRequired, Invalidated, RawNotReady, RawBlocked, ApplyPending,
        ApplyInProgress, ApplyFailed, RuntimeUnavailable, RuntimeChanged, SourceMismatch,
        CatalogInvalid, CatalogChanged, ReentrantValidation
    }

    public sealed class StaffMigrationPreparationResult
    {
        public StaffMigrationPreparationStatus Status { get; }
        public string Error { get; }
        public StaffMigrationPreparation Preparation { get; }
        internal StaffMigrationPreparationResult(StaffMigrationPreparationStatus status,
            StaffMigrationPreparation preparation = null, string error = null)
        {
            if ((status == StaffMigrationPreparationStatus.Valid) != (preparation != null))
                throw new ArgumentException("Only a valid observation can contain an unsaved preparation.");
            Status = status;
            Preparation = preparation;
            Error = error;
        }
    }

    /// <summary>Bound to one observed round. It is not a save payload, migration-completion marker, or game-ready flag.</summary>
    public sealed class StaffMigrationPreparation
    {
        internal StaffStageMigrationCollection Owner { get; }
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveTarget Target { get; }
        public long Round { get; }
        public StaffAccountSaveData Candidate { get; }
        public IReadOnlyList<StaffMigrationStageEvidence> Stages { get; }
        public IReadOnlyDictionary<string, int> CatalogMaximums { get; }

        internal StaffMigrationPreparation(StaffStageMigrationCollection owner, GameDataRestoreQuery query,
            GameDataSaveTarget target, long round, StaffAccountSaveData candidate,
            IReadOnlyList<StaffMigrationStageEvidence> stages, IReadOnlyDictionary<string, int> maximums)
        {
            Owner = owner;
            Query = query;
            Target = target;
            Round = round;
            Candidate = candidate;
            Stages = Array.AsReadOnly(new List<StaffMigrationStageEvidence>(stages).ToArray());
            var copied = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> item in maximums) copied.Add(item.Key, item.Value);
            CatalogMaximums = new ReadOnlyDictionary<string, int>(copied);
        }
    }

    public sealed class StaffMigrationStageEvidence
    {
        public EStage Stage { get; }
        public string RowInDate { get; }
        public StaffStageRuntimeSnapshot Snapshot { get; }
        internal StaffMigrationStageEvidence(EStage stage, string rowInDate, StaffStageRuntimeSnapshot snapshot)
        { Stage = stage; RowInDate = rowInDate; Snapshot = snapshot; }
    }
}
