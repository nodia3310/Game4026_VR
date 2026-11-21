using System;
using System.Collections.Generic;
using Game4026.Artifacts;
using Game4026.Contracts;
using NUnit.Framework;

namespace Game4026.Tests.Artifacts
{
    public class ArtifactRestorationTrackerTests
    {
        [Test]
        public void Constructor_RejectsEmptyPlan()
        {
            Assert.Throws<ArgumentException>(() =>
                new ArtifactRestorationTracker(new Dictionary<string, ArtifactType>()));
        }

        [Test]
        public void Constructor_RejectsNullPlan()
        {
            Assert.Throws<ArgumentNullException>(() => new ArtifactRestorationTracker(null));
        }

        [TestCase(ArtifactType.Unknown)]
        [TestCase((ArtifactType)999)]
        public void Constructor_RejectsUnknownTypes(ArtifactType type)
        {
            Assert.Throws<ArgumentException>(() => new ArtifactRestorationTracker(
                new Dictionary<string, ArtifactType> { ["jar-01"] = type }));
        }

        [TestCase("")]
        [TestCase("  ")]
        public void Constructor_RejectsBlankIds(string id)
        {
            Assert.Throws<ArgumentException>(() => new ArtifactRestorationTracker(
                new Dictionary<string, ArtifactType> { [id] = ArtifactType.CanopicJar }));
        }

        [Test]
        public void WrongType_DoesNotCountOrNotify_AndDoesNotPreventLaterCorrectRestoration()
        {
            var tracker = OneJar();
            var notifications = 0;
            tracker.OnArtifactRestored += _ => notifications++;

            Assert.That(tracker.TryRestore("jar-01", ArtifactType.RosettaStone), Is.False);
            Assert.That(tracker.RestoredCount, Is.Zero);
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(notifications, Is.Zero);
            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [TestCase("unknown")]
        [TestCase("JAR-01")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownOrInvalidId_IsRejected(string id)
        {
            var tracker = OneJar();
            Assert.That(tracker.TryRestore(id, ArtifactType.CanopicJar), Is.False);
            Assert.That(tracker.RestoredCount, Is.Zero);
            Assert.That(tracker.IsComplete, Is.False);
        }

        [Test]
        public void DuplicateId_CountsAndNotifiesOnlyOnce_WithUpdatedStateAndCorrectPayload()
        {
            var tracker = OneJar();
            var notifications = 0;
            ArtifactRestoredEvent received = null;
            tracker.OnArtifactRestored += notification =>
            {
                notifications++;
                received = notification;
                Assert.That(tracker.RestoredCount, Is.EqualTo(1));
                Assert.That(tracker.IsComplete, Is.True);
            };

            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.True);
            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.False);
            Assert.That(tracker.RestoredCount, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(received.ArtifactId, Is.EqualTo("jar-01"));
            Assert.That(received.Type, Is.EqualTo(ArtifactType.CanopicJar));
        }

        [Test]
        public void TwoArtifactsOfSameType_CountSeparately_AndCompleteOnlyWhenBothAreRestored()
        {
            var tracker = new ArtifactRestorationTracker(new Dictionary<string, ArtifactType>
            {
                ["jar-01"] = ArtifactType.CanopicJar,
                ["jar-02"] = ArtifactType.CanopicJar
            });

            Assert.That(tracker.TotalCount, Is.EqualTo(2));
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.True);
            Assert.That(tracker.RestoredCount, Is.EqualTo(1));
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.False);
            Assert.That(tracker.TryRestore("jar-02", ArtifactType.TutankhamunMask), Is.False);
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(tracker.TryRestore("jar-02", ArtifactType.CanopicJar), Is.True);
            Assert.That(tracker.RestoredCount, Is.EqualTo(2));
            Assert.That(tracker.IsComplete, Is.True);
        }

        [Test]
        public void Constructor_CopiesPlan_SoCallerMutationCannotChangeProgressRequirements()
        {
            var plan = new Dictionary<string, ArtifactType> { ["jar-01"] = ArtifactType.CanopicJar };
            var tracker = new ArtifactRestorationTracker(plan);
            plan.Clear();
            plan["mask-01"] = ArtifactType.TutankhamunMask;

            Assert.That(tracker.TotalCount, Is.EqualTo(1));
            Assert.That(tracker.TryRestore("mask-01", ArtifactType.TutankhamunMask), Is.False);
            Assert.That(tracker.TryRestore("jar-01", ArtifactType.CanopicJar), Is.True);
            Assert.That(tracker.IsComplete, Is.True);
        }

        private static ArtifactRestorationTracker OneJar()
        {
            return new ArtifactRestorationTracker(new Dictionary<string, ArtifactType>
            {
                ["jar-01"] = ArtifactType.CanopicJar
            });
        }
    }
}
