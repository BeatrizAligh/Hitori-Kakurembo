using System;
using Unity.Netcode;

namespace TestMultiplayer.Data
{
    [Serializable]
    public struct CharacterAppearanceData : INetworkSerializable, IEquatable<CharacterAppearanceData>
    {
        public int Head;
        public int Hair;
        public int LowerBody;
        public int UpperBody;
        public int Eyes;

        public static CharacterAppearanceData Default => new CharacterAppearanceData
        {
            Head = 0,
            Hair = 0,
            LowerBody = 0,
            UpperBody = 0,
            Eyes = 0
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Head);
            serializer.SerializeValue(ref Hair);
            serializer.SerializeValue(ref LowerBody);
            serializer.SerializeValue(ref UpperBody);
            serializer.SerializeValue(ref Eyes);
        }

        public bool Equals(CharacterAppearanceData other)
        {
            return Head == other.Head
                && Hair == other.Hair
                && LowerBody == other.LowerBody
                && UpperBody == other.UpperBody
                && Eyes == other.Eyes;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterAppearanceData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Head;
                hashCode = (hashCode * 397) ^ Hair;
                hashCode = (hashCode * 397) ^ LowerBody;
                hashCode = (hashCode * 397) ^ UpperBody;
                hashCode = (hashCode * 397) ^ Eyes;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"Head {Head}, Hair {Hair}, Upper {UpperBody}, Lower {LowerBody}, Eyes {Eyes}";
        }
    }
}
