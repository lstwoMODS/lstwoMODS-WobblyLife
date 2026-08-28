using System;
using System.Linq;
using Plugin = lstwoMODS_WobblyLife.Plugin;

namespace WLProxChat.Transport
{
    /// <summary>
    /// Who a voice frame came from, in a form every peer agrees on.
    /// <para>
    /// Hawk connection ids are not that form. <c>HawkNetworkObject.AssignOwnership</c> refuses to run
    /// anywhere but the host, and the ownership message that reaches clients carries a bare
    /// "are you the owner" bool rather than an id, so on a client <c>GetOwner()</c> is null for every
    /// object, including its own player. A client handed a connection id has nothing to turn it back
    /// into a player with.
    /// </para>
    /// <para>
    /// The network id of the speaker's <see cref="PlayerController"/> is the same number on every
    /// machine, which is the whole basis of replication, so that is what travels with a frame.
    /// </para>
    /// </summary>
    public static class VoiceIdentity
    {
        /// <summary>
        /// A speaker we could not place. Their voice still plays, just not positionally. Network ids
        /// are handed out from zero upwards, so the sentinel is taken from the far end of the range
        /// rather than being 0.
        /// </summary>
        public const uint Unknown = uint.MaxValue;

        /// <summary>Our own voice looped back to us locally. Never travels.</summary>
        public const uint Loopback = uint.MaxValue - 1;

        /// <summary>Our own id, to stamp on outgoing frames. <see cref="Unknown"/> until we have a body.</summary>
        public static uint Local()
        {
            try
            {
                return GameInstance.InstanceExists
                    ? IdOf(GameInstance.Instance.GetFirstLocalPlayerController())
                    : Unknown;
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] could not read the local speaker id: {e.Message}");
                return Unknown;
            }
        }

        /// <summary>
        /// Host only: the speaker id of whoever owns <paramref name="connectionId"/>. This is how the
        /// relay stamps the truth onto a frame instead of trusting what the sender claimed, and it
        /// only works here because owners are resolvable on the host.
        /// </summary>
        public static uint ForConnection(int connectionId)
        {
            try
            {
                if (!GameInstance.InstanceExists || connectionId < 0) return Unknown;

                return IdOf(GameInstance.Instance.GetPlayerControllers()
                    .FirstOrDefault(c => c != null && c.networkObject?.GetOwner()?.Id == connectionId));
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] could not resolve a speaker id for connection {connectionId}: {e.Message}");
                return Unknown;
            }
        }

        /// <summary>
        /// The player a frame belongs to, or null if they have not spawned here yet. Returning null
        /// rather than throwing matters: frames routinely arrive before the player object does.
        /// </summary>
        public static PlayerController Resolve(uint speakerId)
        {
            try
            {
                if (!GameInstance.InstanceExists || speakerId >= Loopback) return null;

                return GameInstance.Instance.GetPlayerControllerByNetworkID(speakerId);
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogWarning($"[VoiceChat] could not resolve speaker {speakerId}: {e.Message}");
                return null;
            }
        }

        private static uint IdOf(PlayerController controller)
            => controller == null || controller.networkObject == null
                ? Unknown
                : controller.networkObject.GetNetworkID();
    }
}
