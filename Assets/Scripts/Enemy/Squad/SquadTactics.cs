public enum SquadTactic
{
    NoContact,           // 공유된 적 정보가 없는 상태.
    HoldPosition,        // 자리를 지키며 교전.
    FormationAdvance,    // 생존 대원이 함께 압박.
    DisorganizedDefense,  // 분대장 사망 후 지휘 없는 상태.
    Investigate,          // 공유된 적 정보가 있지만 아무도 적의 모습을 발견하지 못한 상태.
}