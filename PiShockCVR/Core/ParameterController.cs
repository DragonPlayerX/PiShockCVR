using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;

namespace PiShockCVR.Core
{
    public static class ParameterController
    {
        private static CVRAnimatorManager AnimatorManager => PlayerSetup.Instance?.animatorManager;

        public static bool SetParameter(string parameterName, bool value)
        {
            if (AnimatorManager == null)
                return false;

            bool? current = AnimatorManager.GetAnimatorParameterBool(parameterName);
            if (current == null)
                return false;

            AnimatorManager.SetAnimatorParameterBool(parameterName, value);
            CVR_MenuManager.Instance.SendAdvancedAvatarUpdate(parameterName, value ? 1f : 0f);

            return true;
        }
    }
}