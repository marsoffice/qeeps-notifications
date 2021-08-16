using System.Collections.Generic;

namespace MarsOffice.Qeeps.Notifications.Abstractions
{
    public class NotificationsDto
    {
        public int Total {get;set;}
        public int Unread {get;set;}
        public IEnumerable<NotificationDto> Notifications {get;set;}
    }
}