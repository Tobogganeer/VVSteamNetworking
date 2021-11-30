
namespace VirtualVoid.Net
{
    // VVV Sent by client
    internal enum InternalClientMessageIDs : ushort // 2560 - 2585
    {
        PING = 2560,
        SCENE_LOADED = 2562,
        NETWORK_BEHAVIOR_COMMAND = 2563,
        //CLIENT_COMMAND = 2564,
        SEND_VOICE = 2565,
        REQUEST_NETWORK_ID_RESPAWN = 2566,
        C_NETWORK_TRANSFORM = 2567,
        C_NETWORK_ANIMATOR = 2568,
        CONNECTED = 2581,
        AUTH_TICKET = 2582
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
        BOUNCE_VOICE = 2568,
        REQUEST_AUTH = 2581
    }
}
