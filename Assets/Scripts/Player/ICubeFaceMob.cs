using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// ICubeFaceMob (면 기반 몹 공통 인터페이스)
    /// [[CubeFaceLockedMob]](프로토타입 첫 테스트 몹)과 [[CubeEnemyBase]](실제 5종 몹의 큐브
    /// 버전 베이스)가 서로 상속 관계가 아니라 둘 다 독립적으로 만들어져서, [[CubeFaceAttackTester]]가
    /// 지금까지 `CubeFaceLockedMob`만 찾고 `CubeEnemyBase` 계열(Pyeong/Won/Bun/Heup/Gwang)은
    /// 아예 못 때리고 있었습니다. 공통 인터페이스로 묶어서 F키 공격이 둘 다에 통하게 합니다.
    ///
    /// `currentHP`/`maxHP`는 두 클래스 다 public 필드라서(Inspector 노출용), 필드는 인터페이스
    /// 프로퍼티를 암묵적으로 구현할 수 없으므로 각 클래스에서 명시적 인터페이스 구현으로
    /// 필드값을 그대로 반환하게 했습니다.
    /// </summary>
    public interface ICubeFaceMob
    {
        bool IsSameFaceAs(Vector3 otherNormal);
        void TakeDamage(float amount);
        float CurrentHP { get; }
        float MaxHP { get; }
    }
}
