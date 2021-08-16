using Newtonsoft.Json;

namespace MarsOffice.Qeeps.Notifications.Entities
{
    public class PushSubscriptionEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string UserId { get; set; }
        public string SubscriptionJson { get; set; }
    }
}