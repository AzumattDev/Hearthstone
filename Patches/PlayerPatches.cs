using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Hearthstone.Managers;
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

    private static void ApplyCooldownStatusEffect(Player player, DateTime cooldownEndTime)
    {
        // Remove existing cooldown effect if present
        player.GetSEMan().RemoveStatusEffect("HearthstoneCooldown".GetStableHashCode());
        
        // Apply new cooldown effect
        Hearthstone.cooldownEffect.SetCooldownEndTime(cooldownEndTime);
        
        player.GetSEMan().AddStatusEffect(Hearthstone.cooldownEffect);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    public static class HearthConsumePatch
    {
        private static readonly List<string> ItemsPreventingTeleport = [];

        private static bool Prefix(ItemDrop.ItemData item)
        {
            if (!item.m_shared.m_name.Contains("item_hearthstone")) return true;

            Player? player = Player.m_localPlayer;

            // Check teleportation restrictions (but allow bypass with config)
            if (!player.IsTeleportable() && Hearthstone.AllowTeleportWithoutRestriction.Value == Hearthstone.Toggle.Off)
            {
                List<ItemDrop.ItemData>? itemDatas = player.GetInventory().GetAllItems();
                foreach (ItemDrop.ItemData? invItem in itemDatas.Where(invItem => invItem.m_shared.m_teleportable == false))
                    ItemsPreventingTeleport.Add(Localization.instance.Localize(invItem.m_shared.m_name));

                if (Admin.Enabled && Hearthstone.AdminsallowTeleportWithoutRestriction.Value == Hearthstone.Toggle.On)
                {
                    player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_admin_bypass_teleport"));
                    // Still need to check cooldown even for admins
                    return CheckCooldownAndTeleport(player);
                }

                player.Message(MessageHud.MessageType.Center, $"$msg_noteleport\n{ItemsPreventingTeleport[0]}");
                return false;
            }

            // Check cooldown regardless of AllowTeleportWithoutRestriction setting
            return CheckCooldownAndTeleport(player);
        }

        private static bool CheckCooldownAndTeleport(Player player)
        {
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

            // Check cooldown (only bypass if AllowTeleportWithoutRestriction is On)
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

            // Set new cooldown time
            DateTime newCooldownTime = DateTime.Now.AddSeconds(Hearthstone.Cooldown.Value);
            player.m_customData["HearthstoneCooldown"] = newCooldownTime.ToString(CultureInfo.InvariantCulture);
            
            // Apply status effect to show cooldown
            ApplyCooldownStatusEffect(player, newCooldownTime);
            
            TeleportMe();
            return true;
        }
    }
}

// Custom Status Effect to display cooldown
public class HearthstoneCooldownStatusEffect : StatusEffect
{
    private DateTime cooldownEndTime;
    
    public void SetCooldownEndTime(DateTime endTime)
    {
        cooldownEndTime = endTime;
        m_ttl = (float)(endTime - DateTime.Now).TotalSeconds;
    }

    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        
        TimeSpan timeLeft = cooldownEndTime - DateTime.Now;
        if (timeLeft.TotalSeconds <= 0)
        {
            m_character.GetSEMan().RemoveStatusEffect(this);
            return;
        }

        // Update the tooltip to show remaining time
        string timeLeftText = "";
        if (timeLeft.Hours > 0)
        {
            timeLeftText += $"{timeLeft.Hours}h ";
        }
        timeLeftText += $"{timeLeft.Minutes}m {timeLeft.Seconds}s";
        
        m_tooltip = $"Hearthstone Cooldown\nTime remaining: {timeLeftText}";
    }

    public override string GetTooltipString()
    {
        return m_tooltip;
    }
}

[HarmonyPatch(typeof(ObjectDB),nameof(ObjectDB.Awake))]
static class AddStatusEffectToObjectDBAwakePatch
{
    static void Postfix(ObjectDB __instance)
    {
        if (!__instance.m_StatusEffects.Contains(Hearthstone.cooldownEffect))
        {
            __instance.m_StatusEffects.Add(Hearthstone.cooldownEffect);
        }
        __instance.UpdateRegisters();
    }
}