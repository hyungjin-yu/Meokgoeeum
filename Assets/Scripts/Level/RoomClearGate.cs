using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// RoomClearGate (방 클리어 게이트)
    /// 지정한 박스 범위 안에 살아있는 적이 하나도 없으면 [[Stairs]]를 활성화합니다.
    /// [[19 층별 상세 설계]]에서 가장 흔한 계단 조건인 "전투 클리어"의 기본형 구현입니다.
    ///
    /// 적을 미리 손으로 목록에 등록하는 대신 "이 범위 안에 EnemyHealth가 남아있는가"를
    /// 주기적으로 검사하는 방식을 택했습니다 — [[EnemyBun]]처럼 처치 시 미니언이 새로
    /// 생겨나는 경우에도(손으로 등록해둔 목록에는 없는 새 오브젝트) 자동으로 걸러집니다.
    ///
    /// 다른 계단 조건(예: "색 구슬 획득 후 자동 등장", "특정 순서로 처치")이 필요한 층은
    /// 이 스크립트 대신 새로 만들고 Stairs.Activate()만 그대로 호출하면 됩니다 — Stairs는
    /// "누가 왜 열었는지" 모릅니다(단일 책임 분리).
    /// </summary>
    public class RoomClearGate : MonoBehaviour
    {
        [Tooltip("이 범위(중심 = 이 오브젝트 위치) 안의 적을 전부 처치해야 계단이 열립니다.")]
        public Vector3 boundsSize = new Vector3(20f, 5f, 20f);

        [Tooltip("클리어되면 활성화할 계단입니다.")]
        public Stairs stairs;

        [Tooltip("검사 주기입니다. 매 프레임 안 하고 이 간격으로 확인합니다 (최적화 원칙 — EnemyPyeong의 perceptionInterval과 같은 이유).")]
        public float checkInterval = 0.5f;

        [Tooltip("적이 0마리인 상태가 이만큼 '연속으로' 지속돼야 클리어로 판정합니다. " +
                 "EncounterSpawner처럼 웨이브 사이에 인터벌(대기 시간)이 있는 스포너와 같이 쓸 때, " +
                 "그 인터벌 동안의 일시적 0마리 상태를 클리어로 오판하지 않으려면 이 값이 " +
                 "스포너의 웨이브 인터벌보다 넉넉히 커야 합니다 (기본 3초 — 이 프로젝트의 " +
                 "EncounterSpawner.waveInterval 기본값 2초보다 여유 있게 잡음).")]
        public float clearGraceDuration = 3f;

        private bool cleared;
        private float timer;

        // 2026-09-04 발견·수정: 처음엔 "한 번이라도 적을 본 적 있으면 그 다음 0마리 즉시 클리어"로
        // 고쳤는데, 이러면 여전히 웨이브 사이 인터벌(예: 1웨이브 처치 → 2웨이브 스폰까지 2초 대기)
        // 동안의 일시적 0마리를 클리어로 오판했음(사용자 리포트: 1웨이브만 잡았는데 계단이 열림).
        // "한 번이라도 봤는가"가 아니라 "0마리 상태가 얼마나 오래 지속됐는가"로 바꿔서, 웨이브
        // 인터벌보다 넉넉한 유예 시간(clearGraceDuration) 동안 계속 0마리여야만 클리어로 판정.
        // 적이 다시 나타나면(다음 웨이브 스폰) 유예 타이머는 그 즉시 리셋됩니다.
        private bool hasEverSeenEnemy;
        private float emptyStreakDuration;

        private void Update()
        {
            if (cleared) return;

            timer += Time.deltaTime;
            if (timer < checkInterval) return;
            timer = 0f;

            bool anyAlive = AnyEnemyAlive();
            if (anyAlive)
            {
                hasEverSeenEnemy = true;
                emptyStreakDuration = 0f;
                return;
            }

            if (!hasEverSeenEnemy) return; // 아직 인카운터가 시작조차 안 함 — 판정 보류

            emptyStreakDuration += checkInterval;
            if (emptyStreakDuration < clearGraceDuration) return;

            cleared = true;
            Debug.Log($"[RoomClearGate] {name} 클리어! 계단 활성화.");
            if (stairs != null)
                stairs.Activate();
            else
                Debug.LogWarning($"[RoomClearGate] {name}에 Stairs가 연결 안 되어 있습니다.");
        }

        private bool AnyEnemyAlive()
        {
            Collider[] hits = Physics.OverlapBox(transform.position, boundsSize / 2f, transform.rotation);
            foreach (var hit in hits)
            {
                if (hit.GetComponent<EnemyHealth>() != null)
                    return true;

                // 2026-09-02 발견·수정: 보스는 EnemyHealth가 아니라 별도의 BossHealth를 씁니다
                // ([[BossHealth]] 클래스 doc 참고 — 구슬 미드랍/페이즈 전환 때문에 독립 구현).
                // 그래서 여기서 안 걸러지면 보스룸에 이 게이트가 있을 때 "일반 적이 하나도 없다"만
                // 보고 보스가 멀쩡히 살아있어도 즉시 클리어 판정 → 계단 활성화되던 실제 버그가 있었음
                // (사용자 리포트: "보스 잡고 있는데 스테이지가 넘어가버렸어").
                var boss = hit.GetComponent<BossHealth>();
                if (boss != null && !boss.IsDead)
                    return true;
            }
            return false;
        }

        // 에디터에서 방 범위를 눈으로 확인하기 위한 기즈모입니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boundsSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
