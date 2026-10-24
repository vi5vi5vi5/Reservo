using System.Linq.Expressions;
using Reservo.Domain.Bookings;

namespace Reservo.Application.Bookings;

/// <summary>Бронь.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Resource">Что забронировано.</param>
/// <param name="User">Кто забронировал.</param>
/// <param name="StartAt">Начало (UTC).</param>
/// <param name="EndAt">Конец, не включительно (UTC).</param>
/// <param name="Title">Тема или цель.</param>
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
    string Title,
    int? Attendees,
    BookingStatus Status,
    string? StatusReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Кратко о забронированном ресурсе.</summary>
/// <param name="Id">Id ресурса.</param>
/// <param name="Name">Название.</param>
/// <param name="LocationName">Корпус.</param>
/// <param name="RequiresApproval">Брони ресурса подтверждает администратор.</param>
public sealed record BookingResourceDto(int Id, string Name, string LocationName, bool RequiresApproval);

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
        new BookingResourceDto(b.Resource.Id, b.Resource.Name, b.Resource.Location.Name, b.Resource.RequiresApproval),
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
