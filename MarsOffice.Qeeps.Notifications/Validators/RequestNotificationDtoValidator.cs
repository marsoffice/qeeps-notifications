using FluentValidation;
using MarsOffice.Qeeps.Notifications.Abstractions;

namespace MarsOffice.Qeeps.Notifications.Validators
{
    public class RequestNotificationDtoValidator : AbstractValidator<RequestNotificationDto>
    {
        public RequestNotificationDtoValidator()
        {
            RuleFor(x => x.NotificationTypes).NotNull().WithMessage("notifications.processNotification.notificationTypesRequired")
                .NotEmpty().WithMessage("notifications.processNotification.notificationTypesRequired");
            RuleFor(x => x.Recipients).NotNull().WithMessage("notifications.processNotification.recipientsRequired")
                .NotEmpty().WithMessage("notifications.processNotification.recipientsRequired");
            RuleFor(x => x.TemplateName).NotEmpty().WithMessage("notifications.processNotification.templateRequired");
        }
    }
}