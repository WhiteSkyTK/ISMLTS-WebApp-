using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // The same notifications as the website's bell
    public class NotificationsApiController : StudentApiController
    {
        private readonly INotificationRepository _notifications;

        public NotificationsApiController(INotificationRepository notifications)
        {
            _notifications = notifications;
        }

        [HttpGet("notifications")]
        [ProducesResponseType<NotificationPageDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(bool unreadOnly = false, int page = 1)
        {
            var list = await _notifications.GetPageAsync(Roles.Student, StudentId, unreadOnly, page);
            var unread = await _notifications.CountUnreadAsync(Roles.Student, StudentId);
            return Ok(new NotificationPageDto(list.Items.Select(ApiMap.Notification).ToList(), unread, list.Page, list.TotalPages));
        }

        [HttpPost("notifications/{id:int}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Read(int id)
        {
            if (await _notifications.GetForUserAsync(Roles.Student, StudentId, id) == null)
                return Error(StatusCodes.Status404NotFound, "not_found", "That notification doesn't exist.");
            await _notifications.MarkReadAsync(Roles.Student, StudentId, new[] { id });
            return NoContent();
        }

        [HttpPost("notifications/read-all")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ReadAll()
        {
            await _notifications.MarkReadAsync(Roles.Student, StudentId, null);
            return NoContent();
        }
    }
}
