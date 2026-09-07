using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Meokgoeeum
{
    /// <summary>
    /// FootstepEffect (발자국 이펙트)
    /// [[15 튜토리얼 설계]] 1단계("바닥에 발자국 이펙트로 WASD 이동 유도")용. 아트 에셋이 없어서
    /// 작은 납작한 Cube를 발 옆에 번갈아 찍고, 시간이 지나면 크기를 0으로 줄이며 사라지게 합니다
    /// ([[ColorWaveEffect]]처럼 알파 페이드 대신 스케일 애니메이션 — URP 머티리얼을 투명 렌더링으로
    /// 바꾸는 번거로움 없이 같은 "옅어지며 사라짐" 느낌을 냄).
    ///
    /// 튜토리얼 전용으로 구역을 한정하지 않고 Player에 상시 붙여둔 상시 이동 이펙트로 구현했습니다
    /// — 어차피 진행에 영향 없는 순수 연출이라, 굳이 1층 복도에서만 켜고 끄는 트리거를 따로 두는
    /// 것보다 "걸을 때마다 항상 나는" 게 더 자연스럽고("환경으로 자연스럽게 유도" 원칙) 코드도 단순합니다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepEffect : MonoBehaviour
    {
        [Tooltip("발자국 사이의 시간 간격입니다.")]
        public float stepInterval = 0.35f;

        [Tooltip("좌우 발자국이 몸 중심선에서 벌어지는 거리입니다.")]
        public float stepSideOffset = 0.15f;

        [Tooltip("발자국이 사라지는 데 걸리는 시간입니다.")]
        public float fadeDuration = 1.2f;

        [Tooltip("발자국 하나의 크기입니다 (가로 x 두께 x 세로).")]
        public Vector3 markSize = new Vector3(0.13f, 0.02f, 0.22f);

        [Tooltip("이동으로 인정할 최소 수평 속도입니다. 이보다 느리면(제자리/카메라 회전만) 발자국을 안 찍습니다.")]
        public float minMoveSpeed = 0.5f;

        private CharacterController cc;
        private float stepTimer;
        private bool leftFoot;
        private Vector3 lastPosition;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            lastPosition = transform.position;
        }

        // 2026-08-31 실측: 원래 cc.velocity로 이동을 감지했는데 발자국이 아예 안 찍힘 —
        // PlayerController.Update()가 매 프레임 cc.Move()를 두 번 부르는데(이동 → 중력 순서),
        // CharacterController.velocity는 "가장 마지막 Move() 호출" 기준으로 갱신되고 마지막이
        // 중력(수직 전용)이라 수평 성분이 거의 항상 0으로 읽혔던 것. cc.velocity에 기대는 대신
        // 프레임 간 실제 위치 변화량으로 직접 이동을 감지하도록 바꿈 — Move() 호출 횟수/순서와
        // 무관하게 항상 정확함.
        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;

            float horizontalSpeed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;

            if (horizontalSpeed < minMoveSpeed)
            {
                stepTimer = 0f; // 멈추면 타이머 리셋 — 다시 걷기 시작하면 바로 첫 발자국이 찍히게
                return;
            }

            stepTimer += Time.deltaTime;
            if (stepTimer < stepInterval) return;
            stepTimer = 0f;

            SpawnFootprint();
        }

        private void SpawnFootprint()
        {
            leftFoot = !leftFoot;

            Vector3 sideDir = transform.right * (leftFoot ? -1f : 1f) * stepSideOffset;
            Vector3 position = transform.position + sideDir;
            position.y = transform.position.y - (cc.height * 0.5f) + 0.01f; // 발밑, 바닥에서 살짝 띄워서 Z-fighting 방지

            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "Footprint";
            Object.Destroy(mark.GetComponent<Collider>()); // 발자국이 플레이어/장애물과 충돌하면 안 됨

            // 다른 런타임 생성 오브젝트들과 같은 이유 — CreatePrimitive는 "지금 활성 씬"에 생성되므로
            // 명시적으로 플레이어와 같은 씬 소속으로 맞춰줍니다.
            SceneManager.MoveGameObjectToScene(mark, gameObject.scene);

            mark.transform.position = position;
            mark.transform.rotation = transform.rotation;
            mark.transform.localScale = markSize;

            var renderer = mark.GetComponent<Renderer>();
            renderer.material.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            StartCoroutine(FadeAndDestroy(mark));
        }

        private IEnumerator FadeAndDestroy(GameObject mark)
        {
            Vector3 startScale = mark.transform.localScale;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (mark == null) yield break; // 씬 전환 등으로 먼저 없어졌으면 중단
                mark.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / fadeDuration);
                yield return null;
            }
            if (mark != null) Destroy(mark);
        }
    }
}
