namespace Reservo.Domain.Common;

/// <summary>
/// Нарушение бизнес-правила, найденное доменной логикой: машиночитаемый код и сообщение для человека.
/// Правила не выбрасывают исключений, а возвращают такую ошибку (или null, если всё в порядке) —
/// поэтому их легко проверять в unit-тестах. Слой Application превращает её в ответ API.
/// </summary>
/// <param name="Code">Код ошибки из PROJECT.md, 7.3, например <c>booking.time_conflict</c>.</param>
/// <param name="Message">Короткое сообщение для пользователя.</param>
/// <param name="Detail">Подробности, например какое именно время занято.</param>
/// <param name="IsConflict">true — конфликт с текущим состоянием данных (HTTP 409), false — некорректный запрос (HTTP 400).</param>
public sealed record DomainError(string Code, string Message, string? Detail = null, bool IsConflict = false);
