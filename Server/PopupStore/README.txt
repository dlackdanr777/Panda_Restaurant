DEV-SERVER-01C CURRENT STATUS (2026-11-09)
Function SDK build and deployed integration are BLOCKED / NOT RUN.
Read Function111Readiness.txt for exact local tool inventory and approved next steps.
PopupStoreSources.props links original sources; it is not an installed SDK or a build result.
DatabaseAccountStore now targets partitioned v2 records through the shared partition store.
The single-account JSON schema described in the historical notes below is superseded;
do not create that v1 table or use it as the current deployment schema.
Read DatabasePartitionDesign.txt and WalletAuthorityDecision.txt for current storage and
single-wallet decisions. No GameData writers, UI, assets, balance or live accounts were changed.
The default host and migration gates remain disabled, including account identity scope.

HISTORICAL DEV-SERVER-01/01B NOTES (retain audit context; superseded schema noted above)
DEV-SERVER-01 - disabled server source and SDK transport
Audit date: 2026-11-05. No test environment has been provided.

STATUS
This directory contains a host source template and a DatabaseAccountStore source
candidate, not an installed SDK project or a deployed function. Neither server
source has been built against the Function/Database SDK. There
is no .csproj, package-version guess, credential, deployment manifest, ZIP or
backup. No function is called by default and no existing UI is wired to it.

LOCAL FACTS (metadata/read-only inspection)
- Unity Backend.dll assembly/file/product versions: 5.15.0.0.
- SDK supports Backend.BFunc.InvokeFunction(string, Param) and its async overload
  with Backend.BackendCallback(BackendReturnObject).
- Backend.Utils.GetServerTime() and callback overload return UTC in utcTime.
- Backend.BMember.GetUserInfo() exists; GetUserInfoV2() is absent in this client
  version. UserInDate, PlayerInDate and UID exist; no GamerId property exists.
- One Backend settings asset is populated for ordinary SDK initialization;
  Function Auth Key is empty and the multi-project list is empty. The audit did
  not establish whether that existing project is development or production.
- No pre-existing Function project or database SDK package was found. backend
  is absent from PATH; dotnet --list-sdks reports only 7.0.400.

HOST AND WIRE CONTRACT
Use official Function 1.1.1, net8.0 and its bundled server SDK (Base 5.18.17).
The server namespace is BackEnd, not legacy BackendAPI. Client SDK 5.15 and
server SDK DLLs are different artifacts; never substitute the Unity DLL for the
server bundle. The exact NuGet package pins must come from the official generated
template. Current upgrade docs require an explicit System.Text.Json 9.x reference
and CopyLocalLockFileAssemblies=true, but do not establish its exact patch here.
Official generation/update commands may create ZIP/backup output. They were not
run because this task prohibits such artifacts.

Client sends a Param whose request field is a JSON object parsed from Command.
There is no JSON string within request. Command/Response field casing is PascalCase
and Protocol is popup-store.dev-server-01.v1. The SDK wraps the returned Response
object as {"result":{...}}. Transport ResponseJson contains only that inner object.
Installed Unity SDK 5.15 Param.Add(JsonData) throws on optional JSON null values.
The transport omits only the six known optional members when null in its detached
wire object. Absent optional members deserialize to the same null DTO fields on
the host. Non-null values, material order/duplicates, pending command and command
hash remain unchanged. Unknown fields are not stripped or accepted by this step.
SDK IsSuccess() is transport success, not evidence of business success.
The Function host reads Backend.Content["request"], which uses the server SDK's
BackEnd.BackndLitJson.JsonData. A JObject response is serialized once and returned
through Backend.StringToStream. Do not nest serialized JSON strings in responses.
The host accepts only exact Command field names and explicit JSON scalar/array
types, rejects unknown/case-alias/serializer-metadata fields, and disables runtime
type metadata. Duplicate fields are rejected in the normalized JObject. Because
Backend.Initialize parses the input before Content is exposed, duplicate handling
of the raw SDK envelope still needs a real runtime test; this source does not claim
to inspect raw duplicate names that the SDK may already have discarded.

The native entry point is BackendFunction.BFunc.Function(Stream, ILambdaContext).
It initializes the SDK once. If trusted development/store/clock dependencies are
missing, it returns the shared typed Response with Status.Disabled and Error without
calling the authority. SDK transport HTTP 200 may still wrap this response. The
host adds no undeclared ServiceStatus extension: strict clients reject unknown fields.
If configured, it obtains the principal ONLY from
Backend.BMember.GetUserInfoV2().GetReturnValuetoJSON()["row"]["gamerId"].
UserInDate is not interchangeable with gamerId. The Unity restore-query reference
fences callbacks only; it is never sent as the server principal. Bind the client
business account identity to the authenticated Access response within that epoch.

ResolveTrustedDependencies defaults to null. Trusted server startup may supply
IAtomicAccountStore, IServerClock and IServerOrderLifecycle only after deployment
evidence is complete. The lifecycle must authenticate arrival/delivery/departure
and slot release; client completion flags are not authority. SystemSecureRandom
supplies cryptographic randomness. No client input
can configure these dependencies. An exception after Execute may follow a commit;
the host returns an indeterminate envelope rather than claiming rejection.

CLIENT TRANSPORT GATES
PopupStoreBackendFunctionTransport is unavailable by default. Its explicit factory
requires the current BackendManager restore-query identity, an authenticated SDK
session, a captured main-thread SynchronizationContext and independently verified
development deployment. Configuration requires environment=development, a dev_
function name, the exact protocol and explicit development/function/atomic-wallet
verification flags. Release builds cannot send even if these flags are supplied.
Naming and flags do not themselves prove environment isolation: the host's runtime
environmentStillVerified delegate must remain tied to actual deployment evidence.
The transport does not initialize/login, implement retries, write balances or
mutate UI. Existing SDK initialization/retry settings are preserved; any SDK-level
retransmission must be safe through the same request ID and server atomic journal.
Timeout, SDK errors, thrown invocation and invalid/mismatched replies after sending
are Indeterminate. A missing read is not a fence against a later in-flight commit.
Late duplicate replies cannot complete twice. An invalid auth epoch suppresses the
callback, including an already queued reply. Preserve request identity for recovery;
the transport never converts session disposal into proof that a write did not occur.
The higher-level client retains a durable pending request even when this transport
attempt was unavailable or rejected before sending. Another client/process can
adopt and send the same identity concurrently, so this local result cannot prove
the shared operation was not applied. Only correlated server evidence clears the
journal; recovery and any explicit retry preserve the exact original command/ID.
Journal Prepare returns true only for a new atomic create, false for an identical
existing record; a concurrent Submit seeing false does not send. A client whose
authentication epoch has been invalidated stays invalidated and must be replaced.

DATABASE IMPLEMENTATION PLAN - STILL REQUIRED
The only supported candidate identified for atomic CAS is the NEW BACKND.Database
service used from Function 1.1.1. A static Client takes the verified development
database UUID. Its asynchronous APIs use From<T>(), OfCurrentUser(), Where(),
Insert(), Update()/Inc()/Dec(), and Transaction().Commit(). A concrete source
candidate is supplied below; its async integration with the synchronous authority
store interface must be compiled and tested against the actual server SDK.

Schema proposal: UserTable aggregate row keyed by authenticated gamerId with
Revision, stable migrated WalletId, WalletMigrationId, wallet balance/version, usage, frozen policy, business
state and request/grant receipts. All writers must compare Revision and update the
whole logical aggregate atomically. For a split schema, use primary-keyed account,
business, request-receipt and grant rows with predicates and inserts in ONE
transaction. Never split wallet and receipt across commits. A development store
must reject unsupported aggregate size instead of truncating history. Current DB
docs cap a string at 4KB, JSON at 12KB, and a transaction at 10 operations. Unbounded
business/request history therefore requires a reviewed partition/retention schema;
the in-memory aggregate is not a ready-to-deploy single-column schema.

Documented transaction rule: Update/Delete identify one row by primary-key ==;
additional comparisons are execution predicates. A missing row, predicate failure
or duplicate primary-key Insert cancels the whole transaction. Reads occur outside
the transaction. Preserve the read Revision in the commit predicate and retry from
a fresh authoritative read on a verified contention failure. Do not retry unknown
write outcomes blindly. Commit returns TransactionResult; inspect Success/Error.
One transaction cannot operate on the same row twice. Comparison values should be
constants or locals, not nested object properties; string comparisons use ==/!=.

Illustrative SDK API shape (not compiled deployment code):
  await db.Transaction()
    .From<AccountRow>().OfCurrentUser()
      .Where(x => x.Id == trustedGamerId && x.Revision == expectedRevision)
      .Update(nextAccount)
    .From<RequestReceipt>().OfCurrentUser().Insert(receipt)
    .Commit();
trustedGamerId comes from SDK authentication; expectedRevision comes from a prior
server read; nextAccount and receipt come from authority output, never client DTOs.
Duplicate request ID + same immutable intent replays the stored response; conflicting
intent must not overwrite an existing receipt. A business-level grant key prevents
new request IDs from creating a second entitlement for the same business.

TABLE RIGHTS AND WALLET MIGRATION GATE
Create the database/tables in the verified development project first. User tables
must use OfCurrentUser() in every query. Remove all client write permissions;
permit SELF read only where needed. Deployed Function 1.1.1 bypasses client table
permissions, but user tables still target only the caller. Flexible tables have
no owner isolation, so server code must authenticate/filter explicitly and their
client permissions should remain empty. Updating model attributes does not change
existing console permissions. Local debug --user still enforces client permissions;
it cannot validate server-only writes. Such validation needs a verified development
deployment, which is currently absent.

NEW Database transactions cannot be assumed to include legacy GameData. No official
cross-store atomic API/guarantee was found. Backend.GameData.TransactionWriteV2 is a
separate API. Official support states GameData SetUpdate Where conditions are checked
when finding rows, not again when writing; version-based concurrency is not
guaranteed. Therefore DB receipt + legacy Dia write is NOT a conforming atomic store.
Move or fully fence every wallet writer (autosave, gacha, purchases, rewards, mail,
ads, etc.) under the same authoritative DB transaction regime before setting
FullWalletMigrationVerified. A popup-only migration flag is insufficient.

VERIFICATION STILL NEEDED BEFORE ANY CALL
- Separate development project/function/database and explicit target verification.
- Official net8 Function 1.1.1 template/DLL package, dependency versions and compile.
- Backend settings Function Auth Key configured without logging credentials.
- Tables, column limits, primary keys, write permissions, and atomic store adapter.
- Full wallet migration/fencing, server gameplay clock policy and protocol evidence.
- Development-only tests for concurrent same-request and same-business different-ID
  claims, CAS conflicts, response loss, delayed commit, reconnect and stale sessions.
No authenticated server calls, console changes, deployments or account writes were
performed for the current implementation.

CONCRETE DATABASE SOURCE CANDIDATE
DatabaseAccountStore.cs uses only documented SDK query/model/transaction methods:
From<T>().OfCurrentUser().Where(...).FirstOrDefault() and
Transaction().From<T>().OfCurrentUser().Where(...).Update(nextRow).Commit().
It is not compiled, called, deployed or enabled. The schema is one pre-provisioned
UserTable row per authenticated gamerId, with Revision, MigrationId and a property-
backed JSON payload containing the complete AccountState, wallet and request journal.
The host can create one store per SDK-authenticated principal through
CreateStoreForCaller; the underlying Client may be reused. All verification flags
default false. The expected migration ID must also match the stored aggregate.
The aggregate must contain a nonblank migrated WalletId; mutations cannot change it.
There is no CreateTable, Insert, upsert, account bootstrap or migration API in it.
A missing account remains missing; no claim can create its initial wallet.

The commit predicate includes primary key, expected Revision and MigrationId; the
whole new payload and incremented Revision are one Update inside one transaction.
Request/business/grant uniqueness lives within the CAS-protected aggregate. The
table primary key also prevents duplicate account rows; automatic duplicate-key
Insert is unnecessary because this store never provisions rows.

It retries at most three observed Revision conflicts BEFORE submitting a write.
This preflight is only an optimization; the commit predicate is the concurrency
guarantee. After Commit, every exception, Success=false or unexpected affected-row
count is indeterminate and propagates to the host, which preserves query recovery.
Published TransactionResult exposes Success and an Error string, but no verified
typed discriminator separating CAS conflicts from unknown commit outcomes. The
source deliberately does not invent such an SDK method, inspect error wording, or
retry an ambiguous write. Post-commit conflict retry is a remaining integration gap
until an official exact error contract and development evidence are available.

AccountState's public fields are serialized explicitly by Newtonsoft.Json into a
property-backed document wrapper to avoid assuming Database serializer IncludeFields
settings. This round trip must be verified against the actual SDK/console schema.
A conservative escaped-JSON bound rejects payloads above 11,000 bytes and never
prunes receipts. The current journal contains full snapshots and can exceed this
limit during one business. A reviewed partition/retention design remains a live
activation blocker; this candidate does not claim full-session storage capacity.

The API implementation is async; the existing synchronous IAtomicAccountStore
bridge uses GetAwaiter().GetResult() only with no SynchronizationContext. A real
Function SDK compile and Lambda-context/deadline test are still required, or the
authority/host interfaces must be changed to async together. No Unity thread may
use this server-only store. No exact NuGet pins or fake SDK stubs are supplied.

PRIMARY SOURCES
Function 1.1.1 and server requirements:
https://docs.backnd.com/sdk-docs/function/intro/
https://docs.backnd.com/sdk-docs/function/template/
https://docs.backnd.com/sdk-docs/function/backnd-features/call-features/
https://docs.backnd.com/sdk-docs/function/upgrade/
Caller identity and response format:
https://docs.backnd.com/sdk-docs/function/request-response/caller/
https://docs.backnd.com/en/sdk-docs/function/request-response/return-result/
https://docs.backnd.com/sdk-docs/backend/base/function/execute-function/
Database transaction, rights, modeling, predicates:
https://docs.backnd.com/sdk-docs/function/backnd-features/database/
https://docs.backnd.com/sdk-docs/database/transaction/
https://docs.backnd.com/sdk-docs/database/data-modeling/
https://docs.backnd.com/sdk-docs/database/supported-operators/
GameData limitation (official staff response dated 2026-10-14; its older statement
that Function cannot access Database was superseded by Function 1.1.1 on 2026-11-03,
but that does not turn the separate legacy GameData API into a CAS transaction):
https://community.thebackend.io/t/sdk/12074
