using UnityEngine;

public readonly struct SquadCommand
{
    public SquadCommandType Type { get; }
    public Vector3 Destination { get; }
    public Transform FollowTarget { get; }
    public Vector3 FollowOffset { get; }

    public SquadCommand(SquadCommandType type, Vector3 destination)
    {
        Type = type;
        Destination = destination;
        FollowTarget = null;
        FollowOffset = Vector3.zero;
    }

    public SquadCommand(
        SquadCommandType type,
        Transform followTarget,
        Vector3 followOffset)
    {
        Type = type;
        Destination = default;
        FollowTarget = followTarget;
        FollowOffset = followOffset;
    }
}
