using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Nhom1.Hubs
{
    // Simple SignalR hub for relaying call signaling messages between clients.
    // Clients should join a group using their userId so the server can target messages.
    [Authorize]
    public class CallHub : Hub
    {
        private readonly ILogger<CallHub> _logger;

        public CallHub(ILogger<CallHub> logger)
        {
            _logger = logger;
        }
        public override Task OnConnectedAsync()
        {
            // Optionally log connection
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
            // Optionally cleanup
            return base.OnDisconnectedAsync(exception);
        }

        // Join a group associated with the user's id so other clients can send messages to this user
        public Task JoinUserGroup(string userId)
        {
            var group = GetUserGroup(userId);
            _logger.LogInformation("JoinUserGroup: connection={ConnectionId} userId={UserId} group={Group}", Context.ConnectionId, userId, group);
            return Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        public Task LeaveUserGroup(string userId)
        {
            var group = GetUserGroup(userId);
            _logger.LogInformation("LeaveUserGroup: connection={ConnectionId} userId={UserId} group={Group}", Context.ConnectionId, userId, group);
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        // Notify recipient about an incoming call
        public Task NotifyIncomingCall(string recipientUserId, string callId, string callerUserId, string callType)
        {
            var group = GetUserGroup(recipientUserId);
            _logger.LogInformation("NotifyIncomingCall: from={Caller} toGroup={Group} callId={CallId} type={CallType}", callerUserId, group, callId, callType);
            return Clients.Group(group)
                .SendCoreAsync("IncomingCall", new object[] { new { callId, callerUserId, callType } }, default);
        }

        // Relay SDP offer/answer and ICE candidates
        public Task SendOffer(string recipientUserId, string callId, object offer)
        {
            var group = GetUserGroup(recipientUserId);
            _logger.LogInformation("SendOffer: fromConn={Conn} toGroup={Group} callId={CallId} offerType={OfferType}", Context.ConnectionId, group, callId, offer?.GetType().Name ?? "null");
            return Clients.Group(group)
                .SendCoreAsync("ReceiveOffer", new object[] { new { callId, offer } }, default);
        }

        public Task SendAnswer(string recipientUserId, string callId, object answer)
        {
            var group = GetUserGroup(recipientUserId);
            _logger.LogInformation("SendAnswer: fromConn={Conn} toGroup={Group} callId={CallId} answerType={AnswerType}", Context.ConnectionId, group, callId, answer?.GetType().Name ?? "null");
            return Clients.Group(group)
                .SendCoreAsync("ReceiveAnswer", new object[] { new { callId, answer } }, default);
        }

        public Task SendIce(string recipientUserId, string callId, object candidate)
        {
            var group = GetUserGroup(recipientUserId);
            _logger.LogInformation("SendIce: fromConn={Conn} toGroup={Group} callId={CallId} candidateType={CandType}", Context.ConnectionId, group, callId, candidate?.GetType().Name ?? "null");
            return Clients.Group(group)
                .SendCoreAsync("ReceiveIce", new object[] { new { callId, candidate } }, default);
        }

        private static string GetUserGroup(string userId) => $"user-{userId}";
    }
}
