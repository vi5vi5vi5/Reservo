using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Domain.Catalog;

namespace Reservo.Infrastructure.Persistence.Seed;

/// <summary>
/// Демо-каталог условного университета (PROJECT.md, 11.4). Добавляется один раз — в пустую базу,
/// поэтому правки, сделанные администратором через API, при перезапуске не теряются.
/// </summary>
internal static class CatalogSeeder
{
    private static readonly TimeOnly MainOpen = new(8, 0);
    private static readonly TimeOnly MainClose = new(21, 0);
    private static readonly TimeOnly LabOpen = new(9, 0);
    private static readonly TimeOnly LabClose = new(20, 0);
    private static readonly TimeOnly CoworkingOpen = new(8, 0);
    private static readonly TimeOnly CoworkingClose = new(22, 0);
    private static readonly TimeOnly LibraryOpen = new(9, 0);
    private static readonly TimeOnly LibraryClose = new(21, 0);

    /// <summary>Место в коворкинге можно занять максимум на 8 часов.</summary>
    private const int SeatMaxMinutes = 480;

    private sealed record ResourceSeed(
        string Name,
        string Category,
        string Location,
        int? Floor,
        int? Capacity,
        bool RequiresApproval,
        TimeOnly Open,
        TimeOnly Close,
        int? MaxDurationMinutes,
        string Description,
        string[] Features,
        bool AllowsOvernight = false,
        string? PickupInfo = null);

    /// <summary>Коворкинг: места нумеруются по рядам слева направо, в первых рядах — компьютеры, первый ряд у окна.</summary>
    private sealed record SpaceSeed(
        string Name,
        string ShortName,
        string Location,
        int Floor,
        bool IsQuiet,
        int Rows,
        int Columns,
        int AisleAfterColumn,
        int RowsWithComputers,
        TimeOnly Open,
        TimeOnly Close,
        string Description);

    private static readonly SpaceSeed[] Spaces =
    [
        new("Коворкинг «Точка»", "«Точка»", "Главный корпус", 1, false, 4, 6, 3, 3, CoworkingOpen, CoworkingClose,
            "Открытый коворкинг у главного входа. Можно говорить вполголоса."),
        new("Коворкинг в библиотеке", "Библиотека", "Корпус Б", 2, true, 3, 6, 3, 1, LibraryOpen, LibraryClose,
            "Тихая зона: без звонков и разговоров."),
    ];

    private static readonly ResourceSeed[] Resources =
    [
        new("Переговорная «Байкал»", "meeting-room", "Главный корпус", 2, 8, false, MainOpen, MainClose, null,
            "Переговорная для встреч команды и созвонов с партнёрами.", ["projector", "whiteboard", "video-conference"]),
        new("Переговорная «Ладога»", "meeting-room", "Главный корпус", 2, 6, false, MainOpen, MainClose, null,
            "Небольшая переговорная для обсуждений и интервью.", ["whiteboard", "video-conference"]),
        new("Переговорная «Онега»", "meeting-room", "Корпус Б", 1, 12, false, MainOpen, MainClose, null,
            "Просторная переговорная с интерактивной доской.", ["projector", "interactive-board", "video-conference", "air-conditioning"]),
        new("Переговорная «Селигер»", "meeting-room", "Корпус Б", 3, 4, false, MainOpen, MainClose, 120,
            "Комната для разговора вдвоём или вчетвером.", ["whiteboard"]),

        new("Аудитория 305", "classroom", "Главный корпус", 3, 30, false, MainOpen, MainClose, null,
            "Учебная аудитория для семинаров.", ["projector", "whiteboard"]),
        new("Аудитория 412", "classroom", "Главный корпус", 4, 24, false, MainOpen, MainClose, null,
            "Компьютерный класс на 24 рабочих места.", ["computers", "projector"]),
        new("Лекционный зал 1", "classroom", "Корпус Б", 1, 120, false, MainOpen, MainClose, null,
            "Большой зал для лекций, конференций и защит.", ["projector", "audio-system", "air-conditioning", "accessible"]),
        new("Аудитория Б-210", "classroom", "Корпус Б", 2, 40, false, MainOpen, MainClose, null,
            "Аудитория с интерактивной доской для практических занятий.", ["interactive-board", "projector", "accessible"]),

        new("Лаборатория робототехники", "lab", "Технопарк", 1, 16, true, LabOpen, LabClose, 360,
            "Стенды с манипуляторами и наборы для сборки роботов. Брони подтверждает администратор.", ["computers", "whiteboard"]),
        new("Лаборатория VR", "lab", "Технопарк", 2, 8, false, LabOpen, LabClose, null,
            "Шлемы виртуальной реальности и мощные рабочие станции.", ["computers", "air-conditioning"]),
        new("Лаборатория электроники", "lab", "Технопарк", 1, 12, false, LabOpen, LabClose, null,
            "Паяльные станции, осциллографы, макетные платы.", ["computers"]),

        new("3D-принтер Prusa MK4", "3d-printers", "Технопарк", 1, null, true, LabOpen, LabClose, 2880,
            "Печатает PLA и PETG. Печать можно оставить на ночь: корпус закрыт, принтер работает сам. Брони подтверждает администратор.",
            [], AllowsOvernight: true,
            PickupInfo: "Запуск печати и выдача деталей — Технопарк, 1 этаж, 09:00–20:00. Ночью принтер работает без присмотра."),
        new("Проектор Epson #2", "projectors", "Главный корпус", 1, null, false, MainOpen, MainClose, 480,
            "Переносной проектор с кабелем HDMI.", [],
            PickupInfo: "Стойка информации, Главный корпус, 1 этаж, 08:00–21:00."),
        new("Камера Sony для видеосъёмки", "cameras", "Главный корпус", 1, null, false, MainOpen, MainClose, 4320,
            "Камера со штативом и петличным микрофоном.", [], AllowsOvernight: true,
            PickupInfo: "Выдача и возврат — стойка информации, Главный корпус, 1 этаж, 08:00–21:00."),
        new("Ноутбук MacBook Pro #1", "laptops", "Корпус Б", 1, null, false, MainOpen, MainClose, 4320,
            "Ноутбук для презентаций и монтажа видео.", [], AllowsOvernight: true,
            PickupInfo: "Выдача и возврат — стойка информации, Корпус Б, 1 этаж, 08:00–21:00."),
    ];

    public static async Task SeedAsync(AppDbContext db, TimeProvider timeProvider, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        // Одна транзакция: либо каталог добавится целиком, либо не добавится вовсе. При включённых повторах
        // (EnableRetryOnFailure) свою транзакцию нужно выполнять через стратегию: при сбое она повторит её целиком.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await AddCatalogAsync(db, timeProvider, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        logger.LogInformation(
            "Добавлен демо-каталог: {Resources} ресурсов и {Spaces} коворкинга с {Seats} местами",
            Resources.Length, Spaces.Length, Spaces.Sum(s => s.Rows * s.Columns));
    }

    private static async Task AddCatalogAsync(AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {

        Category[] categories =
        [
            // Порядок важен: id 1–3 у помещений используют .http-файлы со сценариями.
            new("meeting-room", "Переговорные", "Комнаты для встреч и командной работы", ResourceKind.Room),
            new("classroom", "Аудитории", "Учебные аудитории и лекционные залы", ResourceKind.Room),
            new("lab", "Лаборатории", "Специализированные лаборатории", ResourceKind.Room),
            new("coworking", "Места в коворкинге", "Рабочие места в открытых пространствах", ResourceKind.Seat),
            new("projectors", "Проекторы", "Переносные проекторы", ResourceKind.Equipment),
            new("3d-printers", "3D-принтеры", "Печать деталей и прототипов", ResourceKind.Equipment),
            new("laptops", "Ноутбуки", "Ноутбуки на время", ResourceKind.Equipment),
            new("cameras", "Камеры", "Фото- и видеотехника", ResourceKind.Equipment),
        ];

        Location[] locations =
        [
            new("Главный корпус", "ул. Университетская, 1"),
            new("Корпус Б", "ул. Университетская, 3"),
            new("Технопарк", "ул. Инженерная, 12"),
        ];

        Feature[] features =
        [
            new("projector", "Проектор"),
            new("whiteboard", "Маркерная доска"),
            new("interactive-board", "Интерактивная доска"),
            new("video-conference", "Видеосвязь"),
            new("computers", "Компьютеры"),
            new("audio-system", "Звуковая система"),
            new("air-conditioning", "Кондиционер"),
            new("accessible", "Доступная среда"),
            new("window", "У окна"),
        ];

        db.Categories.AddRange(categories);
        db.Locations.AddRange(locations);
        db.Features.AddRange(features);

        // Первое сохранение выдаёт справочникам id, которые нужны ресурсам.
        await db.SaveChangesAsync(cancellationToken);

        var categoryByCode = categories.ToDictionary(c => c.Code);
        var locationByName = locations.ToDictionary(l => l.Name);
        var featureByCode = features.ToDictionary(f => f.Code);
        var now = timeProvider.GetUtcNow();

        foreach (var seed in Resources)
        {
            var resource = new Resource(
                new ResourceDetails(
                    categoryByCode[seed.Category].Id,
                    locationByName[seed.Location].Id,
                    seed.Name,
                    seed.Description,
                    seed.Floor,
                    seed.Capacity,
                    ImageUrl: null,
                    seed.RequiresApproval,
                    seed.Open,
                    seed.Close,
                    seed.MaxDurationMinutes,
                    seed.AllowsOvernight,
                    seed.PickupInfo),
                now);

            resource.SetFeatures(seed.Features.Select(code => featureByCode[code]));
            db.Resources.Add(resource);
        }

        // Коворкинги сохраняются отдельно: местам нужен их id.
        var spaces = Spaces.ToDictionary(seed => seed, seed => new Space(
            new SpaceDetails(locationByName[seed.Location].Id, seed.Name, seed.Description, seed.Floor, seed.IsQuiet,
                seed.Rows, seed.Columns, seed.AisleAfterColumn),
            now));
        db.Spaces.AddRange(spaces.Values);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (seed, space) in spaces)
        {
            var number = 1;
            for (var row = 1; row <= seed.Rows; row++)
            {
                for (var column = 1; column <= seed.Columns; column++, number++)
                {
                    var seat = new Resource(
                        new ResourceDetails(
                            categoryByCode["coworking"].Id,
                            space.LocationId,
                            $"{seed.ShortName}, место {number}",
                            seed.Description,
                            seed.Floor,
                            Capacity: null,
                            ImageUrl: null,
                            RequiresApproval: false,
                            seed.Open,
                            seed.Close,
                            SeatMaxMinutes,
                            Seat: new SeatPlacement(space.Id, number, row, column)),
                        now);

                    var seatFeatures = new List<Feature>();
                    if (row <= seed.RowsWithComputers)
                    {
                        seatFeatures.Add(featureByCode["computers"]);
                    }

                    if (row == 1)
                    {
                        seatFeatures.Add(featureByCode["window"]);
                    }

                    seat.SetFeatures(seatFeatures);
                    db.Resources.Add(seat);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
