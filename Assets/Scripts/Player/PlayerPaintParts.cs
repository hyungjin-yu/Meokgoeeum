using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// PlayerPaintParts (플레이어 부위 흑백화 — [[MonsterPaintParts]]의 거울상)
    /// 2026-09-16, 사용자 요청: "플레이어는 몹에게 맞으면 색이 흑백으로 변해. 부위별로. 몹이랑
    /// 같은 부위로 총 7대 맞으면 사망이야." 몹은 맞을 때마다 무작위 부위가 색을 "얻고" 7부위가
    /// 전부 칠해지면 죽는데, 플레이어는 정반대로 맞을 때마다 무작위 부위가 색을 "잃고"(원래 색 →
    /// 흑백) 7부위가 전부 흑백이 되면 죽습니다 — 같은 규칙, 반대 방향.
    ///
    /// 렌더러 분류는 [[MonsterPaintParts.ClassifyByName]]을 그대로 재사용합니다 — 몹처럼
    /// "InkGolem_종_부위" 규칙으로 부위별 렌더러가 쪼개진 모델이면 코드 하나 안 고치고 그대로
    /// 맞물립니다. 다만 지금 테스트 중인 유니티짱 모델은 몹과 완전히 다른 방식(피부/옷을
    /// "재질"별로 통짜 렌더러 하나에 담는 방식 — 예: "skin" 렌더러 하나가 팔+다리+몸통 피부를
    /// 전부 포함)이라 이름 기반 분류가 하나도 안 걸립니다. 그래서 <see cref="ClassifyFallback"/>로
    /// 최소한 7부위 구색은 맞추는 근사 매핑을 추가해뒀습니다 — 실제로는 팔/다리 렌더러가
    /// 좌우로 안 나뉘어 있어서 "오른팔만 맞았는데 다리도 같이 흑백 되는" 식의 부정확함이 있을 수
    /// 있음을 감안하고 쓰는 임시 근사치입니다. 나중에 몹처럼 부위별로 나뉜 최종 플레이어 모델이
    /// 생기면 이 폴백 없이도 정확하게 맞물립니다.
    /// </summary>
    public class PlayerPaintParts : MonoBehaviour
    {
        [Tooltip("한 부위가 완전히 흑백이 되는 데 걸리는 시간입니다.")]
        public float fadeDuration = 0.25f;

        /// <summary>7부위가 전부 흑백이 되는 순간 정확히 한 번 호출됩니다.</summary>
        public event System.Action OnAllDrained;

        public int TotalParts { get; private set; }
        public int DrainedCount => drainedRegions.Count;
        public bool AllDrained => TotalParts > 0 && DrainedCount >= TotalParts;

        /// <summary>남은(아직 흑백 안 된) 부위 비율 — [[MonsterPaintParts.HealthFraction]]과 같은 역할.</summary>
        public float HealthFraction => TotalParts > 0 ? (float)(TotalParts - DrainedCount) / TotalParts : 1f;

        /// <summary>
        /// 2026-09-17 추가 — 인게임 HUD(캐릭터 전신 일러스트, 부위별로 검게 물듦)가 "이 부위가
        /// 이미 흑백됐는지"를 3D 모델 렌더러 색을 안 거치고 직접 물어볼 때 씁니다.
        /// </summary>
        public bool IsRegionDrained(MonsterPaintParts.Region region)
        {
            EnsureBuilt();
            return drainedRegions.Contains(region);
        }

        private readonly Dictionary<MonsterPaintParts.Region, List<Renderer>> regionRenderers = new Dictionary<MonsterPaintParts.Region, List<Renderer>>();
        private readonly Dictionary<MonsterPaintParts.Region, List<Color>> regionOriginalColors = new Dictionary<MonsterPaintParts.Region, List<Color>>();
        private readonly HashSet<MonsterPaintParts.Region> drainedRegions = new HashSet<MonsterPaintParts.Region>();

        private void Awake() => EnsureBuilt();

        /// <summary>
        /// [[MonsterPaintParts.EnsureBuilt]]와 동일한 이유로 "실제 데이터 존재 여부"를 가드로
        /// 씀(도메인 리로드 안전) — 자세한 배경은 그쪽 주석 참고.
        /// </summary>
        private void EnsureBuilt()
        {
            if (regionRenderers.Count > 0) return;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var region = MonsterPaintParts.ClassifyByName(renderer.gameObject.name) ?? ClassifyFallback(renderer.gameObject.name);
                if (region == null) continue;

                if (!regionRenderers.TryGetValue(region.Value, out var list))
                {
                    list = new List<Renderer>();
                    regionRenderers[region.Value] = list;
                    regionOriginalColors[region.Value] = new List<Color>();
                }
                list.Add(renderer);
                regionOriginalColors[region.Value].Add(renderer.material.color);
            }

            TotalParts = regionRenderers.Count;
            if (TotalParts == 0)
                Debug.LogWarning($"[PlayerPaintParts] {name}: 이름으로 분류된 부위가 하나도 없습니다 — 모델 렌더러 이름 규칙을 확인하세요.");
        }

        /// <summary>
        /// 아직 흑백이 안 된 부위 중 하나를 무작위로 골라 흑백으로 물들입니다. 이미 전부
        /// 흑백이면 아무 일도 안 합니다.
        /// </summary>
        public void DrainRandomPart()
        {
            EnsureBuilt();
            if (AllDrained || TotalParts == 0) return;

            var candidates = new List<MonsterPaintParts.Region>();
            foreach (var region in regionRenderers.Keys)
                if (!drainedRegions.Contains(region))
                    candidates.Add(region);

            if (candidates.Count == 0) return;

            var chosen = candidates[Random.Range(0, candidates.Count)];
            drainedRegions.Add(chosen);

            StartCoroutine(FadeRegionToGrayscale(chosen));

            if (AllDrained)
                OnAllDrained?.Invoke();
        }

        private IEnumerator FadeRegionToGrayscale(MonsterPaintParts.Region region)
        {
            var renderers = regionRenderers[region];
            var originals = regionOriginalColors[region];
            var targets = new Color[originals.Count];
            for (int i = 0; i < originals.Count; i++)
                targets[i] = ToGrayscale(originals[i]);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].material.color = Color.Lerp(originals[i], targets[i], t);
                }
                yield return null;
            }

            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null)
                    renderers[i].material.color = targets[i];
        }

        /// <summary>표준 휘도 가중치로 원래 색의 명도를 보존한 무채색을 계산합니다(그냥 검정으로 덮지 않음).</summary>
        private static Color ToGrayscale(Color c)
        {
            float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return new Color(gray, gray, gray, c.a);
        }

        /// <summary>
        /// 유니티짱처럼 "InkGolem_종_부위" 규칙이 전혀 안 맞는 모델을 위한 근사 매핑입니다.
        /// ⚠️ 알려진 부정확함 — 유니티짱은 팔/다리/피부가 좌우 구분 없이 통짜 렌더러 하나씩이라,
        /// 여기서는 "LeftLeg"엔 양다리 전부(Leg 렌더러), "RightLeg"엔 몸 전체 피부(skin 렌더러,
        /// 사실상 팔+다리+몸통 피부 전부)를 억지로 배정해서 7부위 구색만 맞췄습니다 — 실제로 어느
        /// 팔다리를 맞았느냐와 무관하게 뭉뚱그려 반응합니다. 나중에 부위별로 나뉜 모델로 바뀌면
        /// 이 메서드는 아예 안 불려도(위 ClassifyByName만으로 충분) 무방합니다.
        /// </summary>
        private static MonsterPaintParts.Region? ClassifyFallback(string name)
        {
            switch (name)
            {
                case "BLW_DEF": case "eye_base_old": case "EYE_DEF": case "EL_DEF":
                case "eye_L_old": case "eye_R_old": case "head_back": case "MTH_DEF":
                case "cheek": case "hair_accce": case "hair_front": case "hair_frontside": case "hairband":
                    return MonsterPaintParts.Region.Head;
                case "Shirts": case "uwagi": case "uwagi_BK": case "button":
                    return MonsterPaintParts.Region.Chest;
                case "tail": case "tail_bottom":
                    return MonsterPaintParts.Region.Waist;
                case "shirts_sode":
                    return MonsterPaintParts.Region.LeftArm;
                case "shirts_sode_BK":
                    return MonsterPaintParts.Region.RightArm;
                case "Leg":
                    return MonsterPaintParts.Region.LeftLeg;
                case "skin":
                    return MonsterPaintParts.Region.RightLeg;
                default:
                    return null;
            }
        }
    }
}
