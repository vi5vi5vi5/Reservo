namespace Reservo.Domain.Catalog;

/// <summary>Единые правила очистки текстовых полей каталога.</summary>
internal static class CatalogText
{
    /// <summary>Коды хранятся без пробелов по краям и в нижнем регистре: «Meeting-Room » → «meeting-room».</summary>
    public static string NormalizeCode(string code) => code.Trim().ToLowerInvariant();

    /// <summary>Пустая или пробельная строка превращается в null, чтобы в базе не было «пустых» значений.</summary>
    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
