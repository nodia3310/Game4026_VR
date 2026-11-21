using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Hit-feel rules: kind precedence, damage scaling, the shake curve and the bolt's reflect freeze.</summary>
    public sealed class HitFeedbackTests
    {
        private HitFeedbackProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<HitFeedbackProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void KindOf_KillThenWeakPointThenStagger()
        {
            Assert.AreEqual(HitKind.Kill, HitFeedbackProfile.KindOf(new EnemyDamageResult(5f, Vector3.zero, true, true, true)));
            Assert.AreEqual(HitKind.WeakPoint, HitFeedbackProfile.KindOf(new EnemyDamageResult(5f, Vector3.zero, true, true, false)));
            Assert.AreEqual(HitKind.Stagger, HitFeedbackProfile.KindOf(new EnemyDamageResult(5f, Vector3.zero, false, true, false)));
            Assert.AreEqual(HitKind.Normal, HitFeedbackProfile.KindOf(new EnemyDamageResult(5f, Vector3.zero, false, false, false)));
        }

        [Test]
        public void HitStop_GrowsWithDamage_IsCapped_AndParryIgnoresDamage()
        {
            float light = profile.HitStopFor(HitKind.Normal, 5f);
            float heavy = profile.HitStopFor(HitKind.Normal, 40f);
            Assert.Greater(heavy, light);
            Assert.LessOrEqual(profile.HitStopFor(HitKind.Kill, 10000f), 0.2f + 1e-5f);
            Assert.AreEqual(profile.HitStopFor(HitKind.Parry, 0f), profile.HitStopFor(HitKind.Parry, 100f), 1e-6f);
            Assert.Greater(profile.HitStopFor(HitKind.Parry, 0f), profile.HitStopFor(HitKind.Blocked, 0f));
        }

        [Test]
        public void Shake_ScalesWithDamage_WithinLimits()
        {
            float baseShake = profile.For(HitKind.Normal).shake;
            Assert.AreEqual(baseShake * 0.6f, profile.ShakeFor(HitKind.Normal, 0.1f), 1e-6f);
            Assert.AreEqual(baseShake * 1.6f, profile.ShakeFor(HitKind.Normal, 999f), 1e-6f);
        }

        [Test]
        public void ParrySound_OnlyOnSuccessfulDeflects_NeverOnAMissedParry()
        {
            var clip = AudioClip.Create("parry", 10, 1, 44100, false);
            try
            {
                var so = new UnityEditor.SerializedObject(profile);
                SerializedPropertyHelper.SetSingle(so.FindProperty("parryClips"), clip);
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(clip, profile.PickClip(HitKind.Parry, 0.5f));
                Assert.AreEqual(clip, profile.PickClip(HitKind.Reflect, 0.5f));
                Assert.IsNull(profile.PickClip(HitKind.Blocked, 0.5f), "Blocked clips empty: silent, not the parry sound.");
            }
            finally { Object.DestroyImmediate(clip); }
        }

        private static class SerializedPropertyHelper
        {
            public static void SetSingle(UnityEditor.SerializedProperty array, Object value)
            {
                array.arraySize = 1;
                array.GetArrayElementAtIndex(0).objectReferenceValue = value;
            }
        }

        [Test]
        public void ShakeOffset_StartsAwayFromHitter_AndDecaysToZero()
        {
            Vector3 start = EnemyHitFeedback.ShakeOffset(0f, 0.1f, 0.02f, 50f, Vector3.forward, Vector3.right);
            Assert.AreEqual(0.02f, start.z, 1e-6f, "The first frame dents away from the hit.");
            Assert.AreEqual(Vector3.zero, EnemyHitFeedback.ShakeOffset(0.1f, 0.1f, 0.02f, 50f, Vector3.forward, Vector3.right));
            Vector3 late = EnemyHitFeedback.ShakeOffset(0.09f, 0.1f, 0.02f, 50f, Vector3.forward, Vector3.right);
            Assert.Less(late.magnitude, 0.005f);
        }

        [Test]
        public void ReflectedBolt_FreezesBriefly_ThenFliesBack()
        {
            var go = new GameObject("Bolt");
            try
            {
                var bolt = go.AddComponent<EnemyProjectile>();
                bolt.Launch(null, Vector3.zero, Vector3.forward, 8f, 10f);
                int before = HitImpacts.Count;
                Assert.IsTrue(bolt.Reflect());
                Assert.AreEqual(before + 1, HitImpacts.Count);
                Assert.AreEqual(HitKind.Reflect, HitImpacts.Last.Kind);

                float stop = HitImpacts.Last.HitStop;
                Assert.Greater(stop, 0f);
                bolt.Step(stop * 0.5f);
                Assert.AreEqual(0f, bolt.transform.position.z, 1e-5f, "Held during the hit stop.");
                bolt.Step(stop * 0.5f + 0.1f);
                Assert.AreEqual(-0.1f * bolt.Speed, bolt.transform.position.z, 1e-3f, "Then flies back.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
