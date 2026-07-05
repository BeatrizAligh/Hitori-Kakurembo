using TestMultiplayer.Data;
using TestMultiplayer.Gameplay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TestMultiplayer.Networking
{
    public class TestMultiplayerPlayerBrain : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<CharacterAppearanceData> appearance = new NetworkVariable<CharacterAppearanceData>(
            CharacterAppearanceData.Default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<NetworkObjectReference> controlledPawn = new NetworkVariable<NetworkObjectReference>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string PlayerName => playerName.Value.ToString();
        public CharacterAppearanceData Appearance => appearance.Value;
        public bool IsReady => isReady.Value;
        public NetworkPawn CurrentPawn { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                TestMultiplayerSessionManager session = TestMultiplayerSessionManager.Instance;
                CharacterProfile profile = session != null ? session.GetApprovedProfile(OwnerClientId) : new CharacterProfile();
                playerName.Value = new FixedString64Bytes(profile.PlayerName);
                appearance.Value = profile.Appearance;
                isReady.Value = session != null && session.GetReadyState(OwnerClientId);
            }

            controlledPawn.OnValueChanged += HandleControlledPawnChanged;
            appearance.OnValueChanged += HandleAppearanceChanged;
            isReady.OnValueChanged += HandleReadyChanged;
            ResolveControlledPawn(controlledPawn.Value);
            TestMultiplayerSessionManager.Instance?.RegisterBrain(this);
        }

        public override void OnNetworkDespawn()
        {
            controlledPawn.OnValueChanged -= HandleControlledPawnChanged;
            appearance.OnValueChanged -= HandleAppearanceChanged;
            isReady.OnValueChanged -= HandleReadyChanged;
            TestMultiplayerSessionManager.Instance?.UnregisterBrain(this);
        }

        public void SubmitReady(bool ready)
        {
            if (!IsOwner)
            {
                return;
            }

            SubmitReadyServerRpc(ready);
        }

        public void SubmitInput(TestMultiplayerInputFrame input)
        {
            if (!IsOwner)
            {
                return;
            }

            SubmitInputServerRpc(input);
        }

        public void AssignPawnOnServer(NetworkPawn pawn)
        {
            if (!IsServer || pawn == null || pawn.NetworkObject == null)
            {
                return;
            }

            pawn.AssignBrainOnServer(this);
            controlledPawn.Value = pawn.NetworkObject;
            ResolveControlledPawn(controlledPawn.Value);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitReadyServerRpc(bool ready)
        {
            isReady.Value = ready;
            TestMultiplayerSessionManager.Instance?.SetReadyStateOnServer(OwnerClientId, ready);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputServerRpc(TestMultiplayerInputFrame input)
        {
            ApplyInputToPawn(input, true);
            ReplicateInputClientRpc(input);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ReplicateInputClientRpc(TestMultiplayerInputFrame input)
        {
            if (IsServer)
            {
                return;
            }

            ApplyInputToPawn(input, false);
        }

        private void ApplyInputToPawn(TestMultiplayerInputFrame input, bool isServerApplication)
        {
            ResolveControlledPawn(controlledPawn.Value);
            CurrentPawn?.ApplyInputFromBrain(this, input, isServerApplication);
        }

        private void HandleControlledPawnChanged(NetworkObjectReference previousValue, NetworkObjectReference newValue)
        {
            ResolveControlledPawn(newValue);
        }

        private void HandleAppearanceChanged(CharacterAppearanceData previousValue, CharacterAppearanceData newValue)
        {
            ResolveControlledPawn(controlledPawn.Value);
            CurrentPawn?.ApplyBrainAppearance(this, newValue);
        }

        private void HandleReadyChanged(bool previousValue, bool newValue)
        {
            TestMultiplayerSessionManager.Instance?.RegisterBrain(this);
        }

        private void ResolveControlledPawn(NetworkObjectReference pawnReference)
        {
            CurrentPawn = null;

            if (pawnReference.TryGet(out NetworkObject pawnObject))
            {
                CurrentPawn = pawnObject.GetComponent<NetworkPawn>();
                CurrentPawn?.ApplyBrainAppearance(this, appearance.Value);
            }
        }
    }
}
