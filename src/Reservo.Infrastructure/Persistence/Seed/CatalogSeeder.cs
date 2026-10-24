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
        string[] Features);

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

        new("3D-принтер Prusa MK4", "equipment", "Технопарк", 1, null, true, LabOpen, LabClose, 480,
            "Печать пластиком PLA и PETG. Брони подтверждает администратор.", []),
        new("Проектор Epson #2", "equipment", "Главный корпус", 1, null, false, MainOpen, MainClose, 480,
            "Переносной проектор. Выдаётся на стойке информации, 1 этаж.", []),
        new("Камера Sony для видеосъёмки", "equipment", "Главный корпус", 1, null, false, MainOpen, MainClose, 480,
            "Камера со штативом и петличным микрофоном.", []),
        new("Ноутбук MacBook Pro #1", "equipment", "Корпус Б", 1, null, false, MainOpen, MainClose, 480,
            "Ноутбук для презентаций и монтажа видео.", []),
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
            "Добавлен демо-каталог: {Resources} ресурсов в {Categories} категориях",
            Resources.Length, Resources.Select(r => r.Category).Distinct().Count());
    }

    private static async Task AddCatalogAsync(AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {

        Category[] categories =
        [
            new("meeting-room", "Переговорные", "Комнаты для встреч и командной работы"),
            new("classroom", "Аудитории", "Учебные аудитории и лекционные залы"),
            new("lab", "Лаборатории", "Специализированные лаборатории"),
            new("equipment", "Оборудование", "Техника, которую можно взять на время"),
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
                    seed.MaxDurationMinutes),
                now);

            resource.SetFeatures(seed.Features.Select(code => featureByCode[code]));
            db.Resources.Add(resource);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
