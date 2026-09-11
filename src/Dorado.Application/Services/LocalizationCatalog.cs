namespace Dorado.Application.Services;

/// <summary>
/// In-memory string catalog. Dorado is English-first, so any key missing from a
/// locale falls back to English and then to the key itself (never throws).
/// Expanded beyond en/fr toward the Zune 4.8 locale set; untranslated keys fall
/// back to English rather than blocking.
/// </summary>
public sealed class LocalizationCatalog
{
    public const string DefaultLocale = "en";

    private readonly Dictionary<string, Dictionary<string, string>> _byLocale;

    public LocalizationCatalog(Dictionary<string, Dictionary<string, string>> byLocale, IReadOnlyList<string> locales)
    {
        _byLocale = byLocale;
        Locales = locales;
    }

    public static LocalizationCatalog Default { get; } = CreateDefault();

    public IReadOnlyList<string> Locales { get; }

    public string Get(string key, string locale)
    {
        if (_byLocale.TryGetValue(locale, out var strings) && strings.TryGetValue(key, out var value))
        {
            return value;
        }

        if (_byLocale.TryGetValue(DefaultLocale, out var fallback) && fallback.TryGetValue(key, out var english))
        {
            return english;
        }

        return key;
    }

    public IReadOnlyDictionary<string, string> StringsFor(string locale)
        => _byLocale.TryGetValue(locale, out var strings) ? strings : new Dictionary<string, string>();

    private static LocalizationCatalog CreateDefault()
    {
        var en = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pivot.software"] = "SOFTWARE",
            ["pivot.device"] = "DEVICE",
            ["sub.collection"] = "collection",
            ["sub.playback"] = "playback",
            ["sub.podcasts"] = "podcasts",
            ["sub.filetypes"] = "file types",
            ["sub.privacy"] = "privacy",
            ["sub.photos"] = "photos",
            ["sub.rip"] = "rip",
            ["sub.burn"] = "burn",
            ["sub.metadata"] = "metadata",
            ["sub.display"] = "display",
            ["sub.general"] = "general",
            ["sub.about"] = "about",
            ["sub.plugins"] = "plugins",
            // Language display names (used by the Settings language selector).
            ["language.en"] = "English",
            ["language.fr"] = "Français",
            ["language.de"] = "Deutsch",
            ["language.es"] = "Español",
            ["language.it"] = "Italiano",
            ["language.pt"] = "Português",
            ["language.nl"] = "Nederlands",
            ["language.sv"] = "Svenska",
            ["language.da"] = "Dansk",
            ["language.nb"] = "Norsk",
            ["language.fi"] = "Suomi",
            ["language.pl"] = "Polski",
            ["language.cs"] = "Čeština",
            ["language.hu"] = "Magyar",
            ["language.tr"] = "Türkçe",
            ["language.ru"] = "Русский",
            ["language.ja"] = "日本語",
            ["language.ko"] = "한국어",
            ["language.zh-Hans"] = "简体中文",
            ["language.zh-Hant"] = "繁體中文",
            ["general.language"] = "LANGUAGE"
        };

        var locales = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = en,
            ["fr"] = Locale("fr", "Français", new()
            {
                ["pivot.software"] = "LOGICIEL",
                ["pivot.device"] = "APPAREIL",
                ["sub.playback"] = "lecture",
                ["sub.filetypes"] = "types de fichiers",
                ["sub.privacy"] = "confidentialité",
                ["sub.rip"] = "extraction",
                ["sub.burn"] = "gravure",
                ["sub.metadata"] = "métadonnées",
                ["sub.display"] = "affichage",
                ["sub.general"] = "général",
                ["sub.about"] = "à propos",
                ["sub.plugins"] = "modules",
                ["general.language"] = "LANGUE",
            }),
            ["de"] = Locale("de", "Deutsch", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "GERÄT",
                ["sub.collection"] = "sammlung",
                ["sub.playback"] = "wiedergabe",
                ["sub.filetypes"] = "dateitypen",
                ["sub.privacy"] = "datenschutz",
                ["sub.photos"] = "fotos",
                ["sub.rip"] = "rippen",
                ["sub.burn"] = "brennen",
                ["sub.metadata"] = "metadaten",
                ["sub.display"] = "anzeige",
                ["sub.general"] = "allgemein",
                ["sub.about"] = "info",
                ["general.language"] = "SPRACHE",
            }),
            ["es"] = Locale("es", "Español", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "DISPOSITIVO",
                ["sub.collection"] = "colección",
                ["sub.playback"] = "reproducción",
                ["sub.podcasts"] = "pódcasts",
                ["sub.filetypes"] = "tipos de archivo",
                ["sub.privacy"] = "privacidad",
                ["sub.photos"] = "fotos",
                ["sub.rip"] = "extraer",
                ["sub.burn"] = "grabar",
                ["sub.metadata"] = "metadatos",
                ["sub.display"] = "pantalla",
                ["sub.general"] = "general",
                ["sub.about"] = "acerca de",
                ["sub.plugins"] = "complementos",
                ["general.language"] = "IDIOMA",
            }),
            ["it"] = Locale("it", "Italiano", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "DISPOSITIVO",
                ["sub.collection"] = "raccolta",
                ["sub.playback"] = "riproduzione",
                ["sub.filetypes"] = "tipi di file",
                ["sub.privacy"] = "privacy",
                ["sub.photos"] = "foto",
                ["sub.rip"] = "estrazione",
                ["sub.burn"] = "masterizzazione",
                ["sub.metadata"] = "metadati",
                ["sub.display"] = "schermo",
                ["sub.general"] = "generale",
                ["sub.about"] = "informazioni",
                ["sub.plugins"] = "plugin",
                ["general.language"] = "LINGUA",
            }),
            ["pt"] = Locale("pt", "Português", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "DISPOSITIVO",
                ["sub.collection"] = "coleção",
                ["sub.playback"] = "reprodução",
                ["sub.filetypes"] = "tipos de ficheiro",
                ["sub.privacy"] = "privacidade",
                ["sub.photos"] = "fotos",
                ["sub.rip"] = "extrair",
                ["sub.burn"] = "gravar",
                ["sub.metadata"] = "metadados",
                ["sub.display"] = "ecrã",
                ["sub.general"] = "geral",
                ["sub.about"] = "acerca de",
                ["sub.plugins"] = "extensões",
                ["general.language"] = "IDIOMA",
            }),
            ["nl"] = Locale("nl", "Nederlands", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "APPARAAT",
                ["sub.collection"] = "verzameling",
                ["sub.playback"] = "afspelen",
                ["sub.filetypes"] = "bestandstypen",
                ["sub.privacy"] = "privacy",
                ["sub.photos"] = "foto's",
                ["sub.rip"] = "rippen",
                ["sub.burn"] = "branden",
                ["sub.metadata"] = "metagegevens",
                ["sub.display"] = "weergave",
                ["sub.general"] = "algemeen",
                ["sub.about"] = "over",
                ["sub.plugins"] = "invoegtoepassingen",
                ["general.language"] = "TAAL",
            }),
            ["sv"] = Locale("sv", "Svenska", new()
            {
                ["pivot.software"] = "PROGRAMVARA",
                ["pivot.device"] = "ENHET",
                ["sub.collection"] = "samling",
                ["sub.playback"] = "uppspelning",
                ["sub.podcasts"] = "poddradio",
                ["sub.filetypes"] = "filtyper",
                ["sub.privacy"] = "integritet",
                ["sub.photos"] = "foton",
                ["sub.rip"] = "rippa",
                ["sub.burn"] = "bränna",
                ["sub.metadata"] = "metadata",
                ["sub.display"] = "visning",
                ["sub.general"] = "allmänt",
                ["sub.about"] = "om",
                ["sub.plugins"] = "plugin-program",
                ["general.language"] = "SPRÅK",
            }),
            ["da"] = Locale("da", "Dansk", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "ENHED",
                ["sub.collection"] = "samling",
                ["sub.playback"] = "afspilning",
                ["sub.filetypes"] = "filtyper",
                ["sub.privacy"] = "fortrolighed",
                ["sub.photos"] = "fotos",
                ["sub.metadata"] = "metadata",
                ["sub.display"] = "skærm",
                ["sub.general"] = "generelt",
                ["sub.about"] = "om",
                ["general.language"] = "SPROG",
            }),
            ["nb"] = Locale("nb", "Norsk", new()
            {
                ["pivot.software"] = "PROGRAMVARE",
                ["pivot.device"] = "ENHET",
                ["sub.collection"] = "samling",
                ["sub.playback"] = "avspilling",
                ["sub.filetypes"] = "filtyper",
                ["sub.privacy"] = "personvern",
                ["sub.photos"] = "bilder",
                ["sub.metadata"] = "metadata",
                ["sub.display"] = "skjerm",
                ["sub.general"] = "generelt",
                ["sub.about"] = "om",
                ["general.language"] = "SPRÅK",
            }),
            ["fi"] = Locale("fi", "Suomi", new()
            {
                ["pivot.software"] = "OHJELMISTO",
                ["pivot.device"] = "LAITE",
                ["sub.collection"] = "kokoelma",
                ["sub.playback"] = "toisto",
                ["sub.filetypes"] = "tiedostotyypit",
                ["sub.privacy"] = "tietosuoja",
                ["sub.photos"] = "kuvat",
                ["sub.metadata"] = "metatiedot",
                ["sub.display"] = "näyttö",
                ["sub.general"] = "yleiset",
                ["sub.about"] = "tietoja",
                ["general.language"] = "KIELI",
            }),
            ["pl"] = Locale("pl", "Polski", new()
            {
                ["pivot.software"] = "OPROGRAMOWANIE",
                ["pivot.device"] = "URZĄDZENIE",
                ["sub.collection"] = "kolekcja",
                ["sub.playback"] = "odtwarzanie",
                ["sub.podcasts"] = "podcasty",
                ["sub.filetypes"] = "typy plików",
                ["sub.privacy"] = "prywatność",
                ["sub.photos"] = "zdjęcia",
                ["sub.rip"] = "zgrywanie",
                ["sub.burn"] = "nagrywanie",
                ["sub.metadata"] = "metadane",
                ["sub.display"] = "ekran",
                ["sub.general"] = "ogólne",
                ["sub.about"] = "informacje",
                ["sub.plugins"] = "wtyczki",
                ["general.language"] = "JĘZYK",
            }),
            ["cs"] = Locale("cs", "Čeština", new()
            {
                ["pivot.software"] = "SOFTWARE",
                ["pivot.device"] = "ZAŘÍZENÍ",
                ["sub.collection"] = "sbírka",
                ["sub.playback"] = "přehrávání",
                ["sub.filetypes"] = "typy souborů",
                ["sub.privacy"] = "soukromí",
                ["sub.photos"] = "fotografie",
                ["sub.metadata"] = "metadata",
                ["sub.display"] = "zobrazení",
                ["sub.general"] = "obecné",
                ["sub.about"] = "o aplikaci",
                ["general.language"] = "JAZYK",
            }),
            ["hu"] = Locale("hu", "Magyar", new()
            {
                ["pivot.software"] = "SZOFTVER",
                ["pivot.device"] = "ESZKÖZ",
                ["sub.collection"] = "gyűjtemény",
                ["sub.playback"] = "lejátszás",
                ["sub.filetypes"] = "fájltípusok",
                ["sub.privacy"] = "adatvédelem",
                ["sub.photos"] = "fényképek",
                ["sub.metadata"] = "metaadatok",
                ["sub.display"] = "megjelenítés",
                ["sub.general"] = "általános",
                ["sub.about"] = "névjegy",
                ["general.language"] = "NYELV",
            }),
            ["tr"] = Locale("tr", "Türkçe", new()
            {
                ["pivot.software"] = "YAZILIM",
                ["pivot.device"] = "AYGIT",
                ["sub.collection"] = "koleksiyon",
                ["sub.playback"] = "oynatma",
                ["sub.filetypes"] = "dosya türleri",
                ["sub.privacy"] = "gizlilik",
                ["sub.photos"] = "fotoğraflar",
                ["sub.metadata"] = "meta veriler",
                ["sub.display"] = "ekran",
                ["sub.general"] = "genel",
                ["sub.about"] = "hakkında",
                ["general.language"] = "DİL",
            }),
            ["ru"] = Locale("ru", "Русский", new()
            {
                ["pivot.software"] = "ПРОГРАММА",
                ["pivot.device"] = "УСТРОЙСТВО",
                ["sub.collection"] = "коллекция",
                ["sub.playback"] = "воспроизведение",
                ["sub.podcasts"] = "подкасты",
                ["sub.filetypes"] = "типы файлов",
                ["sub.privacy"] = "конфиденциальность",
                ["sub.photos"] = "фото",
                ["sub.rip"] = "извлечение",
                ["sub.burn"] = "запись",
                ["sub.metadata"] = "метаданные",
                ["sub.display"] = "экран",
                ["sub.general"] = "общие",
                ["sub.about"] = "о программе",
                ["sub.plugins"] = "подключаемые модули",
                ["general.language"] = "ЯЗЫК",
            }),
            ["ja"] = Locale("ja", "日本語", new()
            {
                ["pivot.software"] = "ソフトウェア",
                ["pivot.device"] = "デバイス",
                ["sub.collection"] = "コレクション",
                ["sub.playback"] = "再生",
                ["sub.podcasts"] = "ポッドキャスト",
                ["sub.filetypes"] = "ファイルの種類",
                ["sub.privacy"] = "プライバシー",
                ["sub.photos"] = "写真",
                ["sub.rip"] = "取り込み",
                ["sub.burn"] = "書き込み",
                ["sub.metadata"] = "メタデータ",
                ["sub.display"] = "表示",
                ["sub.general"] = "全般",
                ["sub.about"] = "バージョン情報",
                ["sub.plugins"] = "プラグイン",
                ["general.language"] = "言語",
            }),
            ["ko"] = Locale("ko", "한국어", new()
            {
                ["pivot.software"] = "소프트웨어",
                ["pivot.device"] = "장치",
                ["sub.collection"] = "모음",
                ["sub.playback"] = "재생",
                ["sub.podcasts"] = "팟캐스트",
                ["sub.filetypes"] = "파일 형식",
                ["sub.privacy"] = "개인 정보",
                ["sub.photos"] = "사진",
                ["sub.rip"] = "리핑",
                ["sub.burn"] = "굽기",
                ["sub.metadata"] = "메타데이터",
                ["sub.display"] = "표시",
                ["sub.general"] = "일반",
                ["sub.about"] = "정보",
                ["sub.plugins"] = "플러그인",
                ["general.language"] = "언어",
            }),
            ["zh-Hans"] = Locale("zh-Hans", "简体中文", new()
            {
                ["pivot.software"] = "软件",
                ["pivot.device"] = "设备",
                ["sub.collection"] = "收藏",
                ["sub.playback"] = "播放",
                ["sub.podcasts"] = "播客",
                ["sub.filetypes"] = "文件类型",
                ["sub.privacy"] = "隐私",
                ["sub.photos"] = "照片",
                ["sub.rip"] = "提取",
                ["sub.burn"] = "刻录",
                ["sub.metadata"] = "元数据",
                ["sub.display"] = "显示",
                ["sub.general"] = "常规",
                ["sub.about"] = "关于",
                ["sub.plugins"] = "插件",
                ["general.language"] = "语言",
            }),
            ["zh-Hant"] = Locale("zh-Hant", "繁體中文", new()
            {
                ["pivot.software"] = "軟體",
                ["pivot.device"] = "裝置",
                ["sub.collection"] = "收藏",
                ["sub.playback"] = "播放",
                ["sub.podcasts"] = "播客",
                ["sub.filetypes"] = "檔案類型",
                ["sub.privacy"] = "隱私權",
                ["sub.photos"] = "相片",
                ["sub.rip"] = "擷取",
                ["sub.burn"] = "燒錄",
                ["sub.metadata"] = "中繼資料",
                ["sub.display"] = "顯示",
                ["sub.general"] = "一般",
                ["sub.about"] = "關於",
                ["sub.plugins"] = "外掛程式",
                ["general.language"] = "語言",
            }),
        };

        return new LocalizationCatalog(locales, locales.Keys.ToArray());
    }

    private static Dictionary<string, string> Locale(string code, string displayName, Dictionary<string, string> strings)
    {
        strings["language." + code] = displayName;
        strings["language.en"] = "English";
        return strings;
    }
}
