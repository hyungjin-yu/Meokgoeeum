using System.Collections;
using UnityEngine;

/// <summary>
/// GameOverManager (게임 오버 관리자)
/// [[17 게임오버 & 리트라이]] 최소 버전 — 원 기획은 "처음부터 재시작"이지만, **지금은 사용자
/// 요청으로 "죽은 층에서 다시 시작"으로 구현**했습니다. 레벨 콘텐츠를 층마다 만들어가는 지금
/// 단계에서 매번 1층부터 다시 하면 테스트 흐름이 심하게 끊기기 때문 — 나중에 진짜 "처음부터"로
/// 바꾸고 싶으면 아래 `HandlePlayerDeath()`에서 `CubeMapManager.ReloadCurrentFace()` 호출을
/// "1층부터 다시" 로직으로 바꾸면 됩니다(예: GameState.floorNumber 리셋 + startingFaceIndex로 이동).
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
