using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AzuMiscPatches;
using HarmonyLib;
using UnityEngine;

namespace Hearthstone.Patches;

public class PlayerPatches
{
    private static void TeleportMe()
    {
        PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
        Vector3 teleportPosition = playerProfile.HaveCustomSpawnPoint()
            ? playerProfile.GetCustomSpawnPoint()
            : playerProfile.GetHomePoint();

        if (!playerProfile.HaveCustomSpawnPoint())
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_teleport_home"));

        Player.m_localPlayer.TeleportTo(teleportPosition, Player.m_localPlayer.transform.rotation, true);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    public static class HearthConsumePatch
    {
        private static readonly List<string> ItemsPreventingTeleport = [];

        private static bool Prefix(ItemDrop.ItemData item)
        {
            if (!item.m_shared.m_name.Contains("item_hearthstone")) return true;

            Player? player = Player.m_localPlayer;

            if (!player.IsTeleportable() && Hearthstone.AllowTeleportWithoutRestriction.Value == Hearthstone.Toggle.Off)
            {
                List<ItemDrop.ItemData>? itemDatas = player.GetInventory().GetAllItems();
                foreach (ItemDrop.ItemData? invItem in itemDatas.Where(invItem => invItem.m_shared.m_teleportable == false))
                    ItemsPreventingTeleport.Add(Localization.instance.Localize(invItem.m_shared.m_name));

                if (Admin.Enabled && Hearthstone.AdminsallowTeleportWithoutRestriction.Value == Hearthstone.Toggle.On)
                {
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_admin_bypass_teleport"));
                    TeleportMe();
                    return true;
                }

                player.Message(MessageHud.MessageType.Center, $"$msg_noteleport\n{ItemsPreventingTeleport[0]}");
                return false;
            }

            if (!player.m_customData.TryGetValue("HearthstoneCooldown", out string? cooldownTime))
            {
                // If they don't have a value, set it to now
                cooldownTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                player.m_customData["HearthstoneCooldown"] = cooldownTime;
            }

            if (!DateTime.TryParse(cooldownTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime cdTime))
            {
                Hearthstone.HearthLogger.LogError($"Failed to parse cooldown time '{cooldownTime}'");
                return false;
            }

            if ((DateTime.Now < cdTime) && Hearthstone.AllowTeleportWithoutRestriction.Value == Hearthstone.Toggle.Off)
            {
                // Get how much time is left in the cooldown
                TimeSpan timeLeft = cdTime - DateTime.Now;
                string timeLeftMessage = $"{Localization.instance.Localize("$msg_teleport_cooldown")}\n";
                if (timeLeft.Hours > 0)
                {
                    timeLeftMessage += $"{timeLeft.Hours}h ";
                }

                timeLeftMessage += $"{timeLeft.Minutes}m {timeLeft.Seconds}s";
                player.Message(MessageHud.MessageType.Center, timeLeftMessage);

                return false;
            }

            player.m_customData["HearthstoneCooldown"] = DateTime.Now.AddSeconds(Hearthstone.Cooldown.Value).ToString(CultureInfo.InvariantCulture);
            TeleportMe();

            return true;
        }
    }
}