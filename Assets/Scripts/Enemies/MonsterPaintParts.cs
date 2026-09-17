using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// MonsterPaintParts (몹 부위 색칠 처치 시스템)
    /// 2026-09-16, 사용자 요청으로 몬스터 HP를 재해석 — "몹이 플레이어에게 맞을 때마다 랜덤한
    /// 부위가 색이 부여되고, 모든 부위의 색이 부여되면 몹은 처치되는거야." 숫자 체력 대신
    /// 몸을 7개 부위(머리/가슴/허리/좌우팔/좌우다리)로 나눠서, 맞을 때마다(콤보 타수/공격력과
    /// 무관하게 항상 딱 1부위) 아직 안 칠해진 부위 중 하나를 무작위로 골라 색칠합니다. 7부위가
    /// 전부 칠해지면 처치 — [[EnemyHealth]]/[[CubeEnemyBase]]가 이 컴포넌트가 있으면 그쪽으로
    /// 위임하고, 없으면(아직 이 컴포넌트를 안 붙인 다른 종) 기존 숫자 체력 그대로 동작합니다.
    ///
    /// 먹괴음은 "색을 먹어치운 먹물 괴물"이라 원래 어둡고 무채색인 게 자연스러운 기본 상태입니다
    /// — 그래서 이 컴포넌트는 시작할 때 억지로 회색으로 만들지 않고, 각 부위의 "원래(칠해지기
    /// 전) 색"을 그대로 캡처해뒀다가 맞을 때마다 무작위 색 구슬 색([[OrbColorPalette]])으로
    /// 서서히 물들입니다 — "때릴 때마다 빼앗겼던 색을 되찾아온다"는 [[24 디자인 필러]] 필러 1
    /// ("색은 감정의 증거다")과 정확히 맞아떨어지는 연출.
    ///
    /// `Renderer` 오브젝트 이름을 보고 7부위로 자동 분류합니다(예: "InkGolem_Pyeong_L_UpperArm"
    /// → 왼팔) — 종 이름 접두사와 무관하게 접미사 패턴만 보므로, 같은 파이프라인으로 만든 다른
    /// 종에도 그대로 재사용될 가능성이 높습니다(단, 실제로는 아직 평/Pyeong에만 검증·적용함).
    /// </summary>
    public class MonsterPaintParts : MonoBehaviour
    {
        public enum Region { Head, Chest, Waist, LeftArm, RightArm, LeftLeg, RightLeg }

        [Tooltip("한 부위가 완전히 칠해지는 데 걸리는 시간입니다.")]
        public float fadeDuration = 0.25f;

        /// <summary>7부위가 전부 칠해지는 순간 정확히 한 번 호출됩니다.</summary>
        public event System.Action OnAllPainted;

        public int TotalParts { get; private set; }

        // 2026-09-16 — PaintedCount를 별도 int로 따로 세지 않고 paintedRegions.Count에서 그때그때
        // 계산합니다. 굳이 분리했다가 도메인 리로드로 paintedRegions(HashSet, 못 되살아남)만
        // 비워지고 별도 int는 살아남는 식으로 둘이 어긋나는 사고를 원천 차단하기 위함 — 데이터
        // 원본이 하나면애초에 불일치가 생길 수가 없습니다.
        public int PaintedCount => paintedRegions.Count;
        public bool AllPainted => TotalParts > 0 && PaintedCount >= TotalParts;

        /// <summary>
        /// 2026-09-16 추가 — "남은 부위 비율"(1=아무 데도 안 칠해짐, 0=전부 칠해짐). [[EnemyHeup]]처럼
        /// 숫자 체력 비율(예: "50% 밑으로 떨어지면")로 행동을 바꾸던 종이 이 값을 쓰면, 부위 색칠
        /// 체계에서도 같은 "저체력 판정" 의미를 유지할 수 있습니다 — currentHP/maxHP를 직접 읽으면
        /// (TakeDamage가 그 필드들을 더는 안 건드리므로) 절대 안 맞는 판정이 되는 버그를 여기서 막음.
        /// </summary>
        public float HealthFraction => TotalParts > 0 ? (float)(TotalParts - PaintedCount) / TotalParts : 1f;

        private readonly Dictionary<Region, List<Renderer>> regionRenderers = new Dictionary<Region, List<Renderer>>();
        private readonly Dictionary<Region, List<Color>> regionOriginalColors = new Dictionary<Region, List<Color>>();
        private readonly HashSet<Region> paintedRegions = new HashSet<Region>();

        private void Awake() => EnsureBuilt();

        /// <summary>
        /// 렌더러를 이름으로 7부위에 분류합니다. `EnemyHealth`/`CubeEnemyBase`가 아직 `Awake()`
        /// 순서를 보장 안 해줄 수 있어서, 실제로 쓰기 직전에 항상 이걸 먼저 부르면 안전합니다
        /// (이미 됐으면 그냥 반환).
        ///
        /// ⚠️ 2026-09-16 발견 — 처음엔 `private bool built`만으로 "이미 했는지"를 판단했는데,
        /// Play 모드 도중 스크립트가 재컴파일되면(도메인 리로드) Unity가 `Dictionary`/`HashSet`
        /// 필드는 못 되살리면서(네이티브 직렬화 대상이 아님) `bool`/`int` 같은 단순 필드는 그대로
        /// 되살려서, `built=true`인데 `regionRenderers`는 텅 빈 상태로 불일치가 생기는 걸
        /// 리플렉션 테스트로 실제 재현함 — 그 뒤로 `PaintRandomPart()`가 후보 0개로 조용히
        /// 아무 일도 안 하는 채로 고정됨(맞아도 영원히 안 죽는 버그). `built` 플래그 대신
        /// "실제로 데이터가 들어있는가"를 직접 확인하도록 바꿔서, 도메인 리로드로 비워졌으면
        /// 자동으로 다시 채웁니다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (regionRenderers.Count > 0) return;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var region = ClassifyByName(renderer.gameObject.name);
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
                Debug.LogWarning($"[MonsterPaintParts] {name}: 이름으로 분류된 부위가 하나도 없습니다 — 모델 렌더러 이름 규칙을 확인하세요.");
        }

        /// <summary>
        /// 2026-09-17 추가 — [[EnemyBun]]/[[CubeEnemyBun]]처럼 죽으면서 `Instantiate(gameObject, ...)`로
        /// 자기 자신을 복제해 미니언을 만드는 종에서, 죽기 직전 부위가 이미 칠해져 있었으면
        /// 그 색이 그대로 복제돼서 미니언이 "이미 칠해진 채로" 태어나는 버그가 있었음(사용자
        /// 리포트: "분열체가 나올 때 왜 색이 부여되어있어? 검은색이어야하는데"). 원인: 복제된
        /// GameObject의 렌더러 머티리얼 색은 복제 시점 그대로 상속되는데, 새 인스턴스의
        /// `MonsterPaintParts.Awake()`(EnsureBuilt)가 그 상속된 색을 "원본(칠해지기 전) 색"으로
        /// 잘못 캡처해버림. 복제하기 직전에 죽는 부모의 렌더러 색을 진짜 원본으로 되돌려두면,
        /// 복제된 미니언이 올바른 원본 색을 물려받아 정상적으로 시작합니다.
        /// </summary>
        public void ResetVisualsToOriginal()
        {
            EnsureBuilt();
            foreach (var region in regionRenderers.Keys)
            {
                var renderers = regionRenderers[region];
                var originals = regionOriginalColors[region];
                for (int i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null)
                        renderers[i].material.color = originals[i];
            }
        }

        /// <summary>
        /// 아직 안 칠해진 부위 중 하나를 무작위로 골라 칠합니다. 이미 전부 칠해졌으면 아무 일도
        /// 안 합니다(호출하는 쪽이 `AllPainted`를 매번 확인할 필요 없이 안전하게 반복 호출 가능).
        /// </summary>
        public void PaintRandomPart()
        {
            EnsureBuilt();
            if (AllPainted || TotalParts == 0) return;

            var candidates = new List<Region>();
            foreach (var region in regionRenderers.Keys)
                if (!paintedRegions.Contains(region))
                    candidates.Add(region);

            if (candidates.Count == 0) return; // 방어적 — AllPainted 체크와 논리상 겹치지만 혹시 몰라서

            Region chosen = candidates[Random.Range(0, candidates.Count)];
            paintedRegions.Add(chosen);

            Color paintColor = OrbColorPalette.GetRandomVividColor(); // 2026-09-16: 검정은 제외(칠해도 안 보임)
            StartCoroutine(FadeRegionToColor(chosen, paintColor));

            if (AllPainted)
                OnAllPainted?.Invoke();
        }

        /// <summary>
        /// 2026-09-16 추가 — 이미 칠해진 부위 중 하나를 무작위로 골라 원래(칠해지기 전) 색으로
        /// 되돌립니다. [[EnemyHeup]]/[[CubeEnemyHeup]]의 "회복"이 이 체계에서 뜻하는 바 —
        /// `PaintRandomPart()`의 정반대 동작. 이미 하나도 안 칠해진 상태면 아무 일도 안 합니다
        /// (호출하는 쪽이 매번 확인할 필요 없이 안전하게 반복 호출 가능, PaintRandomPart와 동일한 관례).
        /// </summary>
        public void HealRandomPart()
        {
            EnsureBuilt();
            if (paintedRegions.Count == 0) return;

            var candidates = new List<Region>(paintedRegions);
            Region chosen = candidates[Random.Range(0, candidates.Count)];
            paintedRegions.Remove(chosen);

            StartCoroutine(FadeRegionToOriginal(chosen));
        }

        private IEnumerator FadeRegionToOriginal(Region region)
        {
            var renderers = regionRenderers[region];
            var originals = regionOriginalColors[region];

            // 지금 실제로 보이는 색(대략 마지막으로 칠해진 색)에서 시작 — PaintRandomPart()가
            // 매번 새 무작위 색으로 칠하므로 "현재 색"을 미리 캡처해둔 값으로 가정할 수 없습니다.
            var startColors = new Color[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
                startColors[i] = renderers[i] != null ? renderers[i].material.color : originals[i];

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].material.color = Color.Lerp(startColors[i], originals[i], t);
                }
                yield return null;
            }

            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null)
                    renderers[i].material.color = originals[i];
        }

        private IEnumerator FadeRegionToColor(Region region, Color target)
        {
            var renderers = regionRenderers[region];
            var originals = regionOriginalColors[region];
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null) continue; // 페이드 도중 파괴될 수 있음(예: 사망 처리와 겹침)
                    renderers[i].material.color = Color.Lerp(originals[i], target, t);
                }
                yield return null;
            }

            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null)
                    renderers[i].material.color = target;
        }

        /// <summary>
        /// 렌더러 오브젝트 이름으로 7부위 중 하나를 판정합니다. 종 접두사(예: "Pyeong")와
        /// 무관하게 접미사 패턴만 봅니다 — 눈/턱드립처럼 부위라기보단 장식인 것들은 가장 가까운
        /// 구조적 부위(머리)로 편입시킵니다.
        ///
        /// 2026-09-16 — `internal`로 노출해서 [[PlayerPaintParts]](플레이어 피격 시 흑백화)가
        /// 같은 분류 규칙을 재사용할 수 있게 함 — 몹처럼 부위별로 렌더러가 쪼개진 모델이면
        /// 코드 중복 없이 그대로 맞물립니다.
        /// </summary>
        internal static Region? ClassifyByName(string name)
        {
            if (name.Contains("Chest")) return Region.Chest;
            if (name.Contains("Waist") || name.Contains("Pelvis")) return Region.Waist;
            if (name.Contains("Head") || name.Contains("Neck") || name.Contains("Eye") || name.Contains("ChinDrip")) return Region.Head;

            if (name.Contains("L_Thigh") || name.Contains("L_Calf") || name.Contains("L_Foot")) return Region.LeftLeg;
            if (name.Contains("R_Thigh") || name.Contains("R_Calf") || name.Contains("R_Foot")) return Region.RightLeg;
            if (name.Contains("L_UpperArm") || name.Contains("L_Forearm") || name.Contains("L_Hand") || name.Contains("L_Drip")) return Region.LeftArm;
            if (name.Contains("R_UpperArm") || name.Contains("R_Forearm") || name.Contains("R_Hand") || name.Contains("R_Drip")) return Region.RightArm;

            return null; // 분류 실패 — 이 렌더러는 색칠 대상에서 빠짐(안전한 기본값)
        }
    }
}
