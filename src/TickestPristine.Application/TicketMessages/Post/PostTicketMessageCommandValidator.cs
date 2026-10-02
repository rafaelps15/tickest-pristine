using FluentValidation;

namespace TickestPristine.Application.TicketMessages.Post;

public class PostTicketMessageCommandValidator : AbstractValidator<PostTicketMessageCommand>
{
    public PostTicketMessageCommandValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.Content).NotEmpty().MaximumLength(4000);
    }
}
