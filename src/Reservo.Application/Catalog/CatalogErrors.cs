using Reservo.Application.Common.Errors;

namespace Reservo.Application.Catalog;

/// <summary>Ошибки каталога (коды из PROJECT.md, 7.3).</summary>
public static class CatalogErrors
{
    public static AppException DuplicateCategory() =>
        new(ErrorKind.Conflict, "catalog.duplicate", "Категория с таким кодом или названием уже существует.");

    public static AppException DuplicateLocation() =>
        new(ErrorKind.Conflict, "catalog.duplicate", "Корпус с таким названием уже существует.");

    public static AppException DuplicateFeature() =>
        new(ErrorKind.Conflict, "catalog.duplicate", "Оснащение с таким кодом или названием уже существует.");
}
