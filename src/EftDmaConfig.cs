/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.ColorPicker;
using LoneEftDmaRadar.UI.Data;
using System.Collections.ObjectModel;
using VmmSharpEx.Extensions.Input;

namespace LoneEftDmaRadar
{
    /// <summary>
    /// Global Program Configuration (Config.json)
    /// </summary>
    public sealed class EftDmaConfig
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public EftDmaConfig() { }

        #region Config Sections

        [JsonPropertyName("dma")]
        [JsonInclude]
        public DMAConfig DMA { get; private set; } = new();

        [JsonPropertyName("profileApi")]
        [JsonInclude]
        public ProfileApiConfig ProfileApi { get; private set; } = new();

        [JsonPropertyName("twitchApi")]
        [JsonInclude]
        public TwitchApiConfig TwitchApi { get; private set; } = new();

        [JsonPropertyName("ui")]
        [JsonInclude]
        public UIConfig UI { get; private set; } = new();

        [JsonPropertyName("webRadar")]
        [JsonInclude]
        public WebRadarConfig WebRadar { get; private set; } = new();

        [JsonPropertyName("loot")]
        [JsonInclude]
        public LootConfig Loot { get; private set; } = new LootConfig();

        [JsonPropertyName("containers")]
        [JsonInclude]
        public ContainersConfig Containers { get; private set; } = new();

        [JsonPropertyName("hotkeys_v2")]
        [JsonInclude]
        public ConcurrentDictionary<Win32VirtualKey, string> Hotkeys { get; private set; } = new();

        [JsonPropertyName("hotkeyInputMode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HotkeyInputMode HotkeyInputMode { get; set; } = HotkeyInputMode.RadarPC;

        [JsonPropertyName("radarColors")]
        [JsonConverter(typeof(ColorDictionaryConverter))]
        [JsonInclude]
        public ConcurrentDictionary<ColorPickerOption, string> RadarColors { get; private set; } = new();

        [JsonInclude]
        [JsonPropertyName("aimviewWidget")]
        public AimviewWidgetConfig AimviewWidget { get; private set; } = new();

        [JsonInclude]
        [JsonPropertyName("infoWidget")]
        public InfoWidgetConfig InfoWidget { get; private set; } = new();

        [JsonInclude]
        [JsonPropertyName("lootInfoWidget")]
        public LootInfoWidgetConfig LootInfoWidget { get; private set; } = new();

        [JsonPropertyName("device")]
        [JsonInclude]
        public DeviceAimbotConfig Device { get; private set; } = new();

        [JsonPropertyName("memWrites")]
        [JsonInclude]
        public MemWritesConfig MemWrites { get; private set; } = new();

        [JsonInclude]
        [JsonPropertyName("playerWatchlist")]
        public ObservableCollection<PlayerWatchlistEntry> PlayerWatchlist { get; private set; } = CreateDefaultWatchlist();

        [JsonInclude]
        [JsonPropertyName("lootFilters")]
        public LootFilterConfig LootFilters { get; private set; } = new();

        [JsonPropertyName("questHelper")]
        [JsonInclude]
        public QuestHelperConfig QuestHelper { get; private set; } = new();

        #endregion

        #region Config Interface

        [JsonIgnore]
        internal const string Filename = "Config-EFT.json";

        [JsonIgnore]
        private static readonly Lock _syncRoot = new();

        [JsonIgnore]
        private static readonly FileInfo _configFile = new(Path.Combine(App.ConfigPath.FullName, Filename));

        [JsonIgnore]
        private static readonly FileInfo _tempFile = new(Path.Combine(App.ConfigPath.FullName, Filename + ".tmp"));

        [JsonIgnore]
        private static readonly FileInfo _backupFile = new(Path.Combine(App.ConfigPath.FullName, Filename + ".bak"));

        public static EftDmaConfig Load()
        {
            EftDmaConfig config;
            lock (_syncRoot)
            {
                App.ConfigPath.Create();
                if (_configFile.Exists)
                {
                    config = TryLoad(_tempFile) ??
                        TryLoad(_configFile) ??
                        TryLoad(_backupFile);

                    if (config is null)
                    {
                        var dlg = MessageBox.Show(
                            "Config File Corruption Detected! If you backed up your config, you may attempt to restore it.\n" +
                            "Press OK to Reset Config and continue startup, or CANCEL to terminate program.",
                            App.Name,
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Error);
                        if (dlg == MessageBoxResult.Cancel)
                            Environment.Exit(0);
                        config = new EftDmaConfig();
                        SaveInternal(config);
                    }
                }
                else
                {
                    config = new();
                    SaveInternal(config);
                }

                return config;
            }
        }

        private static EftDmaConfig TryLoad(FileInfo file)
        {
            try
            {
                if (!file.Exists)
                    return null;
                string json = File.ReadAllText(file.FullName);
                return JsonSerializer.Deserialize<EftDmaConfig>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void Save()
        {
            lock (_syncRoot)
            {
                try
                {
                    SaveInternal(this);
                }
                catch (Exception ex)
                {
                    throw new IOException($"ERROR Saving Config: {ex.Message}", ex);
                }
            }
        }

        public async Task SaveAsync() => await Task.Run(Save);

        private static void SaveInternal(EftDmaConfig config)
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            using (var fs = new FileStream(
                _tempFile.FullName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(json);
                sw.Flush();
                fs.Flush(flushToDisk: true);
            }
            if (_configFile.Exists)
            {
                File.Replace(
                    sourceFileName: _tempFile.FullName,
                    destinationFileName: _configFile.FullName,
                    destinationBackupFileName: _backupFile.FullName,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Copy(
                    sourceFileName: _tempFile.FullName,
                    destFileName: _backupFile.FullName);
                File.Move(
                    sourceFileName: _tempFile.FullName,
                    destFileName: _configFile.FullName);
            }
        }

        #endregion

        #region Default Watchlist

        private static ObservableCollection<PlayerWatchlistEntry> CreateDefaultWatchlist()
        {
            return new ObservableCollection<PlayerWatchlistEntry>
            {
                new() { AcctID = "2403694", Reason = "twitch/donpscelli_", Timestamp = DateTime.Now },
                new() { AcctID = "152977", Reason = "twitch/HONEYxxo", Timestamp = DateTime.Now },
                new() { AcctID = "835112", Reason = "twitch/lvndmark", Timestamp = DateTime.Now },
                new() { AcctID = "376689", Reason = "twitch/summit1g", Timestamp = DateTime.Now },
                new() { AcctID = "11387881", Reason = "twitch/tigz", Timestamp = DateTime.Now },
                new() { AcctID = "1049972", Reason = "twitch/hutchmf", Timestamp = DateTime.Now },
                new() { AcctID = "2989797", Reason = "twitch/viibiin", Timestamp = DateTime.Now },
                new() { AcctID = "354136", Reason = "twitch/klean", Timestamp = DateTime.Now },
                new() { AcctID = "1717857", Reason = "twitch/jessekazam", Timestamp = DateTime.Now },
                new() { AcctID = "3391828", Reason = "twitch/nyxia", Timestamp = DateTime.Now },
                new() { AcctID = "2438239", Reason = "twitch/xblazed", Timestamp = DateTime.Now },
                new() { AcctID = "831617", Reason = "twitch/velion", Timestamp = DateTime.Now },
                new() { AcctID = "3526004", Reason = "twitch/gingy", Timestamp = DateTime.Now },
                new() { AcctID = "4637816", Reason = "twitch/trey24k", Timestamp = DateTime.Now },
                new() { AcctID = "1663669", Reason = "twitch/desmondpilak", Timestamp = DateTime.Now },
                new() { AcctID = "763945", Reason = "twitch/aquafps", Timestamp = DateTime.Now },
                new() { AcctID = "2111653", Reason = "twitch/bakeezy", Timestamp = DateTime.Now },
                new() { AcctID = "2095752", Reason = "twitch/blueberrygabi", Timestamp = DateTime.Now },
                new() { AcctID = "2165308", Reason = "twitch/smittystone", Timestamp = DateTime.Now },
                new() { AcctID = "1153634", Reason = "twitch/2thy", Timestamp = DateTime.Now },
                new() { AcctID = "3982736", Reason = "twitch/gl40labsrat", Timestamp = DateTime.Now },
                new() { AcctID = "971133", Reason = "twitch/rengawr", Timestamp = DateTime.Now },
                new() { AcctID = "4897027", Reason = "twitch/annemunition", Timestamp = DateTime.Now },
                new() { AcctID = "9347828", Reason = "twitch/honeyxxo", Timestamp = DateTime.Now },
                new() { AcctID = "2058310", Reason = "twitch/moman", Timestamp = DateTime.Now },
                new() { AcctID = "3424723", Reason = "twitch/binoia", Timestamp = DateTime.Now },
                new() { AcctID = "5006226", Reason = "twitch/cooldee__", Timestamp = DateTime.Now },
                new() { AcctID = "4609337", Reason = "twitch/ponch", Timestamp = DateTime.Now },
                new() { AcctID = "927745", Reason = "twitch/goes", Timestamp = DateTime.Now },
                new() { AcctID = "4764608", Reason = "twitch/tobytwofaced", Timestamp = DateTime.Now },
                new() { AcctID = "2043138", Reason = "twitch/kkersanovtv", Timestamp = DateTime.Now },
                new() { AcctID = "8484894", Reason = "twitch/nogenerals", Timestamp = DateTime.Now },
                new() { AcctID = "1294950", Reason = "twitch/wildez", Timestamp = DateTime.Now },
                new() { AcctID = "1942597", Reason = "twitch/cwis0r", Timestamp = DateTime.Now },
                new() { AcctID = "2334119", Reason = "twitch/jaybaybay", Timestamp = DateTime.Now },
                new() { AcctID = "6541088", Reason = "twitch/shoes", Timestamp = DateTime.Now },
                new() { AcctID = "654070", Reason = "twitch/cryodrollic", Timestamp = DateTime.Now },
                new() { AcctID = "2250762", Reason = "twitch/mismagpie", Timestamp = DateTime.Now },
                new() { AcctID = "3351793", Reason = "twitch/nohelmetchad", Timestamp = DateTime.Now },
                new() { AcctID = "5158172", Reason = "twitch/undeadessence", Timestamp = DateTime.Now },
                new() { AcctID = "9351502", Reason = "twitch/burgaofps", Timestamp = DateTime.Now },
                new() { AcctID = "4168016", Reason = "twitch/endra", Timestamp = DateTime.Now },
                new() { AcctID = "739353", Reason = "twitch/knueppelpaste", Timestamp = DateTime.Now },
                new() { AcctID = "1312997", Reason = "twitch/vonza", Timestamp = DateTime.Now },
                new() { AcctID = "2739217", Reason = "twitch/volayethor", Timestamp = DateTime.Now },
                new() { AcctID = "3400742", Reason = "twitch/fudgexl", Timestamp = DateTime.Now },
                new() { AcctID = "2763053", Reason = "twitch/mzdunk", Timestamp = DateTime.Now },
                new() { AcctID = "2329796", Reason = "twitch/philbo", Timestamp = DateTime.Now },
                new() { AcctID = "1758499", Reason = "twitch/someman", Timestamp = DateTime.Now },
                new() { AcctID = "859833", Reason = "twitch/baxbeast", Timestamp = DateTime.Now },
                new() { AcctID = "766970", Reason = "twitch/genooo", Timestamp = DateTime.Now },
                new() { AcctID = "2773520", Reason = "twitch/skidohunter", Timestamp = DateTime.Now },
                new() { AcctID = "2554678", Reason = "twitch/rileyarmageddon", Timestamp = DateTime.Now },
                new() { AcctID = "3998491", Reason = "twitch/kongstyle101", Timestamp = DateTime.Now },
                new() { AcctID = "3569522", Reason = "twitch/realkraftyy", Timestamp = DateTime.Now },
                new() { AcctID = "5550265", Reason = "twitch/tomrander", Timestamp = DateTime.Now },
                new() { AcctID = "2991546", Reason = "twitch/smol", Timestamp = DateTime.Now },
                new() { AcctID = "2673247", Reason = "twitch/shotsofvaca_", Timestamp = DateTime.Now },
                new() { AcctID = "1632126", Reason = "twitch/wenotrat", Timestamp = DateTime.Now },
                new() { AcctID = "2755056", Reason = "twitch/valarman", Timestamp = DateTime.Now },
                new() { AcctID = "4825441", Reason = "twitch/doubledstroyer", Timestamp = DateTime.Now },
                new() { AcctID = "5311265", Reason = "twitch/vazquez66", Timestamp = DateTime.Now },
                new() { AcctID = "10799845", Reason = "twitch/ashnue", Timestamp = DateTime.Now },
                new() { AcctID = "7225268", Reason = "twitch/crylixblooom", Timestamp = DateTime.Now },
                new() { AcctID = "1712951", Reason = "twitch/mvze_", Timestamp = DateTime.Now },
                new() { AcctID = "4194405", Reason = "twitch/shwiftyfps", Timestamp = DateTime.Now },
                new() { AcctID = "8336334", Reason = "twitch/swirrrly", Timestamp = DateTime.Now },
                new() { AcctID = "885958", Reason = "twitch/switch360tv", Timestamp = DateTime.Now },
                new() { AcctID = "5711540", Reason = "twitch/jewlee", Timestamp = DateTime.Now },
                new() { AcctID = "6567825", Reason = "twitch/strongeo", Timestamp = DateTime.Now },
                new() { AcctID = "926010", Reason = "twitch/toastracktv", Timestamp = DateTime.Now },
                new() { AcctID = "851122", Reason = "twitch/cocaoo_", Timestamp = DateTime.Now },
                new() { AcctID = "4034904", Reason = "twitch/verybadscav", Timestamp = DateTime.Now },
                new() { AcctID = "2277116", Reason = "twitch/imbobby__", Timestamp = DateTime.Now },
                new() { AcctID = "3042051", Reason = "twitch/wardell", Timestamp = DateTime.Now },
                new() { AcctID = "2031346", Reason = "twitch/maza4kst", Timestamp = DateTime.Now },
                new() { AcctID = "39632", Reason = "twitch/jimpanse", Timestamp = DateTime.Now },
                new() { AcctID = "10480940", Reason = "twitch/chi_chaan", Timestamp = DateTime.Now },
                new() { AcctID = "3515629", Reason = "twitch/daskicosin", Timestamp = DateTime.Now },
                new() { AcctID = "2207216", Reason = "twitch/logicalsolutions", Timestamp = DateTime.Now },
                new() { AcctID = "2971732", Reason = "twitch/myst1s", Timestamp = DateTime.Now },
                new() { AcctID = "2592389", Reason = "twitch/pixel8_ttv", Timestamp = DateTime.Now },
                new() { AcctID = "1827749", Reason = "twitch/applebr1nger", Timestamp = DateTime.Now },
                new() { AcctID = "6170674", Reason = "twitch/wo1f_gg", Timestamp = DateTime.Now },
                new() { AcctID = "3330252", Reason = "twitch/blinge1", Timestamp = DateTime.Now },
                new() { AcctID = "4544185", Reason = "twitch/impatiya", Timestamp = DateTime.Now },
                new() { AcctID = "5602537", Reason = "twitch/schmidttyb", Timestamp = DateTime.Now },
                new() { AcctID = "1126512", Reason = "twitch/torkie", Timestamp = DateTime.Now },
                new() { AcctID = "1526877", Reason = "twitch/trentau", Timestamp = DateTime.Now },
                new() { AcctID = "3581557", Reason = "twitch/tqmo__", Timestamp = DateTime.Now },
                new() { AcctID = "7706088", Reason = "twitch/gilltex", Timestamp = DateTime.Now },
                new() { AcctID = "1002256", Reason = "twitch/wondows", Timestamp = DateTime.Now },
                new() { AcctID = "7674224", Reason = "twitch/cujoman", Timestamp = DateTime.Now },
                new() { AcctID = "1161451", Reason = "twitch/gerysenior", Timestamp = DateTime.Now },
                new() { AcctID = "922156", Reason = "twitch/hadess31", Timestamp = DateTime.Now },
                new() { AcctID = "11468155", Reason = "twitch/butecodofranco", Timestamp = DateTime.Now },
                new() { AcctID = "11013668", Reason = "twitch/joeliain2310", Timestamp = DateTime.Now },
                new() { AcctID = "11118058", Reason = "twitch/moonshinefps", Timestamp = DateTime.Now },
                new() { AcctID = "3118179", Reason = "twitch/soultura86", Timestamp = DateTime.Now },
                new() { AcctID = "8115752", Reason = "twitch/renalakec", Timestamp = DateTime.Now },
                new() { AcctID = "7085963", Reason = "twitch/notoriouspdx", Timestamp = DateTime.Now },
                new() { AcctID = "3047477", Reason = "twitch/strngerping", Timestamp = DateTime.Now },
                new() { AcctID = "10959843", Reason = "twitch/ry784", Timestamp = DateTime.Now },
                new() { AcctID = "5646257", Reason = "twitch/mushamaru_", Timestamp = DateTime.Now },
                new() { AcctID = "3539914", Reason = "twitch/rguardian", Timestamp = DateTime.Now },
                new() { AcctID = "5463289", Reason = "twitch/wabrat", Timestamp = DateTime.Now },
                new() { AcctID = "839191", Reason = "twitch/notechniquetv", Timestamp = DateTime.Now },
                new() { AcctID = "7104272", Reason = "twitch/fiathegemini", Timestamp = DateTime.Now },
                new() { AcctID = "9827614", Reason = "twitch/codex011", Timestamp = DateTime.Now },
                new() { AcctID = "5051655", Reason = "twitch/dkaye23", Timestamp = DateTime.Now },
                new() { AcctID = "8788838", Reason = "twitch/mrbubblyttv", Timestamp = DateTime.Now },
                new() { AcctID = "2799174", Reason = "twitch/sweetyboom", Timestamp = DateTime.Now },
                new() { AcctID = "5308968", Reason = "twitch/oggyshoggy", Timestamp = DateTime.Now },
                new() { AcctID = "427222", Reason = "twitch/steeyo", Timestamp = DateTime.Now },
                new() { AcctID = "1481309", Reason = "twitch/anton", Timestamp = DateTime.Now },
                new() { AcctID = "364768", Reason = "twitch/hayz", Timestamp = DateTime.Now },
                new() { AcctID = "4411189", Reason = "twitch/hayz (hc)", Timestamp = DateTime.Now },
                new() { AcctID = "5353635", Reason = "twitch/stankrat", Timestamp = DateTime.Now },
                new() { AcctID = "2614961", Reason = "twitch/oberst0m", Timestamp = DateTime.Now },
                new() { AcctID = "6815534", Reason = "twitch/thatfriendlyguy", Timestamp = DateTime.Now },
                new() { AcctID = "3441806", Reason = "twitch/JohnBBeta", Timestamp = DateTime.Now },
                new() { AcctID = "2238335", Reason = "twitch/zchum", Timestamp = DateTime.Now },
                new() { AcctID = "8016990", Reason = "twitch/mistofhazmat", Timestamp = DateTime.Now },
                new() { AcctID = "858816", Reason = "twitch/hipperpyah", Timestamp = DateTime.Now },
                new() { AcctID = "380648", Reason = "twitch/sektenspinner", Timestamp = DateTime.Now },
                new() { AcctID = "408825", Reason = "twitch/bubbinger", Timestamp = DateTime.Now },
                new() { AcctID = "2215415", Reason = "twitch/raggelton", Timestamp = DateTime.Now },
                new() { AcctID = "2693789", Reason = "twitch/zcritic", Timestamp = DateTime.Now },
                new() { AcctID = "9283718", Reason = "twitch/triple_g", Timestamp = DateTime.Now },
                new() { AcctID = "546813", Reason = "twitch/pepp", Timestamp = DateTime.Now },
                new() { AcctID = "4432653", Reason = "twitch/hexloom", Timestamp = DateTime.Now },
                new() { AcctID = "9826933", Reason = "twitch/satsuki_hotaru", Timestamp = DateTime.Now },
                new() { AcctID = "2699481", Reason = "twitch/headleyy", Timestamp = DateTime.Now },
                new() { AcctID = "2366827", Reason = "twitch/thomaspaste", Timestamp = DateTime.Now },
                new() { AcctID = "1699605", Reason = "twitch/taxfree_", Timestamp = DateTime.Now },
                new() { AcctID = "5378845", Reason = "twitch/thePridgeTTV", Timestamp = DateTime.Now },
                new() { AcctID = "564115", Reason = "twitch/ghostfreak66", Timestamp = DateTime.Now },
                new() { AcctID = "5817655", Reason = "twitch/engineergod", Timestamp = DateTime.Now },
                new() { AcctID = "479729", Reason = "twitch/WaitImCheating", Timestamp = DateTime.Now },
                new() { AcctID = "860017", Reason = "twitch/Baddie", Timestamp = DateTime.Now },
                new() { AcctID = "137994", Reason = "twitch/BaudT", Timestamp = DateTime.Now },
                new() { AcctID = "11351038", Reason = "twitch/thruststv", Timestamp = DateTime.Now },
                new() { AcctID = "8381705", Reason = "twitch/cubFPS", Timestamp = DateTime.Now },
                new() { AcctID = "2997948", Reason = "twitch/sheefgg", Timestamp = DateTime.Now },
                new() { AcctID = "4011779", Reason = "twitch/bearki", Timestamp = DateTime.Now },
                new() { AcctID = "2011844", Reason = "twitch/jonk", Timestamp = DateTime.Now },
                new() { AcctID = "5975690", Reason = "twitch/smojii", Timestamp = DateTime.Now },
                new() { AcctID = "165994", Reason = "twitch/willerz", Timestamp = DateTime.Now },
                new() { AcctID = "641616", Reason = "twitch/pestily", Timestamp = DateTime.Now },
                new() { AcctID = "1090448", Reason = "youtube/@DrLupo", Timestamp = DateTime.Now },
                new() { AcctID = "1448970", Reason = "twitch/glorious_e", Timestamp = DateTime.Now },
                new() { AcctID = "1080203", Reason = "twitch/hyperrattv", Timestamp = DateTime.Now },
                new() { AcctID = "1425172", Reason = "twitch/axel_tv", Timestamp = DateTime.Now },
                new() { AcctID = "3928278", Reason = "twitch/aims", Timestamp = DateTime.Now },
                new() { AcctID = "740807", Reason = "twitch/b_komhate", Timestamp = DateTime.Now },
                new() { AcctID = "923361", Reason = "twitch/swid", Timestamp = DateTime.Now },
                new() { AcctID = "790774", Reason = "twitch/thepoolshark", Timestamp = DateTime.Now },
                new() { AcctID = "417327", Reason = "twitch/wishyvt", Timestamp = DateTime.Now },
                new() { AcctID = "10769222", Reason = "twitch/oimatewtf", Timestamp = DateTime.Now },
                new() { AcctID = "8711935", Reason = "twitch/snok3z", Timestamp = DateTime.Now },
                new() { AcctID = "11404544", Reason = "twitch/suddenly_toast", Timestamp = DateTime.Now },
                new() { AcctID = "9052642", Reason = "twitch/mogu_vtuber", Timestamp = DateTime.Now },
                new() { AcctID = "5225682", Reason = "twitch/beibei69", Timestamp = DateTime.Now },
                new() { AcctID = "1284057", Reason = "twitch/dobbykillstreak", Timestamp = DateTime.Now },
                new() { AcctID = "11024278", Reason = "twitch/yago0795", Timestamp = DateTime.Now },
                new() { AcctID = "2536192", Reason = "youtube/@Airwingmarine", Timestamp = DateTime.Now },
            };
        }

        #endregion
    }
}
