using System;
using Unity.Netcode;
using UnityEngine;

namespace TestMultiplayer.Data
{
    [Serializable]
    public struct TestMultiplayerInputFrame : INetworkSerializable
    {
        public int Sequence;
        public float DeltaTime;
        public Vector2 Move;
        public Vector2 Look;
        public bool PrimaryAction;
        public bool SecondaryAction;
        public bool Jump;

        public static TestMultiplayerInputFrame Empty(int sequence, float deltaTime)
        {
            return new TestMultiplayerInputFrame
            {
                Sequence = sequence,
                DeltaTime = deltaTime
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref DeltaTime);
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Look);
            serializer.SerializeValue(ref PrimaryAction);
            serializer.SerializeValue(ref SecondaryAction);
            serializer.SerializeValue(ref Jump);
        }
    }
}
