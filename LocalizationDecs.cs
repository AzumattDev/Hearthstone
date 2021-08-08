using System;
using HarmonyLib;

namespace Hearthstone
{
    [HarmonyPatch]
    public class LocalizationDecs
    {
        public static void Localize()
        {
            try
            {
                LocalizeWord("item_hearthstone", "Hearthstone");
                LocalizeWord("item_hearthstone_description", "Go back home or spawn if you don't have a bed");
            }
            catch (Exception ex)
            {
                Hearthstone.HearthLogger.LogError($"{ex}");
            }
        }

        private static string LocalizeWord(string key, string val)
        {
            if (Hearthstone.LocalizedStrings.ContainsKey(key)) return $"${key}";
            var loc = Localization.instance;
            var langSection = loc.GetSelectedLanguage();
            var configEntry = Hearthstone.LocalizationFile.Bind(langSection, key, val);
            Localization.instance.AddWord(key, configEntry.Value);
            Hearthstone.LocalizedStrings.Add(key, configEntry);

            return $"${key}";
        }
    }
}