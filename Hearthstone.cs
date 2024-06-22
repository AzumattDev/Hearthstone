using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;

namespace Hearthstone;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Hearthstone : BaseUnityPlugin
{
    internal const string ModName = "Hearthstone";
    public const string ModVersion = "1.0.2";
    internal const string Author = "Azumatt";
    private const string ModGUID = $"{Author}.{ModName}";
    private static string ConfigFileName = ModGUID + ".cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    public static readonly ManualLogSource HearthLogger = BepInEx.Logging.Logger.CreateLogSource(ModGUID);
    private readonly Harmony _harmony = new(ModGUID);

    public enum Toggle
    {
        On = 1,
        Off = 0,
    }

    private void Awake()
    {
        Localizer.Load();
        bool save = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, new ConfigDescription("If on, the configuration is locked and can be changed by server admins only.", null, new ConfigurationManagerAttributes() { Order = 3 }));
        _ = configSync.AddLockingConfigEntry(_serverConfigLocked);

        AllowTeleportWithoutRestriction = config("1 - General", "AllowTeleportWithoutRestriction", Toggle.Off, "Allow teleport without restriction");
        AdminsallowTeleportWithoutRestriction = config("1 - General", "AdminTeleportWithoutRestriction", Toggle.On, "Admins teleport without restriction");
        Cooldown = config("1 - General", "Cooldown", 7200.0, "Cooldown in seconds, default is 7200 (2 hours)");

        Item hearthStone = new("hearthstone", "Hearthstone");
        hearthStone.Crafting.Add(CraftingTable.Workbench, 2);
        hearthStone.RequiredItems.Add("Coins", 30);
        hearthStone.RequiredItems.Add("Resin", 10);
        hearthStone.RequiredItems.Add("BoneFragments", 10);
        hearthStone.RequiredUpgradeItems.Add("Coins", 5);
        hearthStone.RequiredUpgradeItems.Add("Resin", 5);
        hearthStone.RequiredUpgradeItems.Add("BoneFragments", 5);
        hearthStone.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize = 10;

        _harmony.PatchAll();
        Config.Save();
        Config.SaveOnConfigSet = save;
        SetupWatcher();
    }

    private void OnDestroy()
    {
        Config.Save();
    }

    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            Config.Reload();
        }
        catch
        {
            HearthLogger.LogError($"There was an issue loading your {ConfigFileName}");
            HearthLogger.LogError("Please check your config entries for spelling and format!");
        }
    }

    #region ConfigOptions

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    public static ConfigEntry<Toggle> AllowTeleportWithoutRestriction = null!;
    public static ConfigEntry<Toggle> AdminsallowTeleportWithoutRestriction = null!;
    public static ConfigEntry<double> Cooldown = null!;

    private static readonly ConfigSync configSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order = null!;
        [UsedImplicitly] public bool? Browsable = null!;
        [UsedImplicitly] public string? Category = null!;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
    }

    #endregion
}