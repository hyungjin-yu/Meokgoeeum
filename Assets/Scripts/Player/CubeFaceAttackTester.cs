using UnityEngine;
using UnityEngine.InputSystem;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeFaceAttackTester (면 기반 공격 판정 — 프로토타입)
    /// 사용자 요구사항의 두 번째 절반: "캐릭터도 다른 면에 있는 몹은 공격이 불가능하다."
    /// F키를 누르면 주변 [[CubeFaceLockedMob]] 중 "같은 면 + 사거리 안"인 것만 맞춥니다 —
    /// 3D 월드 거리만 보면 모서리 너머 몹이 가까워 보일 수 있어서, 반드시 면 일치 여부를
    /// 거리 판정과 별도로 체크합니다.
    ///
    /// 실제 게임의 [[BrushWeapon]]을 이 프로토타입에서 그대로 쓰지 않은 이유: 이 씬은
    /// [[CubePrototypeBuilder]] 주석대로 "실제 게임 씬을 안 건드리는" 독립 검증용이라,
    /// 판정 규칙 자체(같은 면인지)만 최소로 떼어서 검증합니다. 규칙이 확정되면
    /// BrushWeapon 쪽에도 같은 검사를 추가할 수 있습니다.
    /// </summary>
    public class CubeFaceAttackTester : MonoBehaviour
    {
        [Tooltip("공격 사거리(월드 거리 기준)입니다.")]
        public float attackRange = 3f;

        [Tooltip("공격에 쓰는 CubeSurfaceWalker입니다(비워두면 자기 자신에서 찾음).")]
        public CubeSurfaceWalker walker;

        private void Awake()
        {
            if (walker == null) walker = GetComponent<CubeSurfaceWalker>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;
            TryAttack();
        }

        /// <summary>리플렉션 테스트에서 직접 호출하기 쉽도록 public으로 노출합니다.</summary>
        public void TryAttack()
        {
            if (walker == null) return;

            var mobs = FindObjectsByType<CubeFaceLockedMob>(FindObjectsSortMode.None);
            foreach (var mob in mobs)
            {
                float dist = Vector3.Distance(transform.position, mob.transform.position);
                bool sameFace = mob.IsSameFaceAs(walker.CurrentSurfaceNormal);

                if (dist > attackRange) continue;

                if (!sameFace)
                {
                    Debug.Log($"[CubeFaceAttackTester] {mob.name} — 사거리 안이지만 다른 면이라 공격 무시 (거리={dist:F2})");
                    continue;
                }

                Debug.Log($"[CubeFaceAttackTester] {mob.name} 명중! (거리={dist:F2})");
            }
        }
    }
}
