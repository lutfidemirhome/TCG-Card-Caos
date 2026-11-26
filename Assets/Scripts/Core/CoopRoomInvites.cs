using System.Collections.Generic;

public readonly struct CoopFriend
{
    public readonly ulong Id;
    public readonly string Name;

    public CoopFriend(ulong id, string name)
    {
        Id = id;
        Name = name;
    }
}

public interface ICoopRoomInvites
{
    string RoomCode { get; }
    void JoinByCode(string code);
    void GetOnlineFriends(List<CoopFriend> friends);
    bool InviteFriend(ulong id);
}

public interface ICoopMemberPortraits
{
    UnityEngine.Texture2D GetPortrait(ulong member);
}
