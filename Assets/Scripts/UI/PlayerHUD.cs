using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Meokgoeeum
{
    /// <summary>
    /// PlayerHUD (인게임 HUD — 캐릭터 부위 피격 표시 + 스킬 구슬)
    /// [[23 UI 디자인 기획서 (Stitch AI용)]]의 "인게임 HUD" 절을 사용자와 목업(Artifact)으로
    /// 여러 차례 다듬은 최종안 그대로 구현합니다:
    /// - 좌상단: 숫자 HP바 대신 캐릭터 전신 일러스트 — 맞은 부위([[PlayerPaintParts]])가 검게 변함.
    ///   (지금은 도형 조합 placeholder — 최종 캐릭터 모델링이 나오면 그 렌더/스프라이트로 교체하고
    ///   부위별로 검게 물드는 이 로직만 그대로 이어붙이면 됩니다.)
    /// - 하단 중앙: 색 스킬 구슬 6개. 보유 구슬 인벤토리는 따로 없음 — 구슬이 없으면 그 스킬
    ///   슬롯 자체가 비활성으로 보여서 알려줍니다("구슬이 없으면 스킬은 비활성화하면 된다"는
    ///   사용자 피드백). 쿨타임은 숫자 없이 어두운 쐐기(radial wipe)로만 표시.
    ///
    /// [[HintPopupManager]]/[[SettingsMenu]]와 동일한 이유로 UI를 손으로 안 만들고 코드로
    /// 직접 생성합니다(아직 UI 아트 에셋이 없음) — 최종 아트가 생기면 스프라이트만 갈아끼우면 됨.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Tooltip("비워두면 Awake()에서 씬의 PlayerPaintParts를 자동으로 찾습니다.")]
        public PlayerPaintParts paintParts;

        [Tooltip("비워두면 Awake()에서 씬의 ColorSkillController를 자동으로 찾습니다.")]
        public ColorSkillController skills;

        // v0.2 범위 — 초록/보라/검정은 아직 스킬 자체가 구현 안 됨([[ColorSkillController]]
        // 참고, TryCast 스위치에 케이스가 없음) — 그 3개는 구슬 보유 여부와 무관하게 항상
        // "미구현" 잠금으로 표시합니다.
        private static readonly OrbColor[] SkillOrder =
        {
            OrbColor.Red, OrbColor.Blue, OrbColor.Yellow, OrbColor.Green, OrbColor.Purple, OrbColor.Black,
        };
        private static readonly HashSet<OrbColor> ImplementedSkills = new HashSet<OrbColor>
        {
            OrbColor.Red, OrbColor.Blue, OrbColor.Yellow,
        };

        private static readonly Dictionary<OrbColor, Color> OrbUIColors = new Dictionary<OrbColor, Color>
        {
            { OrbColor.Red, new Color(0.898f, 0.224f, 0.208f) },
            { OrbColor.Blue, new Color(0.118f, 0.533f, 0.898f) },
            { OrbColor.Yellow, new Color(0.992f, 0.847f, 0.208f) },
            { OrbColor.Green, new Color(0.263f, 0.627f, 0.278f) },
            { OrbColor.Purple, new Color(0.557f, 0.141f, 0.667f) },
            { OrbColor.Black, new Color(0.129f, 0.129f, 0.129f) },
        };

        private static readonly Color DrainedColor = new Color(0.078f, 0.078f, 0.078f); // #141414 — 목업과 동일
        private static readonly Color LockedTint = new Color(0.32f, 0.32f, 0.32f); // 구슬 없음/미구현 — 어둡게

        private readonly Dictionary<MonsterPaintParts.Region, (Image image, Color baseColor)> regionImages
            = new Dictionary<MonsterPaintParts.Region, (Image, Color)>();
        private readonly Dictionary<OrbColor, Image> skillBaseImages = new Dictionary<OrbColor, Image>();
        private readonly Dictionary<OrbColor, Image> skillWedgeImages = new Dictionary<OrbColor, Image>();
        private readonly Dictionary<OrbColor, GameObject> skillGlowObjects = new Dictionary<OrbColor, GameObject>();

        private Sprite roundedSprite; // 몸통/팔다리(둥근 사각형)
        private Sprite circleSprite;  // 머리/구슬(원형)

        private void Awake()
        {
            if (paintParts == null) paintParts = FindFirstObjectByType<PlayerPaintParts>();
            if (skills == null) skills = FindFirstObjectByType<ColorSkillController>();

            // 에디터 내장 UI 스프라이트(UISprite.psd 등)는 런타임에서 Resources로 못 불러옴
            // (에디터 전용 리소스) — 대신 코드로 직접 텍스처를 구워서 씀. 최종 아트가 생기면
            // 이 두 스프라이트만 교체하면 됨.
            roundedSprite = CreateRoundedRectSprite();
            circleSprite = CreateCircleSprite();

            BuildCanvas();
        }

        private static Sprite CreateCircleSprite(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = new Color32(255, 255, 255, dist <= radius ? (byte)255 : (byte)0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRoundedRectSprite(int size = 64, int cornerRadius = 14)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, IsInsideRoundedRect(x, y, size, cornerRadius) ? (byte)255 : (byte)0);
            tex.SetPixels32(pixels);
            tex.Apply();
            var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int r)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float left = r, right = size - r, top = r, bottom = size - r;
            float cx = px < left ? left : (px > right ? right : px);
            float cy = py < top ? top : (py > bottom ? bottom : py);
            return Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) <= r;
        }

        private void Update()
        {
            UpdateBodyHud();
            UpdateSkillHud();
        }

        // ================= 빌드 =================

        private void BuildCanvas()
        {
            var canvasObj = new GameObject("PlayerHUD_Canvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // 항상 떠있는 기본 HUD — 힌트(910)/설정(950)보다 훨씬 아래

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // 23 UI 디자인 기획서 기준 해상도
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            BuildBodyHud(canvasObj.transform);
            BuildSkillRow(canvasObj.transform);
        }

        /// <summary>
        /// 좌상단 캐릭터 부위 표시. 7부위를 대략적인 실루엣 배치로 둔 도형 조합 — 최종 모델
        /// 렌더가 생기면 이 영역을 통째로 교체하면 됩니다.
        /// </summary>
        private void BuildBodyHud(Transform canvasTransform)
        {
            var root = new GameObject("BodyHUD", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasTransform, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(24f, -16f);
            root.sizeDelta = new Vector2(150f, 190f);

            // (part, 중심 좌표(root 기준, y는 위가 0/아래로 갈수록 음수), 크기, 기본 색)
            // 피부색(팔/머리)과 옷색(가슴/허리/다리)을 다르게 둬서 인접한 부위끼리 뭉쳐 보이지 않게 함.
            var skinTone = new Color(0.949f, 0.788f, 0.588f);
            var pantsTone = new Color(0.16f, 0.16f, 0.22f);
            AddPart(root, MonsterPaintParts.Region.RightArm, new Vector2(126f, -108f), new Vector2(24f, 60f), skinTone);
            AddPart(root, MonsterPaintParts.Region.LeftArm, new Vector2(24f, -108f), new Vector2(24f, 60f), skinTone);
            AddPart(root, MonsterPaintParts.Region.LeftLeg, new Vector2(57f, -174f), new Vector2(22f, 54f), pantsTone);
            AddPart(root, MonsterPaintParts.Region.RightLeg, new Vector2(93f, -174f), new Vector2(22f, 54f), pantsTone);
            AddPart(root, MonsterPaintParts.Region.Waist, new Vector2(75f, -138f), new Vector2(50f, 30f), pantsTone);
            AddPart(root, MonsterPaintParts.Region.Chest, new Vector2(75f, -92f), new Vector2(62f, 64f), OrbUIColors[OrbColor.Blue]);
            AddPart(root, MonsterPaintParts.Region.Head, new Vector2(75f, -30f), new Vector2(56f, 56f), skinTone, circleSprite);
        }

        private void AddPart(RectTransform root, MonsterPaintParts.Region region, Vector2 centerPos, Vector2 size, Color baseColor, Sprite spriteOverride = null)
        {
            var go = new GameObject(region.ToString(), typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centerPos;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = spriteOverride != null ? spriteOverride : roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = baseColor;

            regionImages[region] = (image, baseColor);
        }

        /// <summary>하단 중앙 스킬 구슬 6개. 보유 구슬 인벤토리는 없음 — 슬롯 자체가 활성/비활성으로 알려줌.</summary>
        private void BuildSkillRow(Transform canvasTransform)
        {
            var root = new GameObject("SkillRow", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasTransform, false);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 28f);

            const float slotSize = 56f;
            const float gap = 18f;
            float totalWidth = SkillOrder.Length * slotSize + (SkillOrder.Length - 1) * gap;
            root.sizeDelta = new Vector2(totalWidth, slotSize);

            for (int i = 0; i < SkillOrder.Length; i++)
            {
                OrbColor color = SkillOrder[i];
                float x = -totalWidth / 2f + slotSize / 2f + i * (slotSize + gap);
                BuildSkillSlot(root, color, new Vector2(x, 0f), slotSize, i + 1);
            }
        }

        private void BuildSkillSlot(RectTransform root, OrbColor color, Vector2 pos, float size, int keyNumber)
        {
            var slot = new GameObject($"Skill_{color}", typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(root, false);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = pos;
            slot.sizeDelta = new Vector2(size, size);

            // 준비완료 은은한 펄스용 글로우(뒤쪽, 슬롯보다 살짝 큼)
            var glowObj = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            var glowRect = (RectTransform)glowObj.transform;
            glowRect.SetParent(slot, false);
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(size * 1.35f, size * 1.35f);
            var glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = circleSprite;
            glowImg.color = new Color(1f, 0.878f, 0.510f, 0f); // accent(#FFE082) — 알파는 Update에서 펄스
            glowImg.raycastTarget = false;
            skillGlowObjects[color] = glowObj;

            // 구슬 본체
            var baseObj = new GameObject("Base", typeof(RectTransform), typeof(Image));
            var baseRect = (RectTransform)baseObj.transform;
            baseRect.SetParent(slot, false);
            baseRect.anchorMin = Vector2.zero; baseRect.anchorMax = Vector2.one;
            baseRect.offsetMin = Vector2.zero; baseRect.offsetMax = Vector2.zero;
            var baseImg = baseObj.GetComponent<Image>();
            baseImg.sprite = circleSprite;
            baseImg.color = OrbUIColors[color];
            skillBaseImages[color] = baseImg;

            // 쿨타임 쐐기 — 숫자 없이 진행률만(시계 방향으로 어두운 부채꼴이 줄어듦)
            var wedgeObj = new GameObject("CooldownWedge", typeof(RectTransform), typeof(Image));
            var wedgeRect = (RectTransform)wedgeObj.transform;
            wedgeRect.SetParent(slot, false);
            wedgeRect.anchorMin = Vector2.zero; wedgeRect.anchorMax = Vector2.one;
            wedgeRect.offsetMin = Vector2.zero; wedgeRect.offsetMax = Vector2.zero;
            var wedgeImg = wedgeObj.GetComponent<Image>();
            wedgeImg.sprite = circleSprite;
            wedgeImg.color = new Color(0f, 0f, 0f, 0.72f);
            wedgeImg.type = Image.Type.Filled;
            wedgeImg.fillMethod = Image.FillMethod.Radial360;
            wedgeImg.fillOrigin = (int)Image.Origin360.Top;
            wedgeImg.fillClockwise = true;
            wedgeImg.fillAmount = 0f;
            wedgeImg.raycastTarget = false;
            skillWedgeImages[color] = wedgeImg;

            // 단축키 힌트(1~6) — 슬롯 바로 아래 작게
            var keyObj = new GameObject("Key", typeof(RectTransform), typeof(Text));
            var keyRect = (RectTransform)keyObj.transform;
            keyRect.SetParent(slot, false);
            keyRect.anchorMin = new Vector2(0.5f, 0f);
            keyRect.anchorMax = new Vector2(0.5f, 0f);
            keyRect.pivot = new Vector2(0.5f, 1f);
            keyRect.anchoredPosition = new Vector2(0f, -4f);
            keyRect.sizeDelta = new Vector2(size, 18f);
            var keyText = keyObj.GetComponent<Text>();
            keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            keyText.text = keyNumber.ToString();
            keyText.fontSize = 14;
            keyText.alignment = TextAnchor.UpperCenter;
            keyText.color = new Color(0.667f, 0.667f, 0.667f);
        }

        // ================= 매 프레임 갱신 =================

        private void UpdateBodyHud()
        {
            if (paintParts == null) return;

            foreach (var kv in regionImages)
            {
                bool drained = paintParts.IsRegionDrained(kv.Key);
                kv.Value.image.color = drained ? DrainedColor : kv.Value.baseColor;
            }
        }

        private void UpdateSkillHud()
        {
            foreach (OrbColor color in SkillOrder)
            {
                bool implemented = ImplementedSkills.Contains(color);
                bool hasOrb = implemented && ColorSystemManager.Instance != null && ColorSystemManager.Instance.GetHeld(color) > 0;
                float cooldownRemaining = implemented && skills != null ? skills.GetCooldownTimer(color) : 0f;
                float cooldownDuration = implemented && skills != null ? skills.GetCooldownDuration(color) : 0f;
                bool onCooldown = cooldownRemaining > 0f && cooldownDuration > 0f;
                bool ready = implemented && hasOrb && !onCooldown;

                skillBaseImages[color].color = (implemented && hasOrb) ? OrbUIColors[color] : LockedTint;

                var wedge = skillWedgeImages[color];
                wedge.fillAmount = onCooldown ? Mathf.Clamp01(cooldownRemaining / cooldownDuration) : 0f;

                // 준비완료 펄스 — sin 기반으로 글로우 알파를 부드럽게 오르내림
                var glowImg = skillGlowObjects[color].GetComponent<Image>();
                if (ready)
                {
                    float pulse = (Mathf.Sin(Time.time * 3.2f) + 1f) * 0.5f; // 0~1
                    var c = glowImg.color;
                    c.a = Mathf.Lerp(0.15f, 0.55f, pulse);
                    glowImg.color = c;
                }
                else
                {
                    var c = glowImg.color;
                    c.a = 0f;
                    glowImg.color = c;
                }
            }
        }
    }
}
