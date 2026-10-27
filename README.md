# Panda_Restaurant

## Staff and pictorial book UI refresh

- `UIStaff.ShowUIStaff` prepares the requested floor, role, selection and slots
  before navigation activates the view. Direct `Show` retains the default Manager
  role. Quest entries also prepare their exact requested staff before activation,
  instead of rendering a default selection and selecting again after `Push`.
  Every entry reads current data; it is not skipped solely because the
  floor/role matches the previous entry.
- Staff cards compare their displayed ownership, placement, rank, portrait and
  name. Unchanged cards and sibling order are left alone. Placement events from
  other floors are still checked because a move can affect the displayed staff.
  The owner refreshes selected detail without duplicate preview subscriptions.
  This detail's acquisition action is gacha and upgrade entry eligibility is
  level-based, so money/score events no longer rebuild it; actual purchase/upgrade
  validation remains with the existing purchase/upgrade views.
- The pictorial book prepares its default item tab and closes customer skin
  presentation while its root is inactive. Customer selection still resets to
  the first entry on reopening, but hidden detail is not rendered until selected.
  Hidden/non-selected tabs retain dirty state. Legacy identity-free events merge
  into one visible refresh; notification badges keep their independent ID-based
  updates. Customer frames and portrait properties change only when different.
- Initialization is idempotent, and UI-owned data subscriptions are released on
  destruction, including staff skin and customer visit/skin subscriptions.

The existing SDK-free Editor request bridge accepts `ui-refresh` through
`Temp/GachaReview20260917-2213/tests-request.txt`. It runs actual UI commands on
in-memory Stage1 PreviewScenes with isolated data/events, without saving user
scenes or verification Scene/Prefab assets. Results are written to
`%TEMP%\CustomerTableUseReview\related-ui-refresh-tests.json` (or
`PANDA_CUSTOMER_USE_EVIDENCE_DIR` when set). These are functional regressions,
not CPU/GC allocation or device benchmarks.

## Mailbox and challenge UI refresh

- Mailbox list events only mark the view dirty while hidden. While visible, events
  and receive/load callbacks merge into one LateUpdate bind of the current manager
  list. A pending list load defers binding until completion; older window-load
  completions cannot release a newer load. Manager load-state/completion signals
  also cover a newer alarm-poll load superseding the window's own request, without
  changing request, receive, deletion or persistence policy. Stable InDate sorting (including ties),
  slot order, count and read/delete visibility are retained without deactivating
  every pooled slot or searching the pool for each mail.
- Mail receive identity includes account, PostType and InDate, but display state
  also depends on the global receive operation, current query and mutable items.
  There is no complete per-mail presentation revision, so this change deliberately
  uses coalesced full binding rather than an identity-only incremental cache.
- Challenge state/add events maintain badges even while hidden. Only the selected
  visible tab binds slots; sorting buffers are reused, with unclaimed-complete,
  in-progress and claimed ordering and claimed Repeat exclusions unchanged.
  Percent events identify a ChallengeType, not a single ID: only visible slots of
  that type update their bar/count. Hidden tabs retain dirty state. Tabs own the
  subscriptions until destruction; inactive pooled slots have no subscriptions.
- Attendance polling is intentionally unchanged. Ad closed can precede rewarded,
  and the no-reward timeout clears the active ad session without another button
  event. AttendanceChanged also does not announce the noon game-day boundary or
  every save-availability change. Removing polling could miss unlock/date changes.

The same SDK-free Editor bridge accepts `ui-deferred-refresh` at the request path
above. It runs focused mailbox/challenge UI regressions and existing MailReceive
and Attendance tests with memory-only boundaries. Mailbox rendering tests simulate
load completions, never request live mail. Results are written to
`%TEMP%\CustomerTableUseReview\related-ui-deferred-refresh-tests.json`.
Neither these tests nor the source review replace PlayerLoop animation/ad callback
checks or CPU/GC profiling on a target device. No forced canvas/layout rebuild is
used.

## Party movement failure handling

- A two-person team's waiting-line path failure or completion short of the target
  cancels the whole waiting reservation without penalties or rewards. Cancellation
  returns both members, sorts the remaining queue and refreshes its save and guide
  availability. Both members must still arrive before the team's wait clock starts;
  single-customer timing and failure policy are unchanged.
- Unowned-menu rejection retains its one-time satisfaction penalty. Each exit is
  recorded as `ReachedExit` or `TechnicalCleanup`; a resolved member is detached
  before pool return without ending the partner's live table use. The table and
  seats are released once all members resolve, without meal settlement or rewards.
- Both paths validate the captured party/visit and movement sequence. Repeated
  callbacks and callbacks from old movements, pooled visits or table uses are ignored.

The existing `GachaReviewTestRunner` accepts `party-movement` through
`Temp/GachaReview20260917-2213/tests-request.txt` in an idle, SDK-free Editor.
It runs the movement regressions plus related party, single-customer and staff guide
tests using a disposable in-memory PreviewScene, without saving/replacing user
scenes or writing verification Scene/Prefab assets. Results are written to
`%TEMP%\CustomerTableUseReview\related-party-movement-tests.json` (or
`PANDA_CUSTOMER_USE_EVIDENCE_DIR` when set). These synchronous command/AStar/callback
checks do not replace real-time PlayerLoop or device playtesting.
 
