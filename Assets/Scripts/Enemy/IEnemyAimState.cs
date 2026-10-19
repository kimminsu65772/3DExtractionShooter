// 애니메이션은 판단 방식 대신 현재 조준 의도만 읽는다.
public interface IEnemyAimState
{
    bool IsAiming { get; }
}
