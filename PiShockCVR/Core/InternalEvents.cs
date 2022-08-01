using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using ABI_RC.Core.Player;

namespace PiShockCVR.Core
{
    public static class InternalEvents
    {
        public static event Action<GameObject> OnLocalAvatarInstantiated;

        public static void Init()
        {
            PiShockCVRMod.Instance.HarmonyInstance.Patch(typeof(PlayerSetup).GetMethod("SetupAvatar", BindingFlags.Public | BindingFlags.Instance),
                postfix: new HarmonyMethod(typeof(InternalEvents).GetMethod(nameof(LocalAvatarInstantiatedPatch), BindingFlags.NonPublic | BindingFlags.Static)));
            PiShockCVRMod.Logger.Msg("Patched SetupAvatar method.");
        }

        private static void LocalAvatarInstantiatedPatch(PlayerSetup __instance)
        {
            if (__instance._avatar == null)
                return;

            try
            {
                OnLocalAvatarInstantiated?.Invoke(__instance._avatar);
            }
            catch (Exception e)
            {
                PiShockCVRMod.Logger.Error("Error while invoking OnLocalAvatarInstantiated: " + e.ToString());
            }
        }
    }
}
