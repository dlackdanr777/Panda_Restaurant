using System;
using System.Security.Cryptography;
using System.Text;
using PandaRestaurant.PopupStoreServer;

namespace Muks.BackEnd
{
    /// <summary>Explicit adapter for the existing admission/reward transaction interfaces. No
    /// default binding, Player mode switch, scene, account initialization or local Dia mutation.
    /// Session.AccountId MUST be the authenticated Function's canonical gamerId, bound by a host
    /// to the captured SDK authentication epoch; UserInDate/UID are not silently substituted.</summary>
    public sealed class PopupStoreBackendPersistence : IPopupStoreBusinessStartPersistence,
        IPopupStoreBusinessAccessInformation, IPopupStoreRewardPersistence
    {
        private readonly PopupStoreServerClient _client;
        private readonly PopupStoreServerClaimCoordinator _claims;
        public PopupStoreBackendPersistence(PopupStoreServerClient client, PopupStoreServerClaimCoordinator claims = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (claims != null && !ReferenceEquals(claims.Client, client))
                throw new ArgumentException("The claim coordinator must own the same authenticated client.", nameof(claims));
            // No migrated wallet is installed in the current project. The default coordinator
            // therefore rejects payout before transport, even if an admission adapter is supplied.
            _claims = claims ?? new PopupStoreServerClaimCoordinator(client);
        }

        public void QueryAccess(PopupStoreRewardAccountSession session, Action<PopupStoreBusinessAccessSnapshot> completed)
        {
            if (!Same(session)) { completed(PopupStoreBusinessAccessSnapshot.Failure(session, "로그인 세션을 확인해 주세요.")); return; }
            if (!_claims.IsWalletReadyForAdmission)
            { completed(PopupStoreBusinessAccessSnapshot.Failure(session, PopupStoreServerWalletGuard.UnavailableReason)); return; }
            bool delivered = false;
            if (!_client.QueryAccess(reply =>
            {
                delivered = true;
                var access = reply.Response?.Access;
                if (reply.Outcome != PopupStoreServerClientOutcome.Confirmed || reply.Response.Status != Status.Ok
                    || access == null || !access.WritesEnabled || access.AccountId != session.AccountId
                    || !_claims.IsWalletReadyForAdmission)
                { completed(PopupStoreBusinessAccessSnapshot.Failure(session, "검증된 서버 영업권 서비스를 사용할 수 없습니다.")); return; }
                completed(new PopupStoreBusinessAccessSnapshot(session, access.GameDay, access.PolicyVersion,
                    access.DailyLimit, access.Used, new DateTimeOffset(access.ServerUtcTicks, TimeSpan.Zero)));
            }) && !delivered) completed(PopupStoreBusinessAccessSnapshot.Failure(session, "기존 요청을 먼저 확인해 주세요."));
        }

        public void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed)
        {
            if (!Same(session) || request == null || !request.TryValidate(out _) || request.AccountId != session.AccountId
                || request.Mode != PopupStoreBusinessMode.Rewarded || !_claims.IsWalletReadyForAdmission)
            { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: "Invalid authenticated start.")); return; }
            bool delivered = false;
            bool sent = _client.Submit(new Command { Operation = Operation.Start, RequestId = request.StartRequestId,
                ExpectedPolicyVersion = request.PolicyVersion }, reply => { delivered = true; completed(StartResponse(request, reply)); });
            if (!sent && !delivered) completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate, error: "Recover the pending start before submitting."));
        }

        public void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed)
        {
            if (!Same(session) || request == null || !request.TryValidate(out _)
                || request.AccountId != session.AccountId || request.Mode != PopupStoreBusinessMode.Rewarded)
            { completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: "Invalid authenticated start recovery.")); return; }
            bool delivered = false;
            Action<PopupStoreServerClientResult> callback = reply => { delivered = true; completed(StartResponse(request, reply)); };
            bool sent = _client.HasPendingRequest ? _client.Recover(callback) : _client.QueryRecordedRequest(request?.StartRequestId, callback);
            if (!sent && !delivered)
                completed(new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate, error: "Recovery pending."));
        }

        public void Submit(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
            Action<PopupStoreRewardResponse> completed)
        {
            if (!Same(session) || request == null || !request.TryValidate(out _) || request.AccountId != session.AccountId)
            { completed(new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.RejectedBeforeSend, error: "Invalid authenticated reward.")); return; }
            bool delivered = false;
            bool sent = _claims.Claim(session, request, reply => { delivered = true; completed(reply); });
            if (!sent && !delivered) completed(new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate, error: "Recover the pending claim and latest wallet before submitting."));
        }

        public void Query(PopupStoreRewardAccountSession session, PopupStoreRewardRequest request,
            Action<PopupStoreRewardResponse> completed)
        {
            if (!Same(session)) { completed(new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate, error: "Session changed.")); return; }
            bool delivered = false;
            bool sent = _claims.Recover(session, request, reply => { delivered = true; completed(reply); });
            if (!sent && !delivered)
                completed(new PopupStoreRewardResponse(request, PopupStoreRewardResponseKind.Indeterminate, error: "Recovery pending."));
        }

        private static PopupStoreBusinessStartResponse StartResponse(PopupStoreBusinessStartRequest request, PopupStoreServerClientResult reply)
        {
            if (reply.Outcome == PopupStoreServerClientOutcome.Unavailable)
                return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.RejectedBeforeSend, error: reply.Error);
            if (reply.Outcome != PopupStoreServerClientOutcome.Confirmed || reply.Response == null)
                return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Indeterminate, error: reply.Error);
            var response = reply.Response; var business = response.Business;
            if (response.Status == Status.Ok && business != null && request != null && business.StartRequestId == request.StartRequestId)
            {
                var record = new PopupStoreBusinessStartRecord(request, business.BusinessSessionId, business.StartGameDay,
                    new DateTimeOffset(business.StartedAtUtcTicks, TimeSpan.Zero), 1);
                if (business.AccountId == request.AccountId && business.PolicyVersion == request.PolicyVersion && record.Matches(request))
                    return new PopupStoreBusinessStartResponse(request, PopupStoreBusinessStartResponseKind.Started, record);
            }
            return new PopupStoreBusinessStartResponse(request, response.Status == Status.Conflict || response.Status == Status.Rejected
                ? PopupStoreBusinessStartResponseKind.Conflict : PopupStoreBusinessStartResponseKind.Indeterminate, error: "Authoritative start recovery required.");
        }

        private bool Same(PopupStoreRewardAccountSession session) => session != null
            && ReferenceEquals(session, _client.Session) && _client.IsCurrentSession;
        internal static string ClaimRequestId(string grantKey)
        {
            if (string.IsNullOrWhiteSpace(grantKey)) throw new ArgumentException("Canonical grant key required.");
            using (var sha = SHA256.Create()) return "claim-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(grantKey))).Replace("-", "").ToLowerInvariant();
        }
    }
}
