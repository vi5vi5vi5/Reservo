namespace Reservo.Application.Common.Errors;

/// <summary>
/// Ожидаемая бизнес-ошибка, например «email уже занят» или «слот занят».
/// <see cref="Code"/> машиночитаемый — для фронтенда, сообщение — для людей.
/// </summary>
public sealed class AppException(ErrorKind kind, string code, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}
