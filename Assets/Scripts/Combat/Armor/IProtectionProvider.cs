// 체력 컴포넌트가 장비 보관 구조를 모르고 피격 부위의 보호 장비만 조회한다.
public interface IProtectionProvider
{
    ArmorInstance GetProtection(HitPart hitPart);
}
