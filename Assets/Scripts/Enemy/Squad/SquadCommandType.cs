// 명령의 의도를 구분한다. 실제 이동과 도착 후 처리는 대원의 행동 로직이 담당한다.
public enum SquadCommandType
{
    MoveTo,
    HoldPosition,
    Regroup,
    ResumePatrol,
    Support,
    Investigate,
    ReturnToBase,
}
