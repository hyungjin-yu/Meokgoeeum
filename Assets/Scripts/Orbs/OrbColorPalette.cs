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

        /// <summary>
        /// 검정을 뺀 5색 중 무작위 하나를 돌려줍니다. [[MonsterPaintParts]] 전용 — 먹괴음의
        /// 기본(칠해지기 전) 상태가 이미 어두운 무채색이라, 검정으로 "칠해봤자" 눈에 안 보여서
        /// 맞았는데도 아무 변화가 없는 것처럼 느껴짐(2026-09-16, 사용자 피드백: "색을 부여할 때,
        /// 검은색은 없애야지"). 인덱스로 하드코딩하지 않고 <see cref="OrbColor.Black"/>의 실제
        /// 위치를 건너뛰는 방식이라 나중에 팔레트 순서가 바뀌어도 안전합니다.
        /// </summary>
        public static Color GetRandomVividColor()
        {
            int blackIndex = (int)OrbColor.Black;
            int index = Random.Range(0, Colors.Length - 1);
            if (index >= blackIndex) index++; // 검정 인덱스를 건너뜀
            return Colors[index];
        }
    }
}
