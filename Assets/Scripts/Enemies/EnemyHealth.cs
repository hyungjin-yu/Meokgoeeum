using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// EnemyHealth (적 체력)
    /// IDamageable을 구현해서 BrushWeapon 등에게 맞으면 체력이 깎입니다.
    /// 모든 먹괴음 타입(평/원/흡/분/광)과 보스가 공통으로 쓸 수 있도록 분리해뒀습니다.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("체력 (14 밸런스 수치 시트)")]
        public float maxHP = 20f; // 2026-08-16: 30 -> 20. 붓 3타 콤보(6+7+10=23)로 정확히 3클릭째 처치되도록 QA용 조정

        private float currentHP;
        private bool isDead;

        /// <summary>
        /// 2026-09-16 추가 — [[MonsterPaintParts]](부위 색칠 처치 시스템)가 붙어있으면 숫자
        /// 체력 대신 그쪽으로 위임합니다. 없으면(아직 이 시스템을 안 붙인 종) 기존 숫자 체력
        /// 그대로 동작 — 두 방식이 종별로 섞여 있어도 안전합니다.
        /// </summary>
        private MonsterPaintParts paintParts;

        /// <summary>처치됐을 때 호출됩니다. (④ 색 구슬 드랍 등을 구독해서 처리하면 됩니다)</summary>
        public event System.Action<EnemyHealth> OnDeath;

        // 2026-09-16 — MonsterPaintParts가 있으면 "남은 칠 안 된 부위 수"를 HP처럼 노출합니다.
        // 안 그러면 [[EnemyHeup]]의 저체력 판정이나 디버그 HUD처럼 CurrentHP를 읽는 다른 코드가
        // 계속 maxHP 그대로인 걸 보고 "멀쩡하다"고 착각하게 됩니다.
        public float CurrentHP => paintParts != null ? paintParts.TotalParts - paintParts.PaintedCount : currentHP;

        /// <summary>죽었는지 여부입니다. 보통은 Die()에서 바로 Destroy돼서 굳이 안 필요하지만,
        /// [[LockOnController]]가 다른 IDamageable 타입(BossHealth 등)과 똑같은 방식으로 다룰 수 있게 노출합니다.</summary>
        public bool IsDead => isDead;

        private void Awake()
        {
            currentHP = maxHP;
            paintParts = GetComponent<MonsterPaintParts>();
        }

        /// <summary>
        /// 맞았을 때 호출됩니다. [[MonsterPaintParts]]가 있으면 데미지 수치와 무관하게 매번 딱
        /// 1부위를 무작위로 칠하고, 7부위가 전부 칠해지면 <see cref="Die"/>를 호출합니다.
        /// 없으면 기존처럼 숫자 체력을 깎다가 0 이하가 되면 호출합니다.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (isDead) return;

            if (paintParts != null)
            {
                paintParts.PaintRandomPart();
                if (paintParts.AllPainted)
                    Die();
                return;
            }

            currentHP -= amount;
            // 2026-08-19: 피격마다 찍히는 로그가 다른 디버깅(EncounterSpawner 등) 콘솔을 뒤덮어서 제거.
            // 죽었을 때(Die())와 회복 때는 상태 변화가 드물어서 그대로 남겨둠.

            if (currentHP <= 0f)
                Die();
        }

        /// <summary>
        /// 체력을 회복합니다. [[EnemyHeup]]이 색 복원 구역에서 흡수할 때 사용합니다.
        /// </summary>
        public void Heal(float amount)
        {
            if (isDead) return;

            currentHP = Mathf.Min(maxHP, currentHP + amount);
            // 2026-08-21: EnemyHeup이 매 프레임 Heal()을 호출해서 로그가 도배됨 — 피격 로그와
            // 같은 이유로 제거 (기획서/CLAUDE.md "피격/상태 변화 로깅 원칙" 예외 참고)
        }

        /// <summary>
        /// 최대 체력을 재설정하고 현재 체력도 그 값으로 맞춥니다.
        /// [[EnemyBun]]이 분열로 생성한 미니언의 체력을 낮출 때처럼, Awake()에서 이미
        /// currentHP가 기존 maxHP로 세팅된 뒤에 다시 바꿔야 하는 경우에 씁니다.
        /// </summary>
        public void ConfigureMaxHP(float newMax)
        {
            maxHP = newMax;
            currentHP = newMax;
        }

        private void Die()
        {
            isDead = true;
            OnDeath?.Invoke(this);

            if (GameState.Instance != null)
                GameState.Instance.totalKills++;

            DropColorOrb();

            Debug.Log($"{name} 정화 완료!");
            Destroy(gameObject);
        }

        /// <summary>
        /// [[03 먹괴음 - 적 설계]] "처치 시 색 구슬 1개 드랍 (랜덤 색)" — 모든 먹괴음 타입 공통이라
        /// 여기(EnemyHealth)에 둡니다. 층별 드랍 가중치([[14 밸런스 수치 시트]])는 층/큐브
        /// 시스템이 아직 없어서, 지금은 6색 균등 확률로 임시 구현 — 나중에 층 시스템이 생기면
        /// 가중치 테이블을 넣을 자리.
        /// </summary>
        private void DropColorOrb()
        {
            if (ColorOrbPool.Instance == null)
            {
                Debug.LogWarning("[EnemyHealth] ColorOrbPool이 씬에 없어서 구슬을 드랍하지 못했습니다.");
                return;
            }

            OrbColor randomColor = (OrbColor)Random.Range(0, 6);
            ColorOrbPool.Instance.Get(transform.position, randomColor);
        }
    }
}
