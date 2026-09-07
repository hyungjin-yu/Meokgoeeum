using UnityEngine;

/// <summary>
/// InkProjectile (먹괴음 - 원의 먹물 투사체)
/// [[EnemyWon]]이 던진 먹물이 직선으로 날아가다가 Player에 맞으면 대미지를 주고 사라집니다.
/// 아트 에셋이 없어서 [[ColorOrbPool]]처럼 런타임에 프리미티브로 생성됩니다 — EnemyWon.ThrowInk()가
/// 만들면서 Launch()로 방향/대미지를 넘겨줍니다.
///
/// 최적화 메모: 지금은 매번 새로 생성/파괴합니다(풀링 안 함). 던지는 빈도가 낮은
/// 원거리 몹 1종뿐이라 지금은 병목이 아닐 걸로 판단 — 나중에 원거리 몹이 많아지면
/// ColorOrbPool과 같은 방식으로 풀링할 것.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class InkProjectile : MonoBehaviour
{
    [Tooltip("맞았을 때 주는 대미지입니다.")]
    public float damage = 10f;

    [Tooltip("날아가는 속도입니다. (Unity units/s)")]
    public float speed = 12f;

    [Tooltip("아무것도 안 맞고 이 시간이 지나면 자동으로 사라집니다. (메모리 누수 방지)")]
    public float lifetime = 3f;

    private Vector3 direction = Vector3.forward;

    /// <summary>생성 직후 EnemyWon이 호출해서 방향/대미지를 세팅합니다.</summary>
    public void Launch(Vector3 travelDirection, float dmg)
    {
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector3.forward;
        damage = dmg;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        other.GetComponent<IDamageable>()?.TakeDamage(damage);
        Destroy(gameObject);
    }
}
