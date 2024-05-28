using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Hearthstone
{
    public class PlayerPatches
    {
        private static void TeleportMe()
        {
            PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            var teleportPosition = playerProfile.HaveCustomSpawnPoint()
                ? playerProfile.GetCustomSpawnPoint()
                : playerProfile.GetHomePoint();

            if (!playerProfile.HaveCustomSpawnPoint())
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    "No custom spawn point found; Teleporting to spawn");

            Player.m_localPlayer.TeleportTo(teleportPosition, Player.m_localPlayer.transform.rotation, true);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        public static class HearthConsumePatch
        {
            private static readonly List<string> itemsPreventingTeleport = new();

            private static bool Prefix(ItemDrop.ItemData item)
            {
                if (!item.m_shared.m_name.Contains("item_hearthstone")) return true;
                if (Hearthstone.AllowTeleportWithoutRestriction != null && !Player.m_localPlayer.IsTeleportable() &&
                    !Hearthstone.AllowTeleportWithoutRestriction.Value)
                {
                    var itemDatas = Player.m_localPlayer.GetInventory().GetAllItems();
                    foreach (var invItem in itemDatas.Where(invItem => invItem.m_shared.m_teleportable == false))
                        itemsPreventingTeleport.Add(Localization.instance.Localize(invItem.m_shared.m_name));
                    if (Hearthstone.IsAdmin)
                    {
                        if (Hearthstone.AdminsallowTeleportWithoutRestriction is { Value: true })
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                                "Admin Bypass; Teleporting");
                            TeleportMe();
                            return true;
                        }

                        Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                            $"Teleporting blocked by {itemsPreventingTeleport[0]}");
                        return false;
                    }

                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                        $"Teleporting blocked by {itemsPreventingTeleport[0]}");
                    return false;
                }

                TeleportMe();


                return true;
            }
        }
    }
}