
namespace VirtualVoid.Networking.Steam.LLAPI
{
    // VVV Sent by client
    internal enum InternalClientMessageIDs : ushort // 2560 - 2585
    {
        PING = 2560,
        DISCONNECTED = 2561,
        SCENE_LOADED = 2562,
        NETWORK_BEHAVIOR_COMMAND = 2563,
        CLIENT_ID = 2580
    }

    // VVV Sent by server
    internal enum InternalServerMessageIDs : ushort // 2560 - 2585
    {
        PONG = 2560,
        DISCONNECT = 2561,
        SCENE_CHANGE = 2562,
        SPAWN_NETWORK_ID = 2563,
        DESTROY_NETWORK_ID = 2564,
        NETWORK_TRANSFORM = 2565,
        NETWORK_ANIMATOR = 2566,
        NETWORK_BEHAVIOR_RPC = 2567,
        CLIENT_ID = 2580
    }
}
