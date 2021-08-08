using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace Hearthstone
{
    [BepInPlugin("Detalhes.Hearthstone", "Hearthstone", ModVersion)]
    public class Hearthstone : BaseUnityPlugin
    {
        private const string PluginGUID = "Detalhes.Hearthstone";
        private const string Author = "Detalhes";
        public const string ModVersion = "1.0.0";
        public static bool IsAdmin = false;
        private static GameObject? _hearth;
        private static Recipe? _recipe;
        public static readonly ManualLogSource HearthLogger = BepInEx.Logging.Logger.CreateLogSource(PluginGUID);
        public bool UpdateRecipe;

        private readonly Harmony harmony = new(PluginGUID);


        private void Awake()
        {
            /* Localization file creation. File name will be Detalhes.Hearthstone.Localization.cfg */
            LocalizationFile =
                new ConfigFile(
                    Path.Combine(Path.GetDirectoryName(Config.ConfigFilePath)!, PluginGUID + ".Localization.cfg"),
                    false);
            /* Config Options */
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            configSync.AddLockingConfigEntry(_serverConfigLocked);
            _nexusId = config("General", "NexusID", 1417,
                new ConfigDescription("Nexus mod ID for updates", null, new ConfigurationManagerAttributes()), false);

            AllowTeleportWithoutRestriction = Config.Bind("General", "allowTeleportWithoutRestriction", false,
                "Allow teleport without restriction");
            AdminsallowTeleportWithoutRestriction = Config.Bind("General", "AdminTeleportWithoutRestriction", true,
                "Admins teleport without restriction");

            /* Item 1 */
            _req1Prefab = itemConfig("Item 1", "Required Prefab", "Coins", "Required item for crafting");
            _req1Amount = itemConfig("Item 1", "Amount Required", 30, "Amount needed of this item for crafting");
            _req1Apl = itemConfig("Item 1", "Amount Per Level", 10,
                "Amount to increase crafting cost by for each level of the item");

            /* Item 2 */
            _req2Prefab = itemConfig("Item 2", "Required Prefab", "Resin", "Required item for crafting");
            _req2Amount = itemConfig("Item 2", "Amount Required", 10, "Amount needed of this item for crafting");
            _req2Apl = itemConfig("Item 2", "Amount Per Level", 10,
                "Amount to increase crafting cost by for each level of the item");

            /* Item 3 */
            _req3Prefab = itemConfig("Item 3", "Required Prefab", "BoneFragments", "Required item for crafting");
            _req3Amount = itemConfig("Item 3", "Amount Required", 10, "Amount needed of this item for crafting");
            _req3Apl = itemConfig("Item 3", "Amount Per Level", 1,
                "Amount to increase crafting cost by for each level of the item");


            ConfigEntry<T> itemConfig<T>(string item, string name, T value, string description)
            {
                ConfigEntry<T> configEntry = config("Recipe " + item, name, value, description);
                configEntry.SettingChanged += (s, e) => UpdateRecipe = true;
                return configEntry;
            }

            harmony.PatchAll();
            MethodInfo methodInfo = AccessTools.Method(typeof(ZNet), "RPC_CharacterID",
                new[] { typeof(ZRpc), typeof(ZDOID) });
            harmony.Patch(methodInfo, null,
                new HarmonyMethod(AccessTools.Method(typeof(HearthstoneAdminGET), "RPC_CharID",
                    new[] { typeof(ZNet), typeof(ZRpc) })));
            LocalizationDecs.Localize();

            LoadAssets();
        }

        private void Update()
        {
            if (!Player.m_localPlayer) return;
            if (UpdateRecipe) HearthRecipe();
            if (!ObjectDB.instance.m_recipes.Contains(_recipe)) ObjectDB.instance.m_recipes.Add(_recipe);
        }

        private void OnDestroy()
        {
            LocalizationFile.Save();
            harmony?.UnpatchSelf();
        }

        private static void TryRegisterFabs(ZNetScene zNetScene)
        {
            if (zNetScene == null || zNetScene.m_prefabs == null || zNetScene.m_prefabs.Count <= 0) return;
            zNetScene.m_prefabs.Add(_hearth);
        }

        private static AssetBundle GetAssetBundleFromResources(string filename)
        {
            var execAssembly = Assembly.GetExecutingAssembly();
            var resourceName = execAssembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(filename));

            using var stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }

        private static void LoadAssets()
        {
            AssetBundle assetBundle = GetAssetBundleFromResources("hearthstone");
            _hearth = assetBundle.LoadAsset<GameObject>("Hearthstone");
            assetBundle.Unload(false);
        }

        private static void RegisterHearth()
        {
            if (ObjectDB.instance.m_items.Count == 0 || ObjectDB.instance.GetItemPrefab("Amber") == null) return;
            var itemDrop = _hearth.GetComponent<ItemDrop>();
            if (itemDrop == null) return;
            if (ObjectDB.instance.GetItemPrefab(_hearth.name.GetStableHashCode()) == null)
                ObjectDB.instance.m_items.Add(_hearth);
        }

        private static void HearthAddRecipe()
        {
            try
            {
                if (!ObjectDB.instance.m_recipes.Any())
                {
                    HearthLogger.Log(LogLevel.Debug, "Recipe database not ready for stuff, skipping initialization.");
                    return;
                }

                HearthRecipe();

                ObjectDB.instance.UpdateItemHashes();
            }
            catch (Exception exc)
            {
                Debug.Log(exc);
            }
        }

        private static void HearthRecipe()
        {
            var db = ObjectDB.instance.m_items;
            try
            {
                db.Remove(_hearth);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error removing Hearthstone from ODB  :{ex}");
            }

            if (_recipe == null) _recipe = ScriptableObject.CreateInstance<Recipe>();
            if (!ObjectDB.instance.m_recipes.Contains(_recipe)) ObjectDB.instance.m_recipes.Add(_recipe);
            GameObject thing1 = ObjectDB.instance.GetItemPrefab(_req1Prefab.Value);
            GameObject thing2 = ObjectDB.instance.GetItemPrefab(_req2Prefab.Value);
            GameObject thing3 = ObjectDB.instance.GetItemPrefab(_req3Prefab.Value);
            _recipe.name = "Recipe_Hearthstone";
            _recipe.m_craftingStation = ZNetScene.instance.GetPrefab("piece_workbench").GetComponent<CraftingStation>();
            _recipe.m_repairStation = ZNetScene.instance.GetPrefab("piece_workbench").GetComponent<CraftingStation>();
            _recipe.m_amount = 1;
            _recipe.m_minStationLevel = 1;
            _recipe.m_item = _hearth.GetComponent<ItemDrop>();
            _recipe.m_enabled = true;
            _recipe.m_resources = new[]
            {
                new()
                {
                    m_resItem = thing1.GetComponent<ItemDrop>(), m_amount = _req1Amount.Value,
                    m_amountPerLevel = _req1Apl.Value, m_recover = true
                },
                new Piece.Requirement
                {
                    m_resItem = thing2.GetComponent<ItemDrop>(), m_amount = _req2Amount.Value,
                    m_amountPerLevel = _req2Apl.Value, m_recover = true
                },
                new Piece.Requirement
                {
                    m_resItem = thing3.GetComponent<ItemDrop>(), m_amount = _req3Amount.Value,
                    m_amountPerLevel = _req3Apl.Value, m_recover = true
                }
            };
            try
            {
                db.Add(_hearth);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error adding Hearthstone to ODB  :{ex}");
            }
        }


        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        public static class HearthZNetScene_Awake_Patch
        {
            public static bool Prefix(ZNetScene __instance)
            {
                TryRegisterFabs(__instance);
                return true;
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        public static class HearthObjectDB_Awake_Patch
        {
            public static void Postfix()
            {
                RegisterHearth();
                HearthAddRecipe();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        public static class HearthObjectDB_CopyOtherDB_Patch
        {
            public static void Postfix()
            {
                RegisterHearth();
                HearthAddRecipe();
            }
        }


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        private static ConfigEntry<int>? _nexusId;
        public static ConfigEntry<bool>? AllowTeleportWithoutRestriction;
        public static ConfigEntry<bool>? AdminsallowTeleportWithoutRestriction;

        /* Localization file declarations */
        public static ConfigFile? LocalizationFile;
        public static readonly Dictionary<string, ConfigEntry<string>> LocalizedStrings = new();

        /* Give users ability to change required items, amounts, and per level reqs */
        private static ConfigEntry<string>? _req1Prefab;
        private static ConfigEntry<string>? _req2Prefab;
        private static ConfigEntry<string>? _req3Prefab;

        private static ConfigEntry<int>? _req1Amount;
        private static ConfigEntry<int>? _req2Amount;
        private static ConfigEntry<int>? _req3Amount;

        private static ConfigEntry<int>? _req1Apl;
        private static ConfigEntry<int>? _req2Apl;
        private static ConfigEntry<int>? _req3Apl;

        private static readonly ConfigSync configSync = new(PluginGUID)
            { DisplayName = "Hearthstone", CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        #endregion
    }
}