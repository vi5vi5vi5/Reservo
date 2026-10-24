namespace Reservo.Application.Common.Errors;

/// <summary>Ошибки, общие для всех областей (коды из PROJECT.md, 7.3).</summary>
public static class CommonErrors
{
    /// <param name="message">Например «Ресурс не найден.» — целиком, чтобы род и число были правильными.</param>
    public static AppException NotFound(string message) =>
        new(ErrorKind.NotFound, "not_found", message);

    /// <summary>Запрос ссылается на запись, которой нет (например, ресурс с несуществующей категорией).</summary>
    public static AppException ReferenceNotFound(string message) =>
        new(ErrorKind.Validation, "reference_not_found", message);

    /// <summary>Запись нельзя удалить: на неё ссылаются другие данные.</summary>
    public static AppException InUse(string message = "Нельзя удалить: запись используется в других данных.") =>
        new(ErrorKind.Conflict, "in_use", message);
}
