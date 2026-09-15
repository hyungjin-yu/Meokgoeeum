using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// OrbColorPalette (6색 구슬의 실제 RGB 값)
    /// [[23 UI 디자인 기획서 (Stitch AI용)]]의 색 구슬 팔레트 HEX 그대로. 원래 [[ColorOrbPickup]]
    /// 안에만 있던 private 배열이었는데, 2026-09-16 [[MonsterPaintParts]](몹 부위 색칠 처치 시스템)도
    /// 같은 팔레트가 필요해져서 공용 유틸로 뽑아냈습니다 — 값 자체는 전혀 안 바뀌었습니다.
    /// </summary>
    public static class OrbColorPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.898f, 0.224f, 0.208f), // 빨강 #E53935
            new Color(0.118f, 0.533f, 0.898f), // 파랑 #1E88E5
            new Color(0.992f, 0.847f, 0.208f), // 노랑 #FDD835
            new Color(0.263f, 0.627f, 0.278f), // 초록 #43A047
            new Color(0.557f, 0.141f, 0.667f), // 보라 #8E24AA
            new Color(0.129f, 0.129f, 0.129f), // 검정 #212121
        };

        public static Color GetColor(OrbColor color) => Colors[(int)color];

        /// <summary>6색 중 무작위 하나를 돌려줍니다.</summary>
        public static Color GetRandomColor() => Colors[Random.Range(0, Colors.Length)];
    }
}
