using UnityEngine;

/// <summary>
/// IKnockbackable (넉백 가능 인터페이스)
/// [[02 플레이어 시스템]] "번쩍(노랑)" 스킬처럼 넉백을 주는 효과가, 대상이 뭔지 몰라도
/// (NavMeshAgent 적이든 나중에 다른 이동 방식이든) 넉백을 적용할 수 있게 분리했습니다.
/// IDamageable과 같은 이유로 나눈 인터페이스입니다.
/// </summary>
public interface IKnockbackable
{
    void ApplyKnockback(Vector3 direction, float force);
}
