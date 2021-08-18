using System;
using System.Collections.Generic;

namespace MarsOffice.Qeeps.Notifications.Abstractions
{
    public class NotificationDto
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Message { get; set; }
        public string Title { get; set; }
        public string AbsoluteRouteUrl { get; set; }
        public bool IsRead { get; set; }
        public Severity Severity { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ReadDate { get; set; }
    }
}
