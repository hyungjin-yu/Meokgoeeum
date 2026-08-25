using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DamageZone (장판)
/// [[BossPo]] "검정: 장판 생성" 공격 구현. 바닥에 생기는 위험 구역 — 그 안에 있는 동안
/// 일정 주기로 대미지를 줍니다. 지속시간이 끝나면 자동으로 사라집니다.
/// [[InkProjectile]]처럼 런타임 프리미티브로 생성됩니다(아트 에셋 없음).
/// </summary>
public class DamageZone : MonoBehaviour
{
    [Tooltip("초당 대미지입니다. tickInterval마다 이 값 × tickInterval만큼 나눠서 줍니다.")]
    public float damagePerSecond = 10f;

    [Tooltip("대미지를 주는 주기입니다.")]
    public float tickInterval = 0.5f;

    [Tooltip("장판이 유지되는 시간입니다.")]
    public float lifetime = 4f;

    private readonly List<Collider> targetsInside = new List<Collider>();
    private float tickTimer;

    /// <summary>
    /// 런타임으로 장판을 만듭니다. 아트 에셋이 없어서 납작한 실린더로 표시합니다.
    /// ⚠️ 2026-08-25: `CreatePrimitive`는 부모를 지정할 수 없어서(=Instantiate처럼 parent 인자가
    /// 없음) 기본적으로 "지금 활성 씬"에 생성됩니다 — [[changelog/2026-08-24_재방문-랜덤인카운터]]에서
    /// 발견한 씬 소속 버그와 같은 종류. 장판은 시전자를 따라 움직이면 안 되므로(위치 고정) parent로
    /// 붙이지 않고, `SceneManager.MoveGameObjectToScene`으로 씬 소속만 시전자 기준으로 맞춥니다.
    /// </summary>
    public static DamageZone Create(Vector3 position, float radius, float damagePerSecond, float lifetime, Scene targetScene)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = "DamageZone";
        obj.transform.position = position;
        obj.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f); // 납작하게
        if (targetScene.IsValid())
            SceneManager.MoveGameObjectToScene(obj, targetScene);

        var renderer = obj.GetComponent<Renderer>();
        renderer.material.color = new Color(0.1f, 0.1f, 0.1f, 1f); // 먹물 웅덩이 느낌의 검정

        var col = obj.GetComponent<CapsuleCollider>(); // 실린더 기본 콜라이더를 그대로 트리거로 씀 (localScale에 맞춰 자동으로 늘어남)
        col.isTrigger = true;

        var rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var zone = obj.AddComponent<DamageZone>();
        zone.damagePerSecond = damagePerSecond;
        zone.lifetime = lifetime;

        Debug.Log($"[DamageZone] 장판 생성! 위치: {position}, 반경: {radius}");
        return zone;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (targetsInside.Count == 0) return;

        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) return;
        tickTimer = 0f;

        float tickDamage = damagePerSecond * tickInterval;
        for (int i = targetsInside.Count - 1; i >= 0; i--)
        {
            var col = targetsInside[i];
            if (col == null)
            {
                targetsInside.RemoveAt(i);
                continue;
            }

            var damageable = col.GetComponent<IDamageable>();
            damageable?.TakeDamage(tickDamage);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!targetsInside.Contains(other)) targetsInside.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        targetsInside.Remove(other);
    }
}
