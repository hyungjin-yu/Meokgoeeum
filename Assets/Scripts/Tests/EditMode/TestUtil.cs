using System.Reflection;
using UnityEngine;

namespace Meokgoeeum.Tests
{
    /// <summary>
    /// EditMode 테스트 전용 헬퍼. Play 모드와 달리 EditMode `[Test]`에서는 `AddComponent` 직후
    /// `Awake()`가 즉시 실행되지 않습니다(다음 에디터 틱까지 미뤄짐 — 프레임을 넘기지 않는 한
    /// 절대 안 불림). 그래서 private `Awake()`를 리플렉션으로 직접 호출해 동기적으로 초기화합니다.
    /// 실제 게임(Play 모드/빌드)에서는 Unity가 정상적으로 호출하므로 이건 테스트 전용 우회입니다.
    /// </summary>
    internal static class TestUtil
    {
        public static void InvokeAwake(MonoBehaviour target)
        {
            var method = target.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(target, null);
        }
    }
}
