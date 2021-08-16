using System;
using System.Collections.Generic;
using MarsOffice.Qeeps.Notifications.Abstractions;

namespace MarsOffice.Qeeps.Notifications.Entities
{
public class WebPushNotification
    {
        public WebPushInnerNotification Notification { get; set; }
    }

    public class WebPushInnerNotification
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public WebPushData Data { get; set; }
        public IEnumerable<int> Vibrate { get; set; }
        public string Icon { get; set; }
        public string Badge { get; set; }
        public string Lang { get; set; }
        public bool Renotify { get; set; }
        public bool RequireInteraction { get; set; }
        public long Timestamp { get; set; }
        public long Tag { get; set; }
    }

    public class WebPushData
    {
        public string Id { get; set; }
        public string AbsoluteRouteUrl { get; set; }
        public Severity Severity { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}