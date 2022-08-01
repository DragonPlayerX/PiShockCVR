using System;
using System.Collections;
using MelonLoader;
using UnityEngine;

using PiShockCVR;
using PiShockCVR.Core;
using PiShockCVR.Config;

[assembly: MelonInfo(typeof(PiShockCVRMod), "PiShockCVR", "1.0.0", "DragonPlayer", "https://github.com/DragonPlayerX/PiShockCVR")]
[assembly: MelonGame("Alpha Blend Interactive", "ChilloutVR")]

namespace PiShockCVR
{
    public class PiShockCVRMod : MelonMod
    {
        public static readonly string Version = "1.0.0";

        public static PiShockCVRMod Instance;
        public static MelonLogger.Instance Logger => Instance.LoggerInstance;

        public override void OnApplicationStart()
        {
            Instance = this;
            Logger.Msg("Initializing PiShockCVR " + Version + "...");

            Configuration.Init();

            InternalEvents.Init();
            AvatarManager.Init();

            Logger.Msg("Running version " + Version + " of PiShockCVR.");
        }

        public override void OnUpdate() => AvatarManager.Update();

        public static void Run(Action action, float? delay = null) => MelonCoroutines.Start(RunAction(action, delay));

        public static IEnumerator RunAction(Action action, float? delay)
        {
            if (delay != null)
                yield return new WaitForSeconds((float)delay);

            action?.Invoke();
        }
    }
}
