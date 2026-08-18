using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ColorWaveEffect (색 복원 파동)
/// 색 구슬을 획득한 지점에서 구형으로 퍼져나가며, 주변 [[PaintableObject]]들을
/// 가까운 순서대로 칠합니다. [[11 셰이더 설계 - 색 복원 파동]] "동작 원리" 참고
/// (Shader Graph 대신 C#으로 구현한 버전 — 이유는 PaintableObject.cs 주석 참고).
/// </summary>
public class ColorWaveEffect : MonoBehaviour
{
    public static ColorWaveEffect Instance { get; private set; }

    [Tooltip("파동이 최대 반경까지 다 퍼지는 데 걸리는 시간입니다. (11 문서 기준값)")]
    public float waveDuration = 1.5f;

    [Tooltip("파동이 도달하는 최대 반경입니다. (11 문서 기준값)")]
    public float maxRadius = 30f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[ColorWaveEffect] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// origin에서 파동을 시작합니다.
    /// </summary>
    public void PlayWave(Vector3 origin)
    {
        Debug.Log($"색 복원 파동 발생! 위치: {origin}");
        StartCoroutine(WaveRoutine(origin));
    }

    private IEnumerator WaveRoutine(Vector3 origin)
    {
        // 최적화: 파동이 퍼지는 매 프레임마다 OverlapSphere를 다시 하지 않고,
        // 시작 시점에 최대 반경으로 1번만 조회해서 거리순 정렬 후 순서대로 소비합니다.
        Collider[] hits = Physics.OverlapSphere(origin, maxRadius);

        var targets = new List<(PaintableObject obj, float dist)>();
        foreach (var hit in hits)
        {
            var paintable = hit.GetComponent<PaintableObject>();
            if (paintable != null)
                targets.Add((paintable, Vector3.Distance(origin, hit.transform.position)));
        }

        if (targets.Count == 0) yield break;

        targets.Sort((a, b) => a.dist.CompareTo(b.dist));

        float elapsed = 0f;
        int nextIndex = 0;

        while (elapsed < waveDuration && nextIndex < targets.Count)
        {
            elapsed += Time.deltaTime;
            float currentRadius = Mathf.Lerp(0f, maxRadius, elapsed / waveDuration);

            while (nextIndex < targets.Count && targets[nextIndex].dist <= currentRadius)
            {
                targets[nextIndex].obj.Paint();
                nextIndex++;
            }

            yield return null;
        }

        // 파동 지속시간이 끝났는데 아직 안 칠해진 먼 오브젝트가 남았으면 마무리로 다 칠함
        while (nextIndex < targets.Count)
        {
            targets[nextIndex].obj.Paint();
            nextIndex++;
        }
    }
}
