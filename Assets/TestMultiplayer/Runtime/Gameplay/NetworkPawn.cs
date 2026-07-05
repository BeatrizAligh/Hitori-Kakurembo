using TestMultiplayer.Data;
using TestMultiplayer.Networking;
using Unity.Netcode;
using UnityEngine;

namespace TestMultiplayer.Gameplay
{
    public abstract class NetworkPawn : NetworkBehaviour
    {
        private readonly NetworkVariable<ulong> brainOwnerClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public ulong BrainOwnerClientId => brainOwnerClientId.Value;
        public bool HasBrain => BrainOwnerClientId != ulong.MaxValue;

        public virtual void AssignBrainOnServer(TestMultiplayerPlayerBrain brain)
        {
            if (!IsServer || brain == null)
            {
                return;
            }

            brainOwnerClientId.Value = brain.OwnerClientId;
        }

        public bool IsControlledBy(TestMultiplayerPlayerBrain brain)
        {
            return brain != null && brain.OwnerClientId == BrainOwnerClientId;
        }

        public abstract void ApplyInputFromBrain(
            TestMultiplayerPlayerBrain brain,
            TestMultiplayerInputFrame input,
            bool isServerApplication);

        public virtual void ApplyBrainAppearance(TestMultiplayerPlayerBrain brain, CharacterAppearanceData appearance)
        {
        }
    }
}
