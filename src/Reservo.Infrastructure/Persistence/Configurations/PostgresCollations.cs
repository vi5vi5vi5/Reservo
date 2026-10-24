namespace Reservo.Infrastructure.Persistence.Configurations;

internal static class PostgresCollations
{
    /// <summary>
    /// Русская сортировка из библиотеки ICU. Без неё PostgreSQL в Alpine-образе сортирует по кодам символов,
    /// и, например, «Ёлка» оказывается раньше «Аудитории». Назначается колонкам, по которым сортируются списки.
    /// </summary>
    public const string Russian = "ru-RU-x-icu";
}
