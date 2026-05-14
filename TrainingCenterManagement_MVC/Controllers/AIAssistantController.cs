using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Services;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AIAssistantController : ControllerBase
    {
        private readonly IAIAssistantService             _ai;
        private readonly ILogger<AIAssistantController> _logger;

        public AIAssistantController(IAIAssistantService ai, ILogger<AIAssistantController> logger)
        {
            _ai     = ai;
            _logger = logger;
        }

        // POST /api/aiassistant/ask
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var ip        = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua        = HttpContext.Request.Headers.UserAgent.ToString();

            try
            {
                var msg = await _ai.AskQuestionAsync(userId, request.Question, ip, ua);
                return Ok(new
                {
                    success   = true,
                    messageId = msg.MessageId,
                    response  = msg.AIResponse,
                    type      = msg.QuestionType.ToString(),
                    provider  = msg.DataAccessLog,
                    timestamp = msg.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI ask failed for user {UserId}", userId);
                return Ok(new { success = false, response = "عذراً، حدث خطأ. يرجى المحاولة مجدداً." });
            }
        }

        // GET /api/aiassistant/history?pageNumber=1&pageSize=20
        [HttpGet("history")]
        public async Task<IActionResult> History(int pageNumber = 1, int pageSize = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var msgs   = await _ai.GetChatHistoryAsync(userId, pageNumber, pageSize);
            return Ok(new
            {
                success  = true,
                count    = msgs.Count,
                messages = msgs.Select(m => new
                {
                    m.MessageId,
                    m.UserMessage,
                    m.AIResponse,
                    m.QuestionType,
                    m.Rating,
                    m.CreatedAt
                })
            });
        }

        // POST /api/aiassistant/rate/{messageId}
        [HttpPost("rate/{messageId:guid}")]
        public async Task<IActionResult> Rate(Guid messageId, [FromBody] RateRequest request)
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var success = await _ai.RateResponseAsync(messageId, userId, request.Rating, request.Feedback);
            return success
                ? Ok(new { success = true,  message = "تم تسجيل تقييمك، شكراً!" })
                : BadRequest(new { success = false, message = "التقييم غير صالح أو الرسالة غير موجودة." });
        }

        // GET /api/aiassistant/statistics
        [HttpGet("statistics")]
        public async Task<IActionResult> Statistics()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var stats  = await _ai.GetStatisticsAsync(userId);
            return Ok(new { success = true, statistics = stats });
        }

        // DELETE /api/aiassistant/{messageId}
        [HttpDelete("{messageId:guid}")]
        public async Task<IActionResult> Delete(Guid messageId)
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var success = await _ai.DeleteMessageAsync(messageId, userId);
            return success
                ? Ok(new { success = true })
                : NotFound(new { success = false });
        }
    }

    public class AskRequest
    {
        [Required, MaxLength(2000)]
        public string Question { get; set; } = string.Empty;
    }

    public class RateRequest
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(500)]
        public string? Feedback { get; set; }
    }
}
