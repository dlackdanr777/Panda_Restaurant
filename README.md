# Panda_Restaurant

## Android APK: kitchen duplicate textures

Starting from main `ab2243095e814aa896deddb9eb00503949f47b24`, the original
Unity 6000.0.16f1 Editor session produced these signed Android Release clean APKs.
Both preserve ARMv7, ARM64 and the previous `CompressWithLz4` build option.
Sizes below are actual APK file lengths, not unpacked BuildReport totals.

| Stage | Asset files changed | APK bytes | Reduction from preceding build | Bytes above 200,000,000 |
| --- | ---: | ---: | ---: | ---: |
| Unchanged main | 0 | 294,520,506 | - | 94,520,506 |
| Kitchen duplicates removed | 680 | 275,374,330 | 19,146,176 | 75,374,330 |
| Shelf resolution trial: ineligible, skipped | 0 | No additional APK | - | 75,374,330 |

- Removed 340 unused category-folder PNGs and their 340 metas under
  `Assets/Resources/KitchenUtensilData/Sprites`. Each decoded RGBA image and its
  dimensions matched its numbered-folder counterpart exactly; mirrored artwork
  was not treated as identical. The deleted imports were Default textures with
  no Sprite subassets, not interchangeable imported Sprites.
- Each candidate's pivot, PPU, slicing, border and Android settings were recorded.
  Import settings differed, but no GUID/fileID, asset dependency, explicit
  Resources path or atlas packable referenced a deleted texture. The binary
  dependency check covered 5,293 assets; text/meta checks covered 6,842 files.
  No reference rewrites were necessary. The numbered-folder Sprite GUIDs,
  fileIDs, names, source PNGs and import settings remain unchanged.
- Production `KitchenUtensilDataManager` still loads numbered folders 01..48
  and derives keys from `sprite.name`. The unique category-folder `SINK00` and
  `COOKER00` Default textures were preserved because they had no exact duplicate.
- All 26 application shelves import at 2048x937 with Android Max Size 2048.
  The original Stage1 camera and three floor placements project their full image
  at about 1536x703 pixels at 1920x1080, 2048x937 at 2560x1440, and 3072x1405 at
  the current 3840x2160 Game View. None is oversized at the largest measured
  display size, so no Android Max Size was lowered and no resolution-trial APK
  was built. None is atlas-packed. Shop list/preview use separate thumbnails;
  those settings were not changed. Refrigerators, furniture and UI resolution
  settings were not changed.

Run **Tools > Panda Restaurant > Kitchen Assets > Run Sprite Regression Checks**
in SDK-free Edit Mode. The two NUnit checks exercise the retained 340 Sprites,
production CSV parsing, actual shop portrait/preview methods and all 26 shelves
on three in-memory Stage1 floor placements. Results go to
`Logs/KitchenApkOptimization/related-kitchen-apk-tests.json` and the matching
NUnit XML. The existing request bridge also accepts `kitchen-apk`. No user Scene
is saved/replaced and no account data, ads or payments are accessed.

Device image quality and animated floor transitions still require manual
acceptance. The configured first build Scene is `FirstLoadingScene`; after the
normal game entry, use the shop button (`UIShop Button`), **주방**, then
**주방 선반**. Check list portraits, selected preview and existing shelf placement
on floors 1, 2 and 3, including switching floors and returning to the list.
Compare missing images, anchors, scale and thin outlines against the baseline
APK at the same device resolution. Do not purchase or change account equipment
solely for these checks. The APK remains above the 200,000,000-byte target.

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

## PERF-MAIN-02: customer unlock events

Customer unlock checks depend only on score, required recipe and required item.
Coin changes no longer trigger the full customer scan. The direct check after
catalog initialization and the score/recipe/item subscriptions are retained.
`UserInfo.TryLoadGameData` restores ownership and enabled-customer records before
publishing recipe/item events, so new and existing account restores still check
eligibility without relying on the money event. Unlock rules, visit counts,
encounters, single/two-person spawning and pictorial book behavior are unchanged.

The existing SDK-free Editor bridge accepts `customer-unlock` through
`Temp/GachaReview20260917-2213/tests-request.txt`. It runs focused production
initialization, coin callback count, score/recipe/item and memory account-restore
tests, plus existing natural-party, single-customer, encounter and customer book
regressions. Results are written to
`%TEMP%\CustomerTableUseReview\related-customer-unlock-tests.json` (or
`PANDA_CUSTOMER_USE_EVIDENCE_DIR` when set), with the matching NUnit XML.
The existing blank-ID diagnostic at the end of each customer CSV is explicitly
expected by the initialization fixture; the parser and CSVs are not changed.
No user Scene is saved/replaced, no verification Scene/Prefab assets are created,
and no real login, server save, ads or payments are executed. These tests count
the real coin-event unlock callback but do not measure Main Thread time, GC
allocation or Android performance.
 
