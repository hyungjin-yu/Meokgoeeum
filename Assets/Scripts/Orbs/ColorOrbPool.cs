using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ColorOrbPool (색 구슬 오브젝트 풀)
/// 색 구슬은 적을 처치할 때마다 계속 생성/회수되는 대표적인 사례라
/// [[CLAUDE.md]] 최적화 원칙("자주 생성/파괴되는 오브젝트는 처음부터 풀링 구조 고려")에
/// 따라 Instantiate/Destroy 대신 풀링으로 구현했습니다.
///
/// 아직 실제 구슬 아트 에셋이 없어서, 모양은 런타임에 만든 기본 Sphere를 씁니다.
/// 나중에 실제 모델이 생기면 CreateOrb() 안의 프리미티브 생성 부분만 프리팹
/// Instantiate로 바꾸면 됩니다 (바깥 API는 그대로 유지됨).
/// </summary>
public class ColorOrbPool : MonoBehaviour
{
    public static ColorOrbPool Instance { get; private set; }

    [Tooltip("풀 초기 크기입니다. 부족해지면 자동으로 새로 만들어서 늘어납니다.")]
    public int initialSize = 10;

    [Tooltip("구슬 크기(지름)입니다.")]
    public float orbScale = 0.4f;

    private readonly Queue<ColorOrbPickup> pool = new Queue<ColorOrbPickup>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[ColorOrbPool] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < initialSize; i++)
            pool.Enqueue(CreateOrb());
    }

    private ColorOrbPickup CreateOrb()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ColorOrb (Pooled)";
        go.transform.localScale = Vector3.one * orbScale;
        go.transform.SetParent(transform, false);

        // Sphere 기본 콜라이더를 트리거로 전환 (플레이어가 그냥 지나가면서 주울 수 있어야 함)
        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;

        // CharacterController와의 트리거 감지를 안정적으로 받기 위한 Kinematic Rigidbody
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var pickup = go.AddComponent<ColorOrbPickup>();
        go.SetActive(false);
        return pickup;
    }

    /// <summary>
    /// 풀에서 구슬 하나를 꺼내 지정 위치에 활성화합니다. 부족하면 새로 만듭니다.
    /// </summary>
    public ColorOrbPickup Get(Vector3 position, OrbColor color)
    {
        ColorOrbPickup orb = pool.Count > 0 ? pool.Dequeue() : CreateOrb();

        orb.transform.SetParent(null);
        orb.transform.position = position;
        orb.gameObject.SetActive(true);
        orb.Init(color, this);

        return orb;
    }

    /// <summary>
    /// 다 쓴 구슬을 비활성화하고 풀로 돌려보냅니다. (Destroy 하지 않음)
    /// </summary>
    public void Release(ColorOrbPickup orb)
    {
        orb.gameObject.SetActive(false);
        orb.transform.SetParent(transform, false);
        pool.Enqueue(orb);
    }
}
