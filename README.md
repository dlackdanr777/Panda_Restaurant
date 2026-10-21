# Panda_Restaurant

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
 
