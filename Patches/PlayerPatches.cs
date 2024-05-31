using System.Collections.Generic;
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
            if (!Player.m_localPlayer.IsTeleportable() && Hearthstone.AllowTeleportWithoutRestriction.Value == Hearthstone.Toggle.Off)
            {
                var itemDatas = Player.m_localPlayer.GetInventory().GetAllItems();
                foreach (ItemDrop.ItemData? invItem in itemDatas.Where(invItem => invItem.m_shared.m_teleportable == false))
                    ItemsPreventingTeleport.Add(Localization.instance.Localize(invItem.m_shared.m_name));
                if (Admin.Enabled)
                {
                    if (Hearthstone.AdminsallowTeleportWithoutRestriction.Value == Hearthstone.Toggle.On)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_admin_bypass_teleport"));
                        TeleportMe();
                        return true;
                    }
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"$msg_noteleport\n{ItemsPreventingTeleport[0]}");
                    return false;
                }

                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"$msg_noteleport\n{ItemsPreventingTeleport[0]}");
                return false;
            }

            TeleportMe();
            return true;
        }
    }
}