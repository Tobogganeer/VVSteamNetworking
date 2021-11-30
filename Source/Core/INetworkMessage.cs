using System;

namespace VirtualVoid.Net
{
    public interface INetworkMessage
    {
        //byte[] Serialize();

        /// <summary>
        /// Here is where you add all the contents of your struct to the message.
        /// </summary>
        /// <param name="message"></param>
        void AddToMessage(Message message);

        /// <summary>
        /// Here is where you read back the contents of your struct from the message.
        /// </summary>
        /// <param name="message"></param>
        void Deserialize(Message message);

        /// <summary>
        /// Only used to determine if the struct will fit in the message.
        /// You can safely return 0 if you know your struct will not exceed SteamManager.PUBLIC_MESSAGE_BUFFER_SIZE.
        /// </summary>
        /// <returns></returns>
        //byte GetMaxSize();
        // Maybe change this? https://stackoverflow.com/questions/8173239/c-getting-size-of-a-value-type-variable-at-runtime
    }
}
