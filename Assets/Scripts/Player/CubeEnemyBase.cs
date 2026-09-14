using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// CubeEnemyBase (큐브 면 고정 몹 — 공용 베이스)
    /// [[changelog/2026-09-14_NavMesh-면단위-장애물회피검증]]에서 실제 `EnemyPyeong`(NavMeshAgent
    /// 기반)을 큐브 면에 세우려다, NavMeshAgent가 이 환경뿐 아니라 실제 Play에서도 NavMesh에
    /// 안 붙는 원인 불명 버그를 만나 사용자가 "NavMeshAgent 포기하고 이미 검증된 방식(직선 이동+
    /// clamp)으로 통일"을 선택했습니다.
    ///
    /// [[CubeFaceLockedMob]](완전히 검증된 프로토타입)의 이동/면 고정/수색·복귀 상태 머신을
    /// 그대로 가져오되, 종별로 다른 "전투 상태머신"(원본 `EnemyBase`/`EnemyPyeong` 구조와 동일한
    /// 발상)을 서브클래스가 얹을 수 있도록 <see cref="OnChasingTick"/>/<see cref="OnBusyTick"/>
    /// 훅을 추가했습니다 — Busy 상태일 땐 베이스가 이동을 멈추고 서브클래스가 전권을 가집니다
    /// (공격 윈드업/액티브/리커버리 등).
    ///
    /// ⚠️ 기존 [[EnemyBase]](NavMeshAgent 기반, 평지 던전 SC_Face_0 등에서 씀)는 전혀 안 건드렸습니다
    /// — 이건 완전히 별도의 "큐브 면 전용" 베이스입니다. 기존 5종 몹/평지 던전엔 영향 없음.
    /// </summary>
    public abstract class CubeEnemyBase : MonoBehaviour
    {
        protected enum MacroState { Idle, Chasing, Searching, Returning, Busy }

        [Tooltip("이 몹이 묶여있는 면의 법선입니다. 스폰 후 절대 바뀌지 않습니다.")]
        public Vector3 faceNormal = Vector3.forward;

        [Tooltip("걷고 있는 큐브의 중심입니다.")]
        public Transform cubeCenter;

        [Tooltip("큐브 한 변 길이의 절반입니다.")]
        public float cubeHalfExtent = 10f;

        [Tooltip("피벗이 수학적 표면에서 법선 방향으로 얼마나 더 떠있어야 하는지입니다 — " +
                 "[[CubeSurfaceWalker]].surfaceOffset과 같은 이유(캡슐/모델이 파묻혀 보이는 버그 방지)입니다.")]
        public float surfaceOffset = 1f;

        [Tooltip("추적 대상(플레이어)입니다.")]
        public CubeSurfaceWalker target;

        [Header("수색/복귀 ([[changelog/2026-09-12_면고정몹-수색복귀AI]])")]
        [Tooltip("플레이어가 면을 떠난 뒤 이 시간(초) 안에 돌아오면 다시 추적, 안 돌아오면 원위치로 복귀합니다.")]
        public float searchDuration = 5f;

        [Tooltip("수색 중 제자리에서 두리번거리는 좌우 각도입니다.")]
        public float lookAroundAngle = 45f;

        [Tooltip("두리번거리는 속도입니다.")]
        public float lookAroundSpeed = 2f;

        [Header("체력")]
        public float maxHP = 100f;
        public float currentHP;

        [Tooltip("복귀 중(및 원위치 대기 중) 초당 회복량입니다.")]
        public float returnRegenPerSecond = 25f;

        /// <summary>종마다 다른 이동속도를 서브클래스가 제공합니다(원본 EnemyBase.MoveSpeed와 동일한 패턴).</summary>
        protected abstract float MoveSpeed { get; }

        protected MacroState state = MacroState.Idle;
        protected float distanceToPlayer = float.MaxValue;

        private Vector3 spawnPosition;
        private Quaternion baseRotation;
        private float searchTimer;

        public bool IsSameFaceAs(Vector3 otherNormal) => Vector3.Dot(faceNormal, otherNormal) > 0.999f;

        protected virtual void Start()
        {
            spawnPosition = transform.position;
            baseRotation = transform.rotation;
            currentHP = maxHP;
        }

        /// <summary>피격 시 호출합니다. 죽음 처리는 서브클래스/후속 작업 범위입니다.</summary>
        public void TakeDamage(float amount)
        {
            currentHP = Mathf.Max(0f, currentHP - amount);
        }

        protected void Heal(float amount)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        protected virtual void Update()
        {
            if (target == null) return;

            bool sameFace = IsSameFaceAs(target.CurrentSurfaceNormal);
            distanceToPlayer = sameFace ? Vector3.Distance(transform.position, target.transform.position) : float.MaxValue;

            switch (state)
            {
                case MacroState.Idle:
                    Heal(returnRegenPerSecond * Time.deltaTime);
                    if (sameFace) state = MacroState.Chasing;
                    break;

                case MacroState.Chasing:
                    if (!sameFace) { EnterSearching(); break; }
                    OnChasingTick();
                    break;

                case MacroState.Searching:
                    if (sameFace) { state = MacroState.Chasing; break; }
                    searchTimer += Time.deltaTime;
                    LookAround();
                    if (searchTimer >= searchDuration) state = MacroState.Returning;
                    break;

                case MacroState.Returning:
                    if (sameFace) { state = MacroState.Chasing; break; }
                    Heal(returnRegenPerSecond * Time.deltaTime);
                    if (MoveToward(spawnPosition, arriveThreshold: 0.15f)) state = MacroState.Idle;
                    break;

                case MacroState.Busy:
                    // 공격 등 서브클래스 전용 상태 — 면을 벗어나면 그 즉시 수색으로 강제 전환(공격 캔슬).
                    if (!sameFace) { EnterSearching(); break; }
                    OnBusyTick();
                    break;
            }
        }

        /// <summary>
        /// Chasing 상태에서 매 프레임 호출됩니다. 기본 구현은 그냥 플레이어에게 접근만 합니다 —
        /// 사거리 안에 들어왔을 때 공격 등으로 전환하고 싶은 서브클래스는 이걸 오버라이드해서
        /// <see cref="EnterBusy"/>를 호출하세요(원본 EnemyPyeong.UpdateChase()와 같은 역할).
        /// </summary>
        protected virtual void OnChasingTick()
        {
            MoveToward(target.transform.position);
        }

        /// <summary>Busy 상태(공격 등) 동안 매 프레임 호출됩니다. 끝나면 <see cref="EnterChasing"/> 등을 호출하세요.</summary>
        protected virtual void OnBusyTick() { }

        protected void EnterChasing() => state = MacroState.Chasing;
        protected void EnterBusy() => state = MacroState.Busy;

        private void EnterSearching()
        {
            state = MacroState.Searching;
            searchTimer = 0f;
        }

        /// <summary>월드 좌표 목표를 향해 면 위에서 이동합니다. 도착하면 true를 반환합니다.</summary>
        protected bool MoveToward(Vector3 worldTarget, float arriveThreshold = 0f)
        {
            Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
            Vector3 myLocal = transform.position - center;
            Vector3 targetLocal = worldTarget - center;

            int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);

            Vector3 toTarget = targetLocal - myLocal;
            toTarget[axis] = 0f; // 법선 축 성분은 제거 — 면 위 접선 방향으로만 이동
            bool arrived = toTarget.magnitude <= arriveThreshold;

            if (toTarget.sqrMagnitude > 0.0001f && !arrived)
            {
                Vector3 moveDir = toTarget.normalized;
                myLocal += moveDir * MoveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(moveDir, faceNormal);
            }

            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                myLocal[a] = Mathf.Clamp(myLocal[a], -cubeHalfExtent, cubeHalfExtent);
            }
            myLocal[axis] = (cubeHalfExtent + surfaceOffset) * Mathf.Sign(faceNormal[axis]);

            transform.position = center + myLocal;
            return arrived;
        }

        /// <summary>제자리에서 faceNormal 축을 기준으로 좌우로 두리번거립니다(당황한 느낌의 시각 피드백).</summary>
        private void LookAround()
        {
            float angle = Mathf.Sin(searchTimer * lookAroundSpeed) * lookAroundAngle;
            transform.rotation = Quaternion.AngleAxis(angle, faceNormal) * baseRotation;
        }
    }
}
