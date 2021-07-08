using System;

namespace VirtualVoid.Networking.Steam
{
    public interface INetworkMessage
    {
        //byte[] Serialize();

        void AddToMessage(Message message);

        void Deserialize(ArraySegment<byte> segment);

        byte GetSize();
    }
}
