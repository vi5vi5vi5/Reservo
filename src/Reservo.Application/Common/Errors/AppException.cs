namespace Reservo.Application.Common.Errors;

/// <summary>
/// An expected business error, e.g. "email already taken" or "time slot is busy".
/// <see cref="Code"/> is machine-readable for the frontend, the message is shown to people.
/// </summary>
public sealed class AppException(ErrorKind kind, string code, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}
