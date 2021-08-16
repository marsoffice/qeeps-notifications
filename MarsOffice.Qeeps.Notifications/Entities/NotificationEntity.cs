using System;
using Newtonsoft.Json;

namespace MarsOffice.Qeeps.Notifications.Entities
{
    public class NotificationEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Message { get; set; }
        public string Title { get; set; }
        public string AbsoluteRouteUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? ReadDate { get; set; }
    }
}