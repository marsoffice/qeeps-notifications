using AutoMapper;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;

namespace MarsOffice.Qeeps.Notifications.Mappers
{
    public class NotificationMapper : Profile
    {
        public NotificationMapper()
        {
            CreateMap<NotificationEntity, NotificationDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}
