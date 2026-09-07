/// <summary>
/// IDamageable (피격 가능 인터페이스)
/// 대미지를 받을 수 있는 모든 대상(먹괴음, 보스 등)이 구현합니다.
/// BrushWeapon은 이 인터페이스로만 대미지를 넘기고, 대상이 뭔지는 모릅니다
/// (③ 먹괴음 구현 시 이 인터페이스만 붙이면 바로 붓 공격을 맞을 수 있음).
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
