using System;
using System.Collections.Generic;

namespace MarsOffice.Qeeps.Notifications.Abstractions
{
    public class RequestNotificationDto
    {
        public IEnumerable<RecipientDto> Recipients { get; set; }
        public string PreferredLanguage { get; set; }
        public string TemplateName { get; set; }
        public string AbsoluteRouteUrl { get; set; }
        public Dictionary<string, string> PlaceholderData { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
        public IEnumerable<NotificationType> NotificationTypes { get; set; }
        public Severity Severity { get; set; }
    }
}
