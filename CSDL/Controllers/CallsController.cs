using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CallsController : ControllerBase
    {
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<Nhom1.Hubs.CallHub> _hubContext;
        private readonly Microsoft.Extensions.Logging.ILogger<CallsController> _logger;

        public CallsController(Microsoft.AspNetCore.SignalR.IHubContext<Nhom1.Hubs.CallHub> hubContext, Microsoft.Extensions.Logging.ILogger<CallsController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }
        public class InitiateCallDto
        {
            public string? RecipientId { get; set; }
            public string? CallType { get; set; }
        }

        public class CallActionDto
        {
            public string? CallerId { get; set; }
        }

    [HttpPost("initiate")]
    public async System.Threading.Tasks.Task<IActionResult> Initiate([FromBody] InitiateCallDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.RecipientId))
                return BadRequest(new { success = false, message = "Invalid data" });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Unauthorized" });

            // Generate a simple callId. In a real implementation you'd persist call state
            var callId = $"call-{Guid.NewGuid():N}";

            // Notify the recipient via SignalR so their client can show incoming call UI
            try
            {
                var groupName = $"user-{dto.RecipientId}";
                _logger.LogInformation("[CallsController] Initiate call: callId={CallId} caller={Caller} recipient={Recipient} group={Group} callType={CallType}", callId, userId, dto.RecipientId, groupName, dto.CallType);
                await _hubContext.Clients.Group(groupName).SendCoreAsync("IncomingCall", new object[] { new { callId, callerUserId = userId, callType = dto.CallType } }, default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CallsController] Failed to notify IncomingCall for callId={CallId} to recipient={Recipient}", callId, dto.RecipientId);
            }

            // Return callId to client so it can open CallScreen
            return Ok(new { callId });
        }

        [HttpPost("{callId}/accept")]
        public async System.Threading.Tasks.Task<IActionResult> Accept(string callId, [FromBody] CallActionDto dto)
        {
            if (string.IsNullOrEmpty(callId))
                return BadRequest(new { success = false, message = "Invalid callId" });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Unauthorized" });

            // Notify the caller (if provided) that this call was accepted
            try
            {
                if (dto != null && !string.IsNullOrEmpty(dto.CallerId))
                {
                    var callerGroup = $"user-{dto.CallerId}";
                    _logger.LogInformation("[CallsController] Accept: callId={CallId} callee={Callee} caller={Caller} group={Group}", callId, userId, dto.CallerId, callerGroup);
                    await _hubContext.Clients.Group(callerGroup).SendCoreAsync("CallAccepted", new object[] { new { callId, calleeUserId = userId } }, default);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CallsController] Failed to notify CallAccepted for callId={CallId} to caller={Caller}", callId, dto?.CallerId);
            }

            return Ok(new { success = true, callId });
        }

        [HttpPost("{callId}/reject")]
        public async System.Threading.Tasks.Task<IActionResult> Reject(string callId, [FromBody] CallActionDto dto)
        {
            if (string.IsNullOrEmpty(callId))
                return BadRequest(new { success = false, message = "Invalid callId" });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                if (dto != null && !string.IsNullOrEmpty(dto.CallerId))
                {
                    var callerGroup = $"user-{dto.CallerId}";
                    _logger.LogInformation("[CallsController] Reject: callId={CallId} callee={Callee} caller={Caller} group={Group}", callId, userId, dto.CallerId, callerGroup);
                    await _hubContext.Clients.Group(callerGroup).SendCoreAsync("CallRejected", new object[] { new { callId, calleeUserId = userId } }, default);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CallsController] Failed to notify CallRejected for callId={CallId} to caller={Caller}", callId, dto?.CallerId);
            }

            return Ok(new { success = true, callId });
        }

        [HttpPost("{callId}/end")]
        public async System.Threading.Tasks.Task<IActionResult> End(string callId, [FromBody] CallActionDto dto)
        {
            if (string.IsNullOrEmpty(callId))
                return BadRequest(new { success = false, message = "Invalid callId" });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                if (dto != null && !string.IsNullOrEmpty(dto.CallerId))
                {
                    var callerGroup = $"user-{dto.CallerId}";
                    _logger.LogInformation("[CallsController] End: callId={CallId} callee={Callee} caller={Caller} group={Group}", callId, userId, dto.CallerId, callerGroup);
                    await _hubContext.Clients.Group(callerGroup).SendCoreAsync("CallEnded", new object[] { new { callId, calleeUserId = userId } }, default);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CallsController] Failed to notify CallEnded for callId={CallId} to caller={Caller}", callId, dto?.CallerId);
            }

            return Ok(new { success = true, callId });
        }
    }
}
