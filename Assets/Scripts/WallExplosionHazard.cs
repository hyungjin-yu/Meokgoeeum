using System.Collections;
using UnityEngine;

/// <summary>
/// WallExplosionHazard (벽 폭발 페널티 — 4층 전용)
/// [[19 층별 상세 설계]] 4층 "흡이 색 복원 구역에 접근하기 전에 처치"라는 계단 조건을
/// 실제 페널티로 구현한 것입니다. [[EnemyHeup]].OnAbsorbStart(회복 구역 도달)를 구독해서,
/// 짧은 경고 유예 시간을 준 뒤에도 그 흡이 여전히 살아있으면(=플레이어가 제때 못 끊었으면)
/// 벽이 터져서 플레이어가 즉사합니다. 반대로 유예 시간 안에 흡을 처치하면 발동이 취소됩니다.
///
/// [[GameOverManager]]가 씬에 있어야 이 사망이 실제로 리트라이로 이어집니다.
/// [[27 전투 프레임 데이터]]의 텔레그래프 원칙에 맞춰, 즉발이 아니라 경고 시간을 둔 것은
/// 이 스크립트에서 추가한 설계입니다(기획 문서엔 유예 시간 명시 없음 — 공정성을 위한 보강).
/// </summary>
public class WallExplosionHazard : MonoBehaviour
{
    [Tooltip("이 흡을 구독합니다. 이 흡이 색 복원 구역에 도달하면 카운트다운이 시작됩니다.")]
    public EnemyHeup heup;

    [Tooltip("흡이 구역에 도달한 뒤, 플레이어가 못 끊으면 벽이 터지기까지의 유예 시간입니다.")]
    public float warningDelay = 1.5f;

    [Tooltip("폭발 연출용(선택) — 지정하면 폭발 시 이 오브젝트를 파괴합니다. 안 넣어도 페널티 자체엔 지장 없음.")]
    public GameObject wallVisual;

    private void Start()
    {
        if (heup == null)
        {
            Debug.LogWarning($"[WallExplosionHazard] {name}: Heup이 비어있어 페널티가 절대 발동하지 않습니다. Inspector에서 확인하세요.");
            return;
        }

        heup.OnAbsorbStart += HandleAbsorbStart;
    }

    private void HandleAbsorbStart()
    {
        Debug.LogWarning("[WallExplosionHazard] 흡이 색 복원 구역에 도달했다! 지금 못 끊으면 벽이 터진다...");
        StartCoroutine(WarnThenExplode());
    }

    private IEnumerator WarnThenExplode()
    {
        yield return new WaitForSeconds(warningDelay);

        // 유예 시간 동안 흡이 처치됐으면(Destroy) heup은 Unity의 fake-null이 되어 여기서 걸러짐
        if (heup == null)
        {
            Debug.Log("[WallExplosionHazard] 유예 시간 안에 흡이 처치됨 — 폭발 취소.");
            yield break;
        }

        Explode();
    }

    private void Explode()
    {
        Debug.Log("[WallExplosionHazard] 벽 폭발! 플레이어 즉사 판정.");

        if (wallVisual != null) Destroy(wallVisual);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null)
            health.TakeDamage(health.maxHP); // 즉사 처리 — 무적(구르기 i-frame) 중이면 무시됨(의도된 완화)
    }
}
