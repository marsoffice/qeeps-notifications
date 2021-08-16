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
            RuleFor(x => x.RecipientUserIds).NotNull().WithMessage("notifications.processNotification.recipientUserIdsRequired")
                .NotEmpty().WithMessage("notifications.processNotification.recipientUserIdsRequired");
            RuleFor(x => x.TemplateName).NotEmpty().WithMessage("notifications.processNotification.templateRequired");
        }
    }
}