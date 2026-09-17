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
    public abstract class CubeEnemyBase : MonoBehaviour, ICubeFaceMob, IDamageable
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

        [Header("겹침 방지 ([[changelog/2026-09-14_몹겹침-최소거리분리]])")]
        [Tooltip("공격 판정이 없거나 사거리 체크를 안 하는 몹(흡/광 등)이 플레이어에게 이보다 " +
                 "가까이 다가가지 않도록 막는 기본 정지 거리입니다. 사거리 체크가 있는 몹(평/분/원)은 " +
                 "이미 각자 attackRange에서 멈추므로 이 값보다 attackRange가 크면 이 값은 실질적으로 안 씀.")]
        public float minApproachToPlayer = 1.2f;

        [Tooltip("다른 몹(플레이어 제외)과 이보다 가까워지면 서로 밀어냅니다 — 여러 몹이 플레이어 " +
                 "한 점으로 몰릴 때 겹쳐 보이는 것 방지.")]
        public float mobSeparationDistance = 1.2f;

        [Tooltip("몹 사이 분리 힘의 세기입니다.")]
        public float separationStrength = 4f;

        /// <summary>종마다 다른 이동속도를 서브클래스가 제공합니다(원본 EnemyBase.MoveSpeed와 동일한 패턴).</summary>
        protected abstract float MoveSpeed { get; }

        protected MacroState state = MacroState.Idle;
        protected float distanceToPlayer = float.MaxValue;

        private Vector3 spawnPosition;
        private Quaternion baseRotation;
        private float searchTimer;
        private bool movedThisFrame;

        /// <summary>
        /// 모델 자식 오브젝트에 붙어있는 Animator입니다("루트=로직, 자식=시각 모델" 구조 —
        /// 원본 [[EnemyBase]]와 동일한 관례). 아트 에셋이 없는 테스트용 오브젝트에서는 null일 수
        /// 있습니다.
        /// </summary>
        protected Animator animator;

        public bool IsSameFaceAs(Vector3 otherNormal) => Vector3.Dot(faceNormal, otherNormal) > 0.999f;

        /// <summary>
        /// 2026-09-16 추가 — [[MonsterPaintParts]](부위 색칠 처치 시스템)가 붙어있으면 숫자
        /// 체력 대신 그쪽으로 위임합니다. 없으면(아직 이 시스템을 안 붙인 종) 기존 숫자 체력
        /// 그대로 동작 — 두 방식이 종별로 섞여 있어도 안전합니다.
        /// </summary>
        private MonsterPaintParts paintParts;

        protected virtual void Start()
        {
            spawnPosition = transform.position;
            baseRotation = transform.rotation;
            currentHP = maxHP;
            animator = GetComponentInChildren<Animator>();
            paintParts = GetComponent<MonsterPaintParts>();
        }

        private bool isDead;

        /// <summary>
        /// 피격 시 호출합니다. [[MonsterPaintParts]]가 있으면 데미지 수치와 무관하게 매번 딱
        /// 1부위를 무작위로 칠하고, 7부위가 전부 칠해지면 <see cref="OnDeath"/>를 호출합니다.
        /// 없으면 기존처럼 숫자 체력을 깎다가 0 이하가 되면 호출합니다.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (isDead) return;

            if (paintParts != null)
            {
                paintParts.PaintRandomPart();
                if (paintParts.AllPainted)
                {
                    isDead = true;
                    OnDeath();
                }
                return;
            }

            currentHP = Mathf.Max(0f, currentHP - amount);
            if (currentHP <= 0f)
            {
                isDead = true;
                OnDeath();
            }
        }

        /// <summary>
        /// HP가 0이 됐을 때 정확히 한 번 호출됩니다. 기본 구현은 오브젝트를 파괴합니다.
        /// [[CubeEnemyBun]]의 분열처럼 파괴 전에 종별로 다른 처리가 필요하면 오버라이드해서
        /// 그 처리를 한 뒤 `base.OnDeath()`를 마지막에 호출하세요.
        /// (원본 [[EnemyHealth]].OnDeath 이벤트와 같은 역할이지만, 여긴 별도 컴포넌트 없이
        /// CubeEnemyBase 자체가 체력을 들고 있어서 훅으로 뺐습니다.)
        ///
        /// ⚠️ 2026-09-14 발견 — 원래 기본 구현이 빈 채였음. `Bun`(분열 시 자기 파괴)만 죽음 처리를
        /// 오버라이드해서 실제로 사라졌고, 나머지 4종(평/원/흡/광)은 HP가 0이 돼도 안 죽고
        /// 그 자리에 계속 남아 AI를 계속 돌리는 "유령" 상태가 됐음 — 사용자가 "공격하다가
        /// 이동하면 몹 하나가 가만히 서있기만 한다"고 리포트한 원인으로 추정.
        ///
        /// ⚠️ 2026-09-16 추가 — 원본 [[EnemyHealth]].Die()는 처치 시 색 구슬을 드랍하는데
        /// 이쪽(큐브 면 버전)엔 그 호출이 아예 없었음 — [[EnemyHeup]]/[[CubeEnemyHeup]]의
        /// "플레이어 보유 구슬을 빼앗아 회복" 재설계 작업 중 발견(SC_CubePrototype엔 애초에
        /// 색 구슬을 얻을 방법 자체가 없었음). `DropColorOrb()`를 여기에도 추가.
        /// </summary>
        protected virtual void OnDeath()
        {
            DropColorOrb();
            Destroy(gameObject);
        }

        /// <summary>[[03 먹괴음 - 적 설계]] "처치 시 색 구슬 1개 드랍(랜덤 색)" — [[EnemyHealth]].DropColorOrb()와 동일 로직.</summary>
        private void DropColorOrb()
        {
            if (ColorOrbPool.Instance == null)
            {
                Debug.LogWarning("[CubeEnemyBase] ColorOrbPool이 씬에 없어서 구슬을 드랍하지 못했습니다.");
                return;
            }

            OrbColor randomColor = (OrbColor)Random.Range(0, 6);
            ColorOrbPool.Instance.Get(transform.position, randomColor);
        }

        // 2026-09-16 — MonsterPaintParts가 있으면 "남은 칠 안 된 부위 수"를 HP처럼 노출합니다.
        // [[RoomClearGate]].AnyEnemyAlive()가 "CurrentHP > 0 = 아직 살아있음"으로 그대로
        // 판정하므로, 이렇게만 해두면 그쪽 코드를 전혀 안 건드려도 새 체계와 맞물립니다.
        float ICubeFaceMob.CurrentHP => paintParts != null ? paintParts.TotalParts - paintParts.PaintedCount : currentHP;
        float ICubeFaceMob.MaxHP => paintParts != null && paintParts.TotalParts > 0 ? paintParts.TotalParts : maxHP;

        protected void Heal(float amount)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        protected virtual void Update()
        {
            if (target == null || isDead) return; // Destroy()는 다음 프레임에 실제 반영되므로, 그 사이 계속 움직이지 않게 방어

            movedThisFrame = false;
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
                    // ⚠️ 2026-09-14 발견 — 이 강제 전환이 OnBusyTick()을 완전히 건너뛰기 때문에,
                    // [[CubeEnemyGwang]]의 경고 인디케이터처럼 "Busy 도중에만 존재하는 임시 오브젝트"를
                    // 정상적인 EnterAttackActive() 경로 없이는 못 지우는 채로 버려짐 — 42개까지 쌓인
                    // 실제 사례로 발견([[changelog/2026-09-14_광경고인디케이터-누수]]). 강제 전환 시엔
                    // 반드시 OnBusyInterrupted()로 서브클래스에게 정리할 기회를 줍니다.
                    if (!sameFace) { OnBusyInterrupted(); EnterSearching(); break; }
                    OnBusyTick();
                    break;
            }

            // ⚠️ 2026-09-14 발견 — 사용자가 실제 Play 중 여러 몹이 플레이어 한 점으로 몰려서
            // 겹치는 것을 스크린샷으로 리포트("몹들간의 겹침은 있으면 안돼"). 상태와 무관하게
            // 매 프레임 다른 몹과의 최소 거리를 확보합니다.
            Vector3 separation = CubeFaceMobUtils.ComputeSeparation(this, faceNormal, mobSeparationDistance, separationStrength);
            if (separation.sqrMagnitude > 0.0001f)
                transform.position = ClampToFace(transform.position + separation * Time.deltaTime);

            // 원본 [[EnemyBase]].LateUpdate()와 같은 이유 — 상태 이름이 아니라 "지금 실제로
            // 움직이고 있는가"라는 사실 하나로 통일해서 Animator의 "Moving" bool을 갱신합니다.
            if (animator != null) animator.SetBool("Moving", movedThisFrame);
        }

        /// <summary>
        /// Chasing 상태에서 매 프레임 호출됩니다. 기본 구현은 플레이어에게 접근하되
        /// <see cref="minApproachToPlayer"/> 안으로는 안 들어갑니다 — 사거리 안에 들어왔을 때
        /// 공격 등으로 전환하고 싶은 서브클래스는 이걸 오버라이드해서 <see cref="EnterBusy"/>를
        /// 호출하세요(원본 EnemyPyeong.UpdateChase()와 같은 역할).
        /// </summary>
        protected virtual void OnChasingTick()
        {
            MoveToward(target.transform.position, arriveThreshold: minApproachToPlayer);
        }

        /// <summary>Busy 상태(공격 등) 동안 매 프레임 호출됩니다. 끝나면 <see cref="EnterChasing"/> 등을 호출하세요.</summary>
        protected virtual void OnBusyTick() { }

        /// <summary>
        /// Busy 상태가 정상 종료(EnterAttackActive 등)가 아니라 면 이탈로 강제 중단될 때 호출됩니다.
        /// [[CubeEnemyGwang]]의 경고 인디케이터처럼 Busy 도중에만 떠있는 임시 오브젝트를 정리할
        /// 기회입니다 — 안 그러면 정상 경로(예: EnterAttackActive)를 거치지 못해 영원히 남습니다.
        /// </summary>
        protected virtual void OnBusyInterrupted() { }

        protected void EnterChasing() => state = MacroState.Chasing;
        protected void EnterBusy() => state = MacroState.Busy;

        private void EnterSearching()
        {
            state = MacroState.Searching;
            searchTimer = 0f;
        }

        /// <summary>
        /// 월드 좌표 목표를 향해 면 위에서 이동합니다. 도착하면 true를 반환합니다.
        /// `speedOverride`를 주면 <see cref="MoveSpeed"/> 대신 그 속도를 씁니다 — [[CubeEnemyWon]]이
        /// 후퇴할 때만 순간적으로 더 빨리 움직이게 하는 용도([[changelog/2026-09-14_원거리몹-후퇴버그수정]]).
        /// </summary>
        protected bool MoveToward(Vector3 worldTarget, float arriveThreshold = 0f, float? speedOverride = null)
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
                float speed = speedOverride ?? MoveSpeed;

                // ⚠️ 2026-09-17 발견 — 이 스텝을 arriveThreshold 이상으로 못 좁히게 clamp하지
                // 않고 있었음. 평소 프레임에서는 걸음 크기(speed*deltaTime)가 워낙 작아서 문제가
                // 안 보였는데, 디버그 패널에 구슬 개수를 큰 값(예: "901")으로 넣어 AddOrb()를
                // 한 프레임에 900번 넘게 동기 호출하면 그 프레임의 Time.deltaTime이 크게
                // 튀면서(로그 900줄 처리 지연) 이 몹이 minApproachToPlayer/absorbRadius를
                // 훌쩍 넘어 플레이어 코앞까지 순간적으로 파고드는 걸 실측 영상(HUD의 "흡 거리"가
                // 1.20→1.00으로 계속 줄어듦)으로 확인함 — "흡한테 꼈다"는 리포트의 진짜 원인.
                // 한 번의 큰 deltaTime 프레임에도 arriveThreshold보다 더 가까이 못 가도록 클램프.
                float remaining = toTarget.magnitude - arriveThreshold;
                float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(remaining, 0f));

                myLocal += moveDir * step;
                transform.rotation = Quaternion.LookRotation(moveDir, faceNormal);
                movedThisFrame = true;
            }

            transform.position = ClampToFace(center + myLocal);
            return arrived;
        }

        /// <summary>
        /// 접선 두 축은 면 범위 안으로 clamp, 법선 축은 표면 값으로 고정합니다 — 면 밖으로
        /// 못 나가게 하는 로직을 <see cref="MoveToward"/>와 겹침 방지 로직이 공유합니다.
        /// </summary>
        protected Vector3 ClampToFace(Vector3 worldPos)
        {
            Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
            Vector3 local = worldPos - center;
            int axis = Mathf.Abs(faceNormal.x) > 0.5f ? 0 : (Mathf.Abs(faceNormal.y) > 0.5f ? 1 : 2);

            for (int a = 0; a < 3; a++)
            {
                if (a == axis) continue;
                local[a] = Mathf.Clamp(local[a], -cubeHalfExtent, cubeHalfExtent);
            }
            local[axis] = (cubeHalfExtent + surfaceOffset) * Mathf.Sign(faceNormal[axis]);

            return center + local;
        }

        /// <summary>제자리에서 faceNormal 축을 기준으로 좌우로 두리번거립니다(당황한 느낌의 시각 피드백).</summary>
        private void LookAround()
        {
            float angle = Mathf.Sin(searchTimer * lookAroundSpeed) * lookAroundAngle;
            transform.rotation = Quaternion.AngleAxis(angle, faceNormal) * baseRotation;
        }
    }
}
