using NUnit.Framework;
using UnityEngine;

namespace Meokgoeeum.Tests
{
    /// <summary>BossHealth 회귀 테스트. [[먹괴음 리팩토링 계획]] 6단계 — 특히 2페이즈 전환은
    /// "정확히 한 번만" 발동해야 하는 조건부 로직이라 회귀 위험이 있습니다.</summary>
    public class BossHealthTests
    {
        private GameObject go;
        private BossHealth health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestBossHealth");
            health = go.AddComponent<BossHealth>();
            TestUtil.InvokeAwake(health); // EditMode에서는 Awake가 즉시 안 불려서 직접 호출 (TestUtil 주석 참고)
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void TakeDamage_ReducesCurrentHPByAmount()
        {
            float before = health.CurrentHP;

            health.TakeDamage(50f);

            Assert.AreEqual(before - 50f, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_CrossingPhase2Threshold_TriggersOnPhase2ExactlyOnce()
        {
            int phase2Count = 0;
            health.OnPhase2 += () => phase2Count++;

            float thresholdHp = health.maxHP * health.phase2Threshold;

            health.TakeDamage(health.maxHP - thresholdHp + 1f); // 임계값 살짝 아래로
            Assert.AreEqual(1, phase2Count);

            health.TakeDamage(10f); // 이미 2페이즈인 상태에서 계속 때려도 중복 발동 금지
            Assert.AreEqual(1, phase2Count);
        }

        [Test]
        public void TakeDamage_AboveThreshold_DoesNotTriggerPhase2()
        {
            int phase2Count = 0;
            health.OnPhase2 += () => phase2Count++;

            health.TakeDamage(1f); // 임계값 훨씬 위

            Assert.AreEqual(0, phase2Count);
        }

        [Test]
        public void TakeDamage_LethalAmount_TriggersOnDeathExactlyOnce()
        {
            int deathCount = 0;
            health.OnDeath += () => deathCount++;

            health.TakeDamage(health.maxHP + 100f);

            Assert.AreEqual(1, deathCount);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void TakeDamage_AfterDeath_IsIgnored()
        {
            int deathCount = 0;
            health.OnDeath += () => deathCount++;

            health.TakeDamage(health.maxHP + 100f); // 사망
            float hpAfterDeath = health.CurrentHP;
            health.TakeDamage(9999f);

            Assert.AreEqual(1, deathCount);
            Assert.AreEqual(hpAfterDeath, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_WhenInvulnerable_IsIgnored()
        {
            // [[BossPo]]가 2페이즈 진입 위치 교란 중 켜두는 시나리오.
            health.SetInvulnerable(true);

            health.TakeDamage(50f);

            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }

        [Test]
        public void Heal_IncreasesCurrentHP_ClampedAtMaxHP()
        {
            health.TakeDamage(50f);

            health.Heal(9999f);

            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }
    }
}
