using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Meokgoeeum.Tests
{
    /// <summary>
    /// EnemyHealth 회귀 테스트. [[먹괴음 리팩토링 계획]] 6단계 — 특히 EnemyHeup이 매 프레임
    /// Heal()을 호출하는 히스테리시스 회복 경로(2026-08-21에 로그 스팸 문제로 한 번 손댄 적
    /// 있음)처럼 재발 이력이 있거나 여러 몹이 공유하는 로직부터 커버합니다.
    ///
    /// GameState/ColorOrbPool 등 Die()가 참조하는 다른 싱글턴은 전부 null 방어(`?.`)돼 있어서
    /// 씬 없이 컴포넌트만 올려도 안전하게 테스트할 수 있습니다.
    /// </summary>
    public class EnemyHealthTests
    {
        private GameObject go;
        private EnemyHealth health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestEnemyHealth");
            health = go.AddComponent<EnemyHealth>();
            TestUtil.InvokeAwake(health); // EditMode에서는 Awake가 즉시 안 불려서 직접 호출 (TestUtil 주석 참고)
        }

        /// <summary>
        /// Die()의 Destroy(gameObject) 호출은 EditMode에서 "Destroy may not be called from
        /// edit mode!" 에러 로그를 남깁니다(실제로는 파괴 안 됨) — 예상된 로그로 등록해서
        /// 테스트가 이걸 진짜 실패로 오인하지 않게 합니다.
        /// </summary>
        private static void ExpectEditModeDestroyWarning()
        {
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Awake_SetsCurrentHPToMaxHP()
        {
            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_ReducesCurrentHPByAmount()
        {
            float before = health.CurrentHP;

            health.TakeDamage(5f);

            Assert.AreEqual(before - 5f, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_LethalAmount_TriggersOnDeathExactlyOnce()
        {
            int deathCount = 0;
            health.OnDeath += _ => deathCount++;

            ExpectEditModeDestroyWarning();
            health.TakeDamage(health.maxHP + 100f); // 확실한 오버킬

            Assert.AreEqual(1, deathCount);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void TakeDamage_AfterDeath_IsIgnored()
        {
            int deathCount = 0;
            health.OnDeath += _ => deathCount++;

            ExpectEditModeDestroyWarning();
            health.TakeDamage(health.maxHP + 100f); // 1차: 사망
            float hpAfterDeath = health.CurrentHP;
            health.TakeDamage(9999f); // 2차: 죽은 뒤 추가 피격 — 무시돼야 함

            Assert.AreEqual(1, deathCount, "죽은 뒤 추가 피격으로 OnDeath가 중복 발동되면 안 됩니다.");
            Assert.AreEqual(hpAfterDeath, health.CurrentHP, "죽은 뒤엔 추가 피격이 HP에 반영되면 안 됩니다.");
        }

        [Test]
        public void Heal_IncreasesCurrentHP_ClampedAtMaxHP()
        {
            health.TakeDamage(10f);

            health.Heal(3f);
            Assert.AreEqual(health.maxHP - 7f, health.CurrentHP);

            health.Heal(9999f); // 최대치를 훌쩍 넘는 회복 — maxHP에서 멈춰야 함
            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }

        [Test]
        public void Heal_AfterDeath_IsIgnored()
        {
            ExpectEditModeDestroyWarning();
            health.TakeDamage(health.maxHP + 100f); // 사망 (참고: TakeDamage는 0 밑으로 클램프하지 않음 — 음수로 남음)
            float hpAfterDeath = health.CurrentHP;

            health.Heal(50f);

            Assert.AreEqual(hpAfterDeath, health.CurrentHP, "죽은 뒤엔 회복도 무시되어 HP가 그대로여야 합니다 (Die() 시점의 값 유지).");
        }

        [Test]
        public void ConfigureMaxHP_SetsBothMaxAndCurrentHP()
        {
            // EnemyGwang의 Awake, EnemyBun의 분열 미니언 HP 조정이 쓰는 경로.
            health.ConfigureMaxHP(60f);

            Assert.AreEqual(60f, health.maxHP);
            Assert.AreEqual(60f, health.CurrentHP);
        }
    }
}
