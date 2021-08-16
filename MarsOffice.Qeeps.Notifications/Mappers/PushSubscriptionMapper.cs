using AutoMapper;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;

namespace MarsOffice.Qeeps.Notifications.Mappers
{
    public class PushSubscriptionMapper : Profile
    {
        public PushSubscriptionMapper()
        {
            CreateMap<PushSubscriptionEntity, PushSubscriptionDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}
