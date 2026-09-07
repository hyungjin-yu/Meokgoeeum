using NUnit.Framework;
using UnityEngine;

namespace Meokgoeeum.Tests
{
    /// <summary>PlayerHealth 회귀 테스트. [[먹괴음 리팩토링 계획]] 6단계.</summary>
    public class PlayerHealthTests
    {
        private GameObject go;
        private PlayerHealth health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TestPlayerHealth");
            health = go.AddComponent<PlayerHealth>();
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

            health.TakeDamage(10f);

            Assert.AreEqual(before - 10f, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_ClampsAtZero_DoesNotGoNegative()
        {
            // EnemyHealth와 달리 PlayerHealth.TakeDamage는 Mathf.Max(0f, ...)로 클램프합니다.
            health.TakeDamage(health.maxHP + 500f);

            Assert.AreEqual(0f, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_LethalAmount_TriggersOnDeathExactlyOnce()
        {
            int deathCount = 0;
            health.OnDeath += () => deathCount++;

            health.TakeDamage(health.maxHP);

            Assert.AreEqual(1, deathCount);
        }

        [Test]
        public void TakeDamage_AfterDeath_IsIgnored()
        {
            int deathCount = 0;
            health.OnDeath += () => deathCount++;

            health.TakeDamage(health.maxHP); // 사망
            health.TakeDamage(9999f); // 죽은 뒤 추가 피격 — 무시돼야 함

            Assert.AreEqual(1, deathCount);
        }

        [Test]
        public void TakeDamage_WhenDebugInvincible_IsIgnored()
        {
            health.debugInvincible = true;

            health.TakeDamage(10f);

            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }

        [Test]
        public void TakeDamage_WhenInvulnerable_IsIgnored()
        {
            // [[PlayerDodge]]의 구르기 i-frame이 켜져있는 동안의 시나리오.
            health.SetInvulnerable(true);

            health.TakeDamage(10f);

            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }

        [Test]
        public void ResetHealth_RestoresFullHPAndClearsDeathState()
        {
            health.TakeDamage(health.maxHP); // 사망
            health.ResetHealth();

            Assert.AreEqual(health.maxHP, health.CurrentHP);

            // isDead가 실제로 풀렸는지는 private라 직접 못 읽으니, 행동으로 검증:
            // 사망 상태였다면 아래 TakeDamage가 (죽은 뒤 무시 로직 때문에) 반영되지 않았을 것.
            health.TakeDamage(10f);
            Assert.AreEqual(health.maxHP - 10f, health.CurrentHP);
        }

        [Test]
        public void SetHP_ClampsToZeroAndMaxHPRange()
        {
            health.SetHP(-50f);
            Assert.AreEqual(0f, health.CurrentHP);

            health.SetHP(health.maxHP + 500f);
            Assert.AreEqual(health.maxHP, health.CurrentHP);
        }
    }
}
