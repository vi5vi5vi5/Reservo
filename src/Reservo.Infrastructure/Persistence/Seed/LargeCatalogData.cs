namespace Reservo.Infrastructure.Persistence.Seed;

/// <summary>
/// Справочники для большого демо-каталога: кампусы и офисы, виды помещений и оборудования.
/// Только данные — как из них собирается каталог, описано в <see cref="LargeCatalogSeeder"/>.
/// </summary>
internal static class LargeCatalogData
{
    public sealed record CampusSeed(string Name, string Street, int Buildings);

    public static readonly CampusSeed[] Campuses =
    [
        new("Северный", "ул. Северная", 5),
        new("Речной", "Набережная ул.", 4),
        new("Сосновый бор", "Сосновая аллея", 4),
        new("Политех", "Политехническая ул.", 5),
        new("Академгородок", "просп. Науки", 4),
        new("Медицинский", "ул. Пирогова", 3),
        new("Гуманитарный", "ул. Ломоносова", 3),
        new("Приморский", "Приморский бульвар", 4),
        new("Южный", "Южное шоссе", 3),
        new("Инженерный", "ул. Инженеров", 4),
    ];

    public static readonly (string Name, string Address)[] Offices =
    [
        ("Офис «Сити»", "Пресненская наб., 10"),
        ("Офис «Арбат»", "ул. Новый Арбат, 21"),
        ("Офис «Невский»", "Невский просп., 88"),
        ("Офис «Казанский»", "ул. Баумана, 44"),
        ("Офис «Уральский»", "ул. Малышева, 51"),
        ("Офис «Сибирь»", "Красный просп., 17"),
        ("Офис «Волга»", "Верхне-Волжская наб., 5"),
        ("Офис «Балтика»", "Ленинский просп., 120"),
        ("Офис «Кавказ»", "просп. Карла Маркса, 3"),
        ("Офис «Тихоокеанский»", "ул. Светланская, 29"),
    ];

    public static readonly string[] BuildingLetters = ["А", "Б", "В", "Г", "Д"];

    /// <summary>Новые виды оснащения помещений. Существующие (проектор, доска…) берутся из базового каталога по коду.</summary>
    public static readonly (string Code, string Name)[] Features =
    [
        ("flipchart", "Флипчарт"),
        ("soundproofing", "Звукоизоляция"),
        ("studio-light", "Студийный свет"),
        ("chroma-key", "Хромакей"),
        ("mirrors", "Зеркала во всю стену"),
        ("piano", "Фортепиано"),
        ("stage", "Сцена"),
        ("microphones", "Микрофоны"),
        ("document-camera", "Документ-камера"),
        ("power-outlets", "Розетки у каждого места"),
        ("coffee-machine", "Кофемашина"),
        ("fume-hood", "Вытяжной шкаф"),
        ("lockers", "Раздевалка и шкафчики"),
        ("tv-panel", "Большой экран"),
    ];

    /// <summary>Вид помещения: код категории, название, как назвать одно помещение, вместимость, оснащение, согласование.</summary>
    public sealed record RoomKind(
        string Code,
        string Category,
        string? Description,
        string Singular,
        int MinCapacity,
        int MaxCapacity,
        string[] Features,
        double ApprovalChance = 0,
        int? MaxMinutes = null);

    public static readonly RoomKind[] RoomKinds =
    [
        // Базовые категории (есть в обычном каталоге) — новых не создают, только наполняются.
        new("meeting-room", "Переговорные", null, "Переговорная", 4, 16, ["whiteboard", "video-conference", "projector", "tv-panel", "flipchart", "air-conditioning", "coffee-machine"]),
        new("classroom", "Аудитории", null, "Аудитория", 20, 60, ["projector", "whiteboard", "interactive-board", "document-camera", "accessible", "air-conditioning"]),
        new("lab", "Лаборатории", null, "Лаборатория", 8, 24, ["computers", "whiteboard", "fume-hood", "air-conditioning", "power-outlets"], ApprovalChance: 0.3, MaxMinutes: 360),

        new("lecture-hall", "Лекционные залы", "Большие залы для лекций и защит", "Лекционный зал", 80, 300, ["projector", "audio-system", "microphones", "accessible", "air-conditioning", "stage"], ApprovalChance: 0.2),
        new("seminar-room", "Семинарские комнаты", "Комнаты на одну учебную группу", "Семинарская", 10, 24, ["whiteboard", "projector", "flipchart", "tv-panel"]),
        new("computer-class", "Компьютерные классы", "Классы с рабочими станциями", "Компьютерный класс", 12, 32, ["computers", "projector", "power-outlets", "air-conditioning"]),
        new("conference-hall", "Конференц-залы", "Залы для конференций и больших встреч", "Конференц-зал", 30, 150, ["projector", "audio-system", "microphones", "video-conference", "stage", "air-conditioning"], ApprovalChance: 0.4),
        new("consult-room", "Кабинеты для консультаций", "Небольшие кабинеты на двоих-четверых", "Кабинет для консультаций", 2, 4, ["whiteboard", "tv-panel"], MaxMinutes: 120),
        new("phone-booth", "Телефонные кабины", "Звукоизолированные кабины для звонков", "Кабина для звонков", 1, 2, ["soundproofing", "power-outlets"], MaxMinutes: 60),
        new("recording-studio", "Студии звукозаписи", "Звукоизолированные студии с пультом", "Студия звукозаписи", 2, 6, ["soundproofing", "microphones", "audio-system", "computers"], ApprovalChance: 0.7),
        new("video-studio", "Видеостудии", "Студии для съёмки лекций и интервью", "Видеостудия", 3, 10, ["studio-light", "chroma-key", "microphones", "soundproofing"], ApprovalChance: 0.7),
        new("photo-studio", "Фотостудии", "Студии с фонами и светом", "Фотостудия", 2, 8, ["studio-light", "chroma-key"], ApprovalChance: 0.5),
        new("workshop", "Мастерские", "Мейкерспейсы с инструментом", "Мастерская", 6, 20, ["power-outlets", "fume-hood", "whiteboard"], ApprovalChance: 0.5, MaxMinutes: 360),
        new("gym", "Спортивные залы", "Залы для игр и тренировок", "Спортзал", 20, 60, ["lockers", "audio-system"]),
        new("dance-hall", "Танцевальные залы", "Залы с зеркалами и звуком", "Танцевальный зал", 10, 30, ["mirrors", "audio-system", "lockers"]),
        new("music-room", "Музыкальные классы", "Классы с инструментами", "Музыкальный класс", 2, 10, ["piano", "soundproofing", "audio-system"]),
        new("assembly-hall", "Актовые залы", "Залы для мероприятий", "Актовый зал", 150, 500, ["stage", "audio-system", "microphones", "projector", "accessible"], ApprovalChance: 0.9, MaxMinutes: 300),
        new("reading-room", "Читальные залы", "Тихие залы с рабочими местами", "Читальный зал", 20, 80, ["power-outlets", "accessible"]),
        new("exhibition-space", "Выставочные пространства", "Пространства для выставок и стендов", "Выставочный зал", 30, 120, ["studio-light", "power-outlets", "accessible"], ApprovalChance: 0.8, MaxMinutes: 480),
    ];

    /// <summary>Сколько помещений вида поставить: в корпусе кампуса и в офисе. «0..2» — от 0 до 2.</summary>
    public static readonly Dictionary<string, (int Min, int Max)> CampusRooms = new()
    {
        ["classroom"] = (6, 12),
        ["seminar-room"] = (2, 5),
        ["lab"] = (1, 4),
        ["computer-class"] = (1, 3),
        ["lecture-hall"] = (0, 2),
        ["meeting-room"] = (1, 4),
        ["consult-room"] = (1, 3),
        ["phone-booth"] = (0, 2),
        ["recording-studio"] = (0, 1),
        ["video-studio"] = (0, 1),
        ["photo-studio"] = (0, 1),
        ["workshop"] = (0, 1),
        ["gym"] = (0, 1),
        ["dance-hall"] = (0, 1),
        ["music-room"] = (0, 2),
        ["assembly-hall"] = (0, 1),
        ["reading-room"] = (0, 1),
        ["exhibition-space"] = (0, 1),
        ["conference-hall"] = (0, 1),
    };

    public static readonly Dictionary<string, (int Min, int Max)> OfficeRooms = new()
    {
        ["meeting-room"] = (6, 14),
        ["phone-booth"] = (3, 8),
        ["conference-hall"] = (1, 2),
        ["consult-room"] = (1, 3),
        ["video-studio"] = (0, 1),
        ["recording-studio"] = (0, 1),
    };

    /// <summary>Имена переговорных — озёра, реки, горы и острова.</summary>
    public static readonly string[] MeetingNames =
    [
        "Таймыр", "Ямал", "Алтай", "Эльбрус", "Казбек", "Енисей", "Лена", "Обь", "Амур", "Волга", "Кама", "Ока", "Дон", "Нева",
        "Ильмень", "Телецкое", "Иссык-Куль", "Валдай", "Хибины", "Урал", "Саяны", "Сахалин", "Камчатка", "Кижи", "Соловки",
        "Валаам", "Байконур", "Арктика", "Полярная", "Северное сияние", "Белое море", "Каспий", "Азов", "Чудское", "Плещеево",
        "Сенеж", "Вуокса", "Ангара", "Иртыш", "Тобол", "Печора", "Онон", "Шантары", "Командоры", "Курилы", "Эверест", "Монблан",
        "Килиманджаро", "Фудзи", "Анды", "Гималаи", "Памир", "Тибет", "Сахара", "Гоби", "Атакама", "Байкальская", "Тайга", "Тундра",
    ];

    public static readonly string[] LabNames =
    [
        "робототехники", "VR и AR", "электроники", "микроэлектроники", "химии", "органической химии", "биологии", "генетики",
        "нейробиологии", "физики", "оптики", "акустики", "материаловедения", "аддитивных технологий", "мехатроники",
        "искусственного интеллекта", "анализа данных", "кибербезопасности", "интернета вещей", "беспилотных систем",
        "энергетики", "возобновляемой энергетики", "геоинформатики", "биоинженерии", "нанотехнологий", "автоматики",
        "космических систем", "медицинской техники", "экологии", "звукорежиссуры",
    ];

    /// <summary>
    /// Вид оборудования: код категории, название категории, как назвать одну вещь, модели (необязательно),
    /// можно ли на ночь и на дни, предел длительности в минутах, нужно ли согласование, сколько штук в фонде.
    /// </summary>
    public sealed record EquipmentKind(
        string Code,
        string Category,
        string Singular,
        string[] Models,
        bool Overnight,
        int MaxMinutes,
        bool Approval = false,
        int MinCount = 1,
        int MaxCount = 3,
        bool Office = false);

    private static EquipmentKind E(string code, string category, string singular, string models, bool overnight, int maxMinutes,
        bool approval = false, int min = 1, int max = 3, bool office = false) =>
        new(code, category, singular, models.Length == 0 ? [] : models.Split('|'), overnight, maxMinutes, approval, min, max, office);

    public static readonly EquipmentKind[] Equipment =
    [
        // Компьютеры и связь
        E("laptops", "Ноутбуки", "Ноутбук", "Lenovo ThinkPad T14|MacBook Air M3|ASUS Zenbook 14|HP EliteBook 840|Dell Latitude 5440", true, 4320, min: 3, max: 10, office: true),
        E("tablets", "Планшеты", "Планшет", "iPad Air|Samsung Galaxy Tab S9|Xiaomi Pad 6", true, 4320, min: 2, max: 6, office: true),
        E("graphic-tablets", "Графические планшеты", "Графический планшет", "Wacom Intuos Pro|XP-Pen Artist 16|Huion Kamvas 22", true, 2880, max: 4),
        E("e-readers", "Электронные книги", "Электронная книга", "PocketBook 743|Kindle Paperwhite|ONYX BOOX Note", true, 4320, max: 5),
        E("test-phones", "Смартфоны для тестирования", "Смартфон", "Pixel 8|iPhone 15|Samsung Galaxy A55|Xiaomi Redmi Note 13", true, 2880, max: 4),
        E("monitors", "Мониторы", "Монитор", "Dell U2723QE|LG UltraFine 27|Samsung Odyssey G7", true, 4320, max: 4, office: true),
        E("docking-stations", "Док-станции", "Док-станция", "Dell WD19|CalDigit TS4", true, 4320, max: 4, office: true),
        E("keyboards-mice", "Клавиатуры и мыши", "Комплект клавиатура и мышь", "Logitech MX Keys|Logitech MX Master", true, 4320, max: 5, office: true),
        E("routers", "Роутеры", "Роутер", "Keenetic Giga|TP-Link Archer AX55|MikroTik hAP ax3", true, 2880, max: 3),
        E("network-switches", "Сетевые коммутаторы", "Коммутатор", "Cisco Catalyst 1000|MikroTik CRS326", true, 2880, max: 2),
        E("servers", "Учебные серверы", "Сервер", "Dell PowerEdge R250|HPE ProLiant ML30", true, 4320, approval: true, max: 2),
        E("single-board", "Одноплатные компьютеры", "Одноплатный компьютер", "Raspberry Pi 5|Orange Pi 5|Jetson Orin Nano", true, 4320, max: 6),
        E("arduino-kits", "Наборы Arduino", "Набор Arduino", "Arduino Uno R4 Starter|Arduino Mega Kit", true, 4320, max: 8),
        E("robot-kits", "Робототехнические наборы", "Робототехнический набор", "LEGO Mindstorms|ROBOTIS Bioloid|Makeblock mBot", true, 4320, max: 5),
        E("lego-kits", "Конструкторы LEGO", "Конструктор LEGO", "LEGO Education SPIKE|LEGO Technic", true, 4320, max: 6),
        E("power-banks", "Пауэрбанки", "Пауэрбанк", "Anker 737|Xiaomi 20000 мАч", true, 1440, max: 8, office: true),
        E("chargers", "Зарядные устройства", "Зарядное устройство", "Anker 100 Вт|Ugreen Nexode 65 Вт", true, 1440, max: 8, office: true),
        E("cables-adapters", "Кабели и переходники", "Набор кабелей и переходников", "", true, 1440, max: 6, office: true),
        E("extension-cords", "Удлинители", "Удлинитель", "", true, 1440, max: 6, office: true),
        E("webcams", "Веб-камеры", "Веб-камера", "Logitech Brio|Logitech C920", true, 2880, max: 4, office: true),
        E("conference-kits", "Комплекты для видеосвязи", "Комплект для видеосвязи", "Jabra PanaCast 50|Logitech Rally Bar Mini|Poly Studio X30", false, 480, max: 2, office: true),
        E("speakerphones", "Спикерфоны", "Спикерфон", "Jabra Speak2 75|Poly Sync 20", true, 1440, max: 4, office: true),

        // Презентации
        E("projectors", "Проекторы", "Проектор", "Epson EB-W51|BenQ MW560|ViewSonic PA503", false, 480, max: 4, office: true),
        E("projection-screens", "Проекционные экраны", "Экран на штативе", "", false, 480, max: 3, office: true),
        E("document-cameras", "Документ-камеры", "Документ-камера", "IPEVO V4K|AVerVision U70", false, 480, max: 3),
        E("interactive-panels", "Интерактивные панели", "Интерактивная панель на стойке", "SMART Board MX|Newline Q", false, 480, approval: true, max: 2),
        E("tv-carts", "Телевизоры на стойке", "Телевизор на стойке", "Samsung 65\"|LG 75\"", false, 480, max: 3, office: true),
        E("teleprompters", "Телесуфлёры", "Телесуфлёр", "Glide Gear TMP100|Neewer X12", true, 1440, max: 2),
        E("flipcharts", "Флипчарты", "Флипчарт", "", false, 480, max: 4, office: true),
        E("mobile-whiteboards", "Мобильные доски", "Мобильная доска", "", false, 480, max: 4, office: true),
        E("presenters", "Презентеры", "Презентер", "Logitech R500|Logitech Spotlight", true, 1440, max: 6, office: true),
        E("lecterns", "Трибуны", "Трибуна", "", false, 480, max: 2),

        // Фото и видео
        E("cameras", "Камеры", "Камера", "Sony ZV-E10|Canon EOS R50|Nikon Z30", true, 4320, max: 4),
        E("cinema-cameras", "Кинокамеры", "Кинокамера", "Blackmagic Pocket 6K|Sony FX3|Canon C70", true, 2880, approval: true, max: 2),
        E("action-cameras", "Экшн-камеры", "Экшн-камера", "GoPro HERO12|DJI Osmo Action 4|Insta360 X3", true, 4320, max: 4),
        E("camcorders", "Видеокамеры", "Видеокамера", "Sony HXR-NX80|Panasonic HC-X2000", true, 2880, max: 3),
        E("lenses", "Объективы", "Объектив", "Sony 24-70 f/2.8|Canon RF 50 f/1.8|Sigma 18-35 f/1.8", true, 2880, max: 5),
        E("tripods", "Штативы", "Штатив", "Manfrotto 190X|Benro Mach3", true, 4320, max: 6),
        E("gimbals", "Стабилизаторы", "Стабилизатор", "DJI RS 3|Zhiyun Crane 4|DJI Osmo Mobile 6", true, 2880, max: 4),
        E("light-panels", "Свет для съёмки", "Светодиодная панель", "Aputure Amaran 200x|Godox SL60W|Nanlite Forza 60", true, 2880, max: 5),
        E("softboxes", "Софтбоксы", "Софтбокс", "Godox SB-UE|Aputure Light Dome", true, 2880, max: 4),
        E("chroma-backdrops", "Хромакей-фоны", "Хромакей-фон", "", true, 2880, max: 2),
        E("photo-backdrops", "Фотофоны", "Фотофон", "", true, 2880, max: 3),
        E("reflectors", "Отражатели", "Отражатель", "", true, 2880, max: 4),
        E("drones", "Квадрокоптеры", "Квадрокоптер", "DJI Mini 4 Pro|DJI Air 3|DJI Avata 2", true, 2880, approval: true, max: 3),
        E("fpv-drones", "FPV-дроны", "FPV-дрон", "", true, 1440, approval: true, max: 2),
        E("streaming-kits", "Стримерские комплекты", "Комплект для стрима", "Elgato Stream Deck + Cam Link|ATEM Mini Pro", true, 2880, max: 2),
        E("capture-cards", "Карты видеозахвата", "Карта видеозахвата", "Elgato HD60 X|AVerMedia Live Gamer", true, 2880, max: 3),
        E("360-cameras", "Камеры 360°", "Камера 360°", "Insta360 X4|Ricoh Theta Z1", true, 2880, max: 2),

        // Звук
        E("microphones", "Микрофоны", "Микрофон", "Shure SM58|Rode NT-USB|Audio-Technica AT2020", true, 2880, max: 6),
        E("lavalier-mics", "Петличные микрофоны", "Петличный микрофон", "Rode Wireless GO II|DJI Mic 2|Sennheiser XSW-D", true, 2880, max: 5),
        E("shotgun-mics", "Микрофоны-пушки", "Микрофон-пушка", "Rode NTG4+|Sennheiser MKE 600", true, 2880, max: 3),
        E("radio-mics", "Радиосистемы", "Радиосистема", "Shure BLX24|Sennheiser EW 100", false, 480, max: 3),
        E("audio-recorders", "Аудиорекордеры", "Аудиорекордер", "Zoom H6|Tascam DR-40X|Zoom H4n Pro", true, 2880, max: 4),
        E("headphones", "Наушники", "Наушники", "Sony MDR-7506|Audio-Technica ATH-M50x|Beyerdynamic DT 770", true, 4320, max: 8),
        E("speakers", "Акустические системы", "Колонка", "JBL EON 712|Yamaha DXR10|JBL PartyBox 310", false, 480, max: 3),
        E("mixers", "Микшерные пульты", "Микшерный пульт", "Yamaha MG10XU|Behringer X32 Compact|Allen & Heath ZEDi-10", false, 480, approval: true, max: 2),
        E("audio-interfaces", "Аудиоинтерфейсы", "Аудиоинтерфейс", "Focusrite Scarlett 2i2|Steinberg UR22C", true, 2880, max: 4),
        E("megaphones", "Мегафоны", "Мегафон", "", true, 1440, max: 3),
        E("karaoke", "Караоке-системы", "Караоке-система", "", false, 480, max: 1),

        // Музыка
        E("synthesizers", "Синтезаторы", "Синтезатор", "Yamaha PSR-E473|Korg minilogue|Roland JUNO-DS61", true, 2880, max: 3),
        E("digital-pianos", "Цифровые пианино", "Цифровое пианино", "Yamaha P-145|Casio Privia PX-S1100", false, 480, max: 2),
        E("guitars", "Гитары", "Гитара", "Yamaha C40|Fender Stratocaster|Yamaha APX600", true, 2880, max: 4),
        E("bass-guitars", "Бас-гитары", "Бас-гитара", "Fender Precision Bass|Ibanez SR300", true, 2880, max: 2),
        E("drum-kits", "Ударные установки", "Электронная ударная установка", "Roland TD-07|Alesis Nitro Max", false, 480, max: 1),
        E("midi-keyboards", "MIDI-клавиатуры", "MIDI-клавиатура", "Arturia KeyLab 49|Novation Launchkey 61", true, 2880, max: 3),
        E("dj-controllers", "DJ-контроллеры", "DJ-контроллер", "Pioneer DDJ-FLX4|Numark Mixtrack Pro", true, 1440, max: 2),

        // VR, игры
        E("vr-headsets", "VR-шлемы", "VR-шлем", "Meta Quest 3|Pico 4|HTC Vive XR Elite", true, 2880, approval: true, max: 5),
        E("ar-glasses", "AR-очки", "AR-очки", "XREAL Air 2|Microsoft HoloLens 2", true, 2880, approval: true, max: 2),
        E("game-consoles", "Игровые консоли", "Игровая консоль", "PlayStation 5|Xbox Series X|Nintendo Switch OLED", true, 1440, max: 3),
        E("gamepads", "Геймпады", "Геймпад", "DualSense|Xbox Wireless Controller", true, 1440, max: 6),
        E("racing-wheels", "Игровые рули", "Игровой руль", "Logitech G29|Thrustmaster T300", false, 480, max: 1),

        // Производство и мейкерспейс
        E("3d-printers", "3D-принтеры", "3D-принтер", "Prusa MK4|Bambu Lab X1C|Creality K1|Anycubic Kobra 3", true, 2880, approval: true, max: 4),
        E("resin-printers", "Фотополимерные принтеры", "Фотополимерный принтер", "Elegoo Saturn 4|Formlabs Form 3+", true, 2880, approval: true, max: 2),
        E("3d-scanners", "3D-сканеры", "3D-сканер", "Shining3D EinScan SE|Creality CR-Scan Ferret", true, 1440, approval: true, max: 2),
        E("laser-cutters", "Лазерные граверы", "Лазерный гравер", "xTool P2|Glowforge Pro|Trotec Speedy 100", false, 360, approval: true, max: 2),
        E("cnc-mills", "Фрезерные станки ЧПУ", "Фрезерный станок ЧПУ", "Shapeoko 4|Carbide Nomad 3", false, 360, approval: true, max: 1),
        E("vinyl-cutters", "Режущие плоттеры", "Режущий плоттер", "Roland GS2-24|Silhouette Cameo 5", false, 480, max: 1),
        E("soldering-stations", "Паяльные станции", "Паяльная станция", "Hakko FX-888D|Weller WE1010|Quick 861DW", false, 480, max: 6),
        E("oscilloscopes", "Осциллографы", "Осциллограф", "Rigol DS1054Z|Siglent SDS1104X-E", false, 480, max: 4),
        E("multimeters", "Мультиметры", "Мультиметр", "Fluke 117|UNI-T UT61E", true, 1440, max: 8),
        E("signal-generators", "Генераторы сигналов", "Генератор сигналов", "Rigol DG1022Z|Siglent SDG2042X", false, 480, max: 2),
        E("power-supplies", "Лабораторные блоки питания", "Лабораторный блок питания", "Korad KA3005D|Rigol DP832", false, 480, max: 5),
        E("logic-analyzers", "Логические анализаторы", "Логический анализатор", "Saleae Logic Pro 8|DSLogic Plus", true, 1440, max: 3),
        E("thermal-cameras", "Тепловизоры", "Тепловизор", "FLIR E8|Seek Thermal Compact", true, 1440, approval: true, max: 2),
        E("drills", "Дрели и шуруповёрты", "Шуруповёрт", "Makita DDF485|Bosch GSR 18V", true, 1440, max: 4),
        E("hammer-drills", "Перфораторы", "Перфоратор", "Bosch GBH 2-26|Makita HR2470", true, 1440, max: 2),
        E("jigsaws", "Электролобзики", "Электролобзик", "Bosch PST 700|Makita 4329", true, 1440, max: 2),
        E("angle-grinders", "Болгарки", "Угловая шлифмашина", "Makita GA5030|Bosch GWS 750", false, 480, approval: true, max: 2),
        E("tool-kits", "Наборы инструментов", "Набор инструментов", "", true, 2880, max: 5),
        E("glue-guns", "Термоклеевые пистолеты", "Термоклеевой пистолет", "", true, 1440, max: 6),
        E("sewing-machines", "Швейные машины", "Швейная машина", "Janome 1143|Brother JS-25", false, 480, max: 3),
        E("overlocks", "Оверлоки", "Оверлок", "Janome 990D|Brother 1034D", false, 480, max: 2),
        E("mannequins", "Манекены", "Манекен", "", true, 4320, max: 3),
        E("steamers", "Отпариватели", "Отпариватель", "Philips STE3170|Tefal Pro Style", false, 480, max: 2),

        // Наука и лаборатории
        E("microscopes", "Микроскопы", "Микроскоп", "Levenhuk 740T|Микромед 3|Bresser Science", false, 480, max: 5),
        E("telescopes", "Телескопы", "Телескоп", "Sky-Watcher 150P|Celestron NexStar 8SE", true, 1440, approval: true, max: 2),
        E("spectrophotometers", "Спектрофотометры", "Спектрофотометр", "ПЭ-5400УФ|Shimadzu UV-1900", false, 480, approval: true, max: 2),
        E("centrifuges", "Центрифуги", "Центрифуга", "Eppendorf 5424|Elmi CM-50", false, 480, max: 2),
        E("ph-meters", "pH-метры", "pH-метр", "Hanna HI98107|Mettler Toledo FiveEasy", true, 1440, max: 4),
        E("lab-scales", "Аналитические весы", "Аналитические весы", "Ohaus Pioneer|Сартогосм ЛВ 210-А", false, 480, max: 3),
        E("pipettes", "Дозаторы", "Набор дозаторов", "Eppendorf Research plus|Thermo Finnpipette", true, 2880, max: 6),
        E("thermostats", "Термостаты", "Термостат", "", false, 480, max: 2),
        E("weather-stations", "Метеостанции", "Метеостанция", "Davis Vantage Pro2|Ecowitt WS2910", true, 4320, max: 1),
        E("gps-receivers", "GPS-приёмники", "GPS-приёмник", "Garmin GPSMAP 66sr|Garmin eTrex 32x", true, 4320, max: 3),
        E("theodolites", "Тахеометры и нивелиры", "Нивелир", "Bosch GOL 26 D|Leica TS07", true, 2880, approval: true, max: 2),
        E("laser-rangefinders", "Лазерные дальномеры", "Лазерный дальномер", "Bosch GLM 50|Leica DISTO D2", true, 2880, max: 4),
        E("radiation-meters", "Дозиметры", "Дозиметр", "Радэкс РД1503|Соэкс 01М", true, 1440, max: 2),
        E("sound-level-meters", "Шумомеры", "Шумомер", "Testo 815|UNI-T UT353", true, 1440, max: 2),
        E("eeg-kits", "Нейрогарнитуры", "Нейрогарнитура", "Muse S|Emotiv EPOC X", true, 1440, approval: true, max: 2),

        // Офис и печать
        E("printers", "Принтеры", "Принтер", "HP LaserJet Pro M404|Brother HL-L2370", false, 480, max: 2, office: true),
        E("scanners", "Сканеры", "Сканер", "Epson Perfection V600|Fujitsu fi-7160", false, 480, max: 2, office: true),
        E("plotters", "Плоттеры", "Плоттер", "HP DesignJet T230|Canon imagePROGRAF TM-200", false, 480, approval: true, max: 1),
        E("laminators", "Ламинаторы", "Ламинатор", "Fellowes Saturn 3i", false, 480, max: 2, office: true),
        E("binders", "Брошюровщики", "Брошюровщик", "Fellowes Pulsar", false, 480, max: 2, office: true),
        E("paper-cutters", "Резаки для бумаги", "Резак для бумаги", "", false, 480, max: 2, office: true),
        E("label-printers", "Принтеры этикеток", "Принтер этикеток", "Brother QL-820|Dymo LabelWriter", true, 1440, max: 2, office: true),

        // Мероприятия
        E("stage-lights", "Сценический свет", "Комплект сценического света", "", false, 480, approval: true, max: 2),
        E("fog-machines", "Генераторы дыма", "Генератор дыма", "", false, 480, approval: true, max: 1),
        E("radios", "Рации", "Рация", "Motorola T82|Baofeng UV-5R", true, 1440, max: 8),
        E("generators", "Электрогенераторы", "Электрогенератор", "Huter DY4000L|Fubag BS 3300", true, 1440, approval: true, max: 1),
        E("tents", "Шатры и палатки", "Шатёр", "", true, 4320, approval: true, max: 2),
        E("folding-tables", "Складные столы", "Складной стол", "", true, 2880, max: 8),
        E("banner-stands", "Ролл-апы", "Ролл-ап", "", true, 2880, max: 4, office: true),
        E("coffee-machines", "Кофемашины для мероприятий", "Кофемашина", "De'Longhi Magnifica|Philips 3200", false, 480, max: 1, office: true),

        // Спорт и туризм
        E("bicycles", "Велосипеды", "Велосипед", "Merida Big.Nine|Stels Navigator", true, 1440, max: 4),
        E("scooters", "Электросамокаты", "Электросамокат", "Ninebot Max G30|Xiaomi Mi 4 Pro", true, 1440, max: 4),
        E("ski-sets", "Лыжи", "Комплект лыж", "", true, 4320, max: 6),
        E("snowboards", "Сноуборды", "Сноуборд", "", true, 4320, max: 3),
        E("ice-skates", "Коньки", "Коньки", "", true, 1440, max: 8),
        E("camping-tents", "Туристические палатки", "Туристическая палатка", "Tramp Lite|Naturehike Cloud Up", true, 4320, max: 4),
        E("sleeping-bags", "Спальники", "Спальник", "", true, 4320, max: 6),
        E("backpacks", "Туристические рюкзаки", "Рюкзак", "Deuter Aircontact|Tatonka Yukon", true, 4320, max: 5),
        E("balls", "Мячи", "Набор мячей", "", true, 1440, max: 4),
        E("table-tennis", "Наборы для настольного тенниса", "Набор для настольного тенниса", "", true, 1440, max: 4),
        E("board-games", "Настольные игры", "Настольная игра", "Каркассон|Колонизаторы|Кодовые имена|Диксит|Манчкин", true, 2880, max: 8),
        E("fitness-gear", "Фитнес-инвентарь", "Набор фитнес-инвентаря", "", true, 1440, max: 4),
    ];

    public static readonly string[] Topics =
    [
        "Подготовка к защите проекта", "Консультация по курсовой", "Встреча студсовета", "Семинар по базам данных",
        "Хакатон: подготовка", "Репетиция выступления", "Собеседование", "Планёрка команды", "Ретроспектива спринта",
        "Съёмка видео для конкурса", "Запись подкаста", "Лабораторная работа", "Занятие кружка робототехники", "Защита практики",
        "Встреча с заказчиком", "Мастер-класс", "Учебная съёмка", "Подготовка к олимпиаде", "Тренировка команды КВН",
        "Разбор домашнего задания", "Демо-день стартапов", "Созвон с партнёрами", "Печать прототипа", "Экскурсия для абитуриентов",
        "Репетиция хора", "Заседание кафедры", "Работа над диссертацией", "Встреча научного кружка",
    ];
}
