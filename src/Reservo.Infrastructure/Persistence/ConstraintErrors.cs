using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reservo.Application.Auth;
using Reservo.Application.Catalog;
using Reservo.Application.Common.Errors;
using Reservo.Infrastructure.Persistence.Configurations;

namespace Reservo.Infrastructure.Persistence;

/// <summary>
/// Ограничения базы — последний рубеж против гонок: два запроса могут одновременно пройти одну и ту же
/// проверку в коде, но через ограничение пройдёт только один из них. Нарушение ограничения превращается
/// в ту же бизнес-ошибку, которую выбросил бы сервис, поэтому клиент всегда получает одинаковый ответ.
/// </summary>
internal static class ConstraintErrors
{
    private static readonly Dictionary<string, Func<AppException>> UniqueViolations = new()
    {
        [UserConfiguration.EmailIndexName] = AuthErrors.EmailTaken,
        [CategoryConfiguration.CodeIndexName] = CatalogErrors.DuplicateCategory,
        [CategoryConfiguration.NameIndexName] = CatalogErrors.DuplicateCategory,
        [LocationConfiguration.NameIndexName] = CatalogErrors.DuplicateLocation,
        [FeatureConfiguration.CodeIndexName] = CatalogErrors.DuplicateFeature,
        [FeatureConfiguration.NameIndexName] = CatalogErrors.DuplicateFeature,
    };

    public static bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out AppException? error)
    {
        error = exception.InnerException switch
        {
            // Уникальный индекс: ошибка зависит от того, какой именно индекс нарушен.
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: { } index }
                when UniqueViolations.TryGetValue(index, out var createError) => createError(),

            // Удаляем запись, на которую ещё ссылаются. Для ON DELETE RESTRICT PostgreSQL возвращает
            // отдельный код 23001, для обычного внешнего ключа — 23503 (тогда смотрим, было ли это удаление).
            PostgresException { SqlState: PostgresErrorCodes.RestrictViolation } => CommonErrors.InUse(),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation }
                when exception.Entries.Any(e => e.State == EntityState.Deleted) => CommonErrors.InUse(),

            // Ссылаемся на запись, которой уже нет.
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                CommonErrors.ReferenceNotFound("Связанная запись не найдена."),

            _ => null,
        };

        return error is not null;
    }
}
