using System;

namespace VirtualVoid.Networking.Steam
{
    public interface INetworkMessage
    {
        //byte[] Serialize();

        void AddToMessage(Message message);

        void Deserialize(Message message);

        byte GetMaxSize();
        // Maybe change this? https://stackoverflow.com/questions/8173239/c-getting-size-of-a-value-type-variable-at-runtime
    }
}
