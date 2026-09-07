using System.Collections;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// GameOverManager (게임 오버 관리자)
    /// [[17 게임오버 & 리트라이]] — 원 기획은 "처음부터 재시작"이었지만, 개발 편의로 시작한
    /// "죽은 면에서 다시 시작" 방식을 **2026-08-25에 정식 채택**으로 확정했습니다. 패널티 없는
    /// 리트라이 취지에 "죽을 때마다 진행이 다 날아가는 처음부터"보다 이쪽이 더 맞다고 판단 — 상세
    /// 근거는 17번 문서의 정정 메모 참고.
    ///
    /// 흐름: [[PlayerHealth]].OnDeath → "게임 오버" 문구([[NarrationManager]] 재사용) →
    /// [[CubeMapManager]].ReloadCurrentFace()(현재 면 통째로 다시 로드 — 적 전부 리스폰) →
    /// 플레이어 체력 회복.
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        public static GameOverManager Instance { get; private set; }

        [Tooltip("사망 문구를 보여준 뒤 재시작 로직을 시작하기까지 대기하는 시간입니다.")]
        public float messageDelay = 1.5f;

        private PlayerHealth playerHealth;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[GameOverManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;

            if (playerHealth == null)
            {
                Debug.LogWarning("[GameOverManager] Player(PlayerHealth)를 찾지 못해 게임 오버 처리를 구독할 수 없습니다.");
                return;
            }

            playerHealth.OnDeath += HandlePlayerDeath;
        }

        private void HandlePlayerDeath()
        {
            Debug.Log("[GameOverManager] 게임 오버! 리트라이 시작.");
            StartCoroutine(GameOverRoutine());
        }

        private IEnumerator GameOverRoutine()
        {
            NarrationManager.Instance?.ShowNarration("게임 오버... 다시 도전!");
            yield return new WaitForSeconds(messageDelay);

            if (CubeMapManager.Instance != null)
            {
                CubeMapManager.Instance.ReloadCurrentFace();
                yield return new WaitUntil(() => !CubeMapManager.Instance.IsRotating);
            }

            playerHealth.ResetHealth();
        }
    }
}
