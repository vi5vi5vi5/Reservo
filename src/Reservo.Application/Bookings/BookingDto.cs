using System.Linq.Expressions;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Bookings;

/// <summary>Бронь.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Resource">Что забронировано.</param>
/// <param name="User">Кто забронировал.</param>
/// <param name="StartAt">Начало (UTC).</param>
/// <param name="EndAt">Конец, не включительно (UTC).</param>
/// <param name="Title">Тема или цель. У места в коворкинге может быть пустой.</param>
/// <param name="Attendees">Число участников.</param>
/// <param name="Status">Статус: <c>Pending</c>, <c>Confirmed</c>, <c>Rejected</c>, <c>Cancelled</c> или <c>Completed</c>.</param>
/// <param name="StatusReason">Причина отмены или отклонения.</param>
/// <param name="CreatedAt">Когда создана (UTC).</param>
/// <param name="UpdatedAt">Когда последний раз изменена (UTC).</param>
public sealed record BookingDto(
    int Id,
    BookingResourceDto Resource,
    BookingUserDto User,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Title,
    int? Attendees,
    BookingStatus Status,
    string? StatusReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Кратко о забронированном ресурсе — всё, что нужно карточке брони без отдельного запроса.</summary>
/// <param name="Id">Id ресурса.</param>
/// <param name="Name">Название.</param>
/// <param name="CategoryCode">Код категории — для иконки.</param>
/// <param name="Kind">Вид ресурса: <c>Room</c>, <c>Seat</c> или <c>Equipment</c>.</param>
/// <param name="LocationName">Корпус.</param>
/// <param name="Floor">Этаж.</param>
/// <param name="RequiresApproval">Брони ресурса подтверждает администратор.</param>
/// <param name="AllowsOvernight">Ресурс можно бронировать на ночь и на несколько дней.</param>
/// <param name="PickupInfo">Где выдают и принимают оборудование.</param>
/// <param name="SpaceId">Коворкинг — только у мест.</param>
/// <param name="SpaceName">Название коворкинга — только у мест.</param>
/// <param name="SeatNumber">Номер места — только у мест.</param>
public sealed record BookingResourceDto(
    int Id,
    string Name,
    string CategoryCode,
    ResourceKind Kind,
    string LocationName,
    int? Floor,
    bool RequiresApproval,
    bool AllowsOvernight,
    string? PickupInfo,
    int? SpaceId,
    string? SpaceName,
    int? SeatNumber);

/// <summary>Кратко о владельце брони.</summary>
/// <param name="Id">Id пользователя.</param>
/// <param name="FullName">Имя.</param>
/// <param name="Email">Email.</param>
public sealed record BookingUserDto(int Id, string FullName, string Email);

public static class BookingMappings
{
    /// <summary>Проекция для LINQ: одна выборка вместе с ресурсом, корпусом и владельцем.</summary>
    public static readonly Expression<Func<Booking, BookingDto>> ToDto = b => new BookingDto(
        b.Id,
        new BookingResourceDto(
            b.Resource.Id,
            b.Resource.Name,
            b.Resource.Category.Code,
            b.Resource.Category.Kind,
            b.Resource.Location.Name,
            b.Resource.Floor,
            b.Resource.RequiresApproval,
            b.Resource.AllowsOvernight,
            b.Resource.PickupInfo,
            b.Resource.SpaceId,
            b.Resource.Space != null ? b.Resource.Space.Name : null,
            b.Resource.SeatNumber),
        new BookingUserDto(b.User.Id, b.User.FullName, b.User.Email),
        b.StartAt,
        b.EndAt,
        b.Title,
        b.Attendees,
        b.Status,
        b.StatusReason,
        b.CreatedAt,
        b.UpdatedAt);
}
