namespace MarsOffice.Qeeps.Notifications.Abstractions
{
    public class PushSubscriptionDto
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string SubscriptionJson { get; set; }
    }
}