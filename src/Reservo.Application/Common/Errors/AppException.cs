namespace Reservo.Application.Common.Errors;

/// <summary>
/// Ожидаемая бизнес-ошибка, например «email уже занят» или «слот занят».
/// <see cref="Code"/> машиночитаемый — для фронтенда, сообщение и подробности — для людей.
/// </summary>
public sealed class AppException(ErrorKind kind, string code, string message, string? detail = null) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;

    public string Code { get; } = code;

    /// <summary>Подробности, например «"Байкал" занят 25 октября с 14:00 до 15:00». Попадают в поле detail ответа.</summary>
    public string? Detail { get; } = detail;
}
