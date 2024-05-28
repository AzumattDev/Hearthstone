using System;
using HarmonyLib;

namespace Hearthstone
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    public static class PatchZNetOnNewConnection
    {
        private static void Postfix(ZNet __instance, ZNetPeer peer)
        {
            if (!__instance.IsServer()) return;
            if (__instance.m_adminList.Contains(peer.m_rpc.GetSocket().GetHostName()))
                peer.m_rpc.Invoke("HearthstoneAdminGetEvent");
        }
    }

    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
    public static class GameStartPatch
    {
        private static void Prefix()
        {
            ZRoutedRpc.instance.Register("HearthstoneAdminGetEvent",
                new Action<long, ZPackage>(HearthstoneAdminGET.RPC_isHearthAdmin));
        }
    }


    public class HearthstoneAdminGET
    {
        private static bool _isAdmin;

        public static void RPC_isHearthAdmin(long sender, ZPackage Hearthpkg)
        {
            if (Hearthpkg == null || Hearthpkg.Size() <= 0) return;
            var getAdm = Hearthpkg.ReadBool();
            ZNetPeer peerSteamID = ZNet.instance.GetPeer(sender);
            Hearthstone.IsAdmin = getAdm;
            if(Hearthstone.IsAdmin)
                Hearthstone.HearthLogger.LogMessage($"ADMIN DETECTED: {Player.m_localPlayer.GetPlayerName()}");
        }

        public static void RPC_CharID(ZNet __instance, ZRpc rpc)
        {
            if (!__instance.IsDedicated() && !__instance.IsServer()) return;
            ZNetPeer peer = __instance.GetPeer(rpc);
            string peerSteamId = peer.m_rpc.GetSocket().GetHostName();
            if (ZNet.instance.m_adminList != null && ZNet.instance.m_adminList.Contains(peerSteamId))
                _isAdmin = true;
            ZPackage newHearthpkg = new();
            if(_isAdmin)
                Hearthstone.HearthLogger.LogMessage($"ADMIN DETECTED: {peerSteamId} a.k.a. {peer.m_playerName}");
            newHearthpkg.Write(_isAdmin);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "HearthstoneAdminGetEvent", newHearthpkg);
        }
    }
}