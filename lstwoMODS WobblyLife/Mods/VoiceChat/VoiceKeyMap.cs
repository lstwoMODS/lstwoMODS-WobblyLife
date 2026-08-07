using lstwoMODS.ImGui.Shared;
using lstwoMODS_Core.Hotkeys;
using UnityEngine;

namespace WLProxChat
{
    /// <summary>
    /// Thin pass-through to core's <see cref="KeyMapper"/>, kept only so call sites read clearly.
    /// The mapping itself lives in core, shared with the macro hotkey trigger and the context menu,
    /// so a key that binds in one place binds everywhere.
    /// </summary>
    internal static class VoiceKeyMap
    {
        /// <summary>
        /// Unity key for an overlay-captured one, or <see cref="KeyCode.None"/> when Unity has no
        /// equivalent. F16 to F24 are the notable case: the overlay can capture them, but Unity's
        /// legacy Input cannot poll them, so they can never drive an in-game hotkey.
        /// </summary>
        public static KeyCode ToKeyCode(ImGuiKey key) => KeyMapper.ToKeyCode(key);
    }
}
