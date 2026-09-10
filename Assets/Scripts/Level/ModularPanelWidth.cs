using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meokgoeeum
{
    /// <summary>
    /// ModularPanelWidth (건물 모듈 폭 조절기)
    /// [[07 UI & 아트 방향]] "구조 제작 계획"의 벽체 패널/창문 모듈(Wall_Cell_01,
    /// Window_Cell_01 등)을 건물 특성에 맞춰 늘리거나 줄일 때, 프레임 두께까지 같이
    /// 늘어나 비율이 깨지는 문제를 막습니다.
    ///
    /// 자식 파츠를 세 종류로 나눠서 처리합니다.
    /// - Stretch: 폭 전체를 채우는 파츠(유리, 문살, 벽면 등). 늘어난 만큼 그대로 커집니다.
    /// - Reposition: 좌우 끝에 고정 두께로 서 있는 파츠(세로 프레임 등). 두께(스케일)는
    ///   그대로 두고 바깥쪽으로 밀려날 뿐입니다.
    /// - Fixed: 중앙 문살처럼 폭과 무관하게 그대로 두는 파츠.
    ///
    /// 각 파츠의 "기준 배치"는 처음 한 번만 캡처해서 PartRule에 저장해둡니다(직렬화되므로
    /// 에디터를 껐다 켜도 유지됨). width가 바뀔 때마다 기준값 대비 차이(delta)만 더하고
    /// 빼서 계산합니다 - 비율로 곱하지 않는 이유는 프레임 두께처럼 "폭이 바뀌어도 그대로여야
    /// 하는 값"까지 같이 늘어나 버리는 걸 막기 위함입니다.
    /// </summary>
    [ExecuteAlways]
    public class ModularPanelWidth : MonoBehaviour
    {
        public enum PartRole { Stretch, Reposition, Fixed }

        [Serializable]
        public class PartRule
        {
            public Transform part;
            public PartRole role = PartRole.Fixed;

            [HideInInspector] public bool captured;
            [HideInInspector] public float baseScaleX;
            [HideInInspector] public float basePositionX;
        }

        [Tooltip("이 모듈이 Blender에서 만들어진 기준 폭입니다. export한 실제 치수와 맞춰주세요.")]
        public float baseWidth = 2.0f;

        [Tooltip("지금 원하는 폭입니다. 바꾸면 에디터에서 바로 반영됩니다.")]
        public float width = 2.0f;

        public List<PartRule> rules = new List<PartRule>();

        private void OnEnable()
        {
            CaptureMissing();
            Apply();
        }

        private void OnValidate()
        {
            CaptureMissing();
            Apply();
        }

        /// <summary>아직 기준값이 없는 파츠만 지금 배치를 기준으로 캡처합니다.</summary>
        private void CaptureMissing()
        {
            foreach (var rule in rules)
            {
                if (rule.part == null || rule.captured) continue;
                rule.baseScaleX = rule.part.localScale.x;
                rule.basePositionX = rule.part.localPosition.x;
                rule.captured = true;
            }
        }

        /// <summary>
        /// 파츠 위치를 손으로 옮긴 뒤 그 배치를 새 기준값으로 다시 잡고 싶을 때 사용합니다.
        /// </summary>
        [ContextMenu("기준값 다시 잡기")]
        public void RecaptureBase()
        {
            foreach (var rule in rules) rule.captured = false;
            baseWidth = width;
            CaptureMissing();
        }

        /// <summary>지금 width 값대로 자식 파츠들을 다시 배치합니다.</summary>
        public void Apply()
        {
            CaptureMissing(); // rules를 코드로 나중에 추가한 경우에도 기준값이 비어있지 않도록 보장

            float delta = width - baseWidth;

            foreach (var rule in rules)
            {
                if (rule.part == null || !rule.captured) continue;

                switch (rule.role)
                {
                    case PartRole.Stretch:
                        var scale = rule.part.localScale;
                        scale.x = rule.baseScaleX + delta;
                        rule.part.localScale = scale;
                        break;

                    case PartRole.Reposition:
                        // basePositionX가 0인 파츠에 Reposition을 쓰면 어느 쪽으로 밀지
                        // 판단할 수 없으므로(부호가 없음) 그런 파츠는 Fixed로 등록할 것.
                        var pos = rule.part.localPosition;
                        float sign = rule.basePositionX >= 0f ? 1f : -1f;
                        pos.x = rule.basePositionX + sign * delta * 0.5f;
                        rule.part.localPosition = pos;
                        break;

                    case PartRole.Fixed:
                        break;
                }
            }
        }
    }
}
