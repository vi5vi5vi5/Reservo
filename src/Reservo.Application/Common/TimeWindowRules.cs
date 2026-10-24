using FluentValidation;

namespace Reservo.Application.Common;

/// <summary>Запрос с временным окном «с — по» (поиск свободных ресурсов, расписание).</summary>
public interface ITimeWindowQuery
{
    DateTimeOffset? From { get; }

    DateTimeOffset? To { get; }
}

/// <summary>Общие правила для окна времени: оба края заданы, конец позже начала, окно не длиннее 31 дня.</summary>
public sealed class TimeWindowValidator : AbstractValidator<ITimeWindowQuery>
{
    public const int MaxDays = 31;

    public TimeWindowValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("Укажите начало интервала (from).");
        RuleFor(x => x.To).NotNull().WithMessage("Укажите конец интервала (to).");

        RuleFor(x => x.To)
            .Must((query, to) => to > query.From).WithMessage("Конец интервала должен быть позже начала.")
            .Must((query, to) => to - query.From <= TimeSpan.FromDays(MaxDays))
            .WithMessage($"Интервал не длиннее {MaxDays} дней.")
            .When(x => x.From is not null && x.To is not null);
    }
}
