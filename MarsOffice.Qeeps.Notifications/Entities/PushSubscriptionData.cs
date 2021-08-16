namespace MarsOffice.Qeeps.Notifications.Entities
{
    public class PushSubscriptionData
    {
        public string Endpoint { get; set; }
        public long? ExpirationTime { get; set; }
        public string P256Dh { get; set; }
        public string Auth { get; set; }
    }
}