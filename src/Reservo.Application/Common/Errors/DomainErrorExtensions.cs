using Reservo.Domain.Common;

namespace Reservo.Application.Common.Errors;

public static class DomainErrorExtensions
{
    /// <summary>Нарушение доменного правила превращается в ответ 409 (конфликт) или 400 (некорректный запрос).</summary>
    public static AppException ToAppException(this DomainError error) =>
        new(error.IsConflict ? ErrorKind.Conflict : ErrorKind.Validation, error.Code, error.Message, error.Detail);

    /// <summary>Правила возвращают null, если всё в порядке. Иначе выбрасываем ошибку, и запрос дальше не идёт.</summary>
    public static void ThrowIfViolated(this DomainError? error)
    {
        if (error is not null)
        {
            throw error.ToAppException();
        }
    }
}
