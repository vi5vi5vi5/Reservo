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

    public static AppException CategoryKindLocked() =>
        new(ErrorKind.Conflict, "in_use", "Нельзя сменить вид категории, пока в ней есть ресурсы.");

    public static AppException DuplicateSeat() =>
        new(ErrorKind.Conflict, "catalog.duplicate", "В этом коворкинге уже есть место с таким номером или на этой позиции схемы.");

    public static AppException SpaceNotFound() => CommonErrors.NotFound("Коворкинг не найден.");

    public static AppException SeatPlacementRequired() =>
        new(ErrorKind.Validation, "validation_failed",
            "Для места в коворкинге укажите коворкинг, номер места, ряд и колонку.");

    public static AppException SeatPlacementNotAllowed() =>
        new(ErrorKind.Validation, "validation_failed",
            "Положение на схеме коворкинга задаётся только местам — ресурсам категории вида Seat.");

    public static AppException SeatOutsideLayout(int rows, int columns) =>
        new(ErrorKind.Validation, "validation_failed",
            $"Место должно быть на схеме коворкинга: ряд от 1 до {rows}, колонка от 1 до {columns}.");

    public static AppException SeatCannotBeOvernight() =>
        new(ErrorKind.Validation, "validation_failed", "Место в коворкинге нельзя бронировать на ночь.");
}
