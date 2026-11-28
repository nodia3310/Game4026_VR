using System;

namespace Game4026.Enemy
{
    public enum ArtifactRoundStage { Idle, Fighting, ArtifactOut, Restored, Done }

    /// <summary>What the rounds component must do this frame.</summary>
    public enum ArtifactRoundAction { None, StartRound, Complete }

    /// <summary>
    /// Engine-free hall rounds: each round fights until its guardian falls (or the round's enemies are all gone),
    /// then the guardian's artifact is out until it is put back on its display. Restoring it ends the round (the
    /// rest of the round's enemies are banished by the caller); after a short pause the next round starts.
    /// After the last artifact the hall is done.
    /// </summary>
    public sealed class ArtifactRoundsModel
    {
        private readonly int roundCount;
        private readonly float pauseAfterRestore;
        private float restoredAt;

        public int RoundCount => roundCount;
        /// <summary>Current round (0-based); stays on the last round once done.</summary>
        public int Round { get; private set; }
        public ArtifactRoundStage Stage { get; private set; } = ArtifactRoundStage.Idle;
        public bool IsComplete => Stage == ArtifactRoundStage.Done;
        /// <summary>Artifacts back on their displays this run.</summary>
        public int RestoredCount { get; private set; }

        public ArtifactRoundsModel(int roundCount, float pauseAfterRestore)
        {
            this.roundCount = Math.Max(0, roundCount);
            this.pauseAfterRestore = Math.Max(0f, pauseAfterRestore);
        }

        /// <summary>Starts round 0. With no rounds the hall is done at once.</summary>
        public ArtifactRoundAction Begin()
        {
            Round = 0;
            RestoredCount = 0;
            if (roundCount == 0)
            {
                Stage = ArtifactRoundStage.Done;
                return ArtifactRoundAction.Complete;
            }
            Stage = ArtifactRoundStage.Fighting;
            return ArtifactRoundAction.StartRound;
        }

        /// <summary>The round's guardian fell (or every enemy of the round is gone): its artifact comes out.</summary>
        public bool GuardianDown()
        {
            if (Stage != ArtifactRoundStage.Fighting)
                return false;
            Stage = ArtifactRoundStage.ArtifactOut;
            return true;
        }

        /// <summary>The round's artifact is back on its display. True → banish the round's remaining enemies.</summary>
        public bool ArtifactRestored(float now)
        {
            if (Stage != ArtifactRoundStage.ArtifactOut)
                return false;
            Stage = ArtifactRoundStage.Restored;
            RestoredCount++;
            restoredAt = now;
            return true;
        }

        public ArtifactRoundAction Tick(float now)
        {
            if (Stage != ArtifactRoundStage.Restored || now - restoredAt < pauseAfterRestore)
                return ArtifactRoundAction.None;
            if (Round + 1 >= roundCount)
            {
                Stage = ArtifactRoundStage.Done;
                return ArtifactRoundAction.Complete;
            }
            Round++;
            Stage = ArtifactRoundStage.Fighting;
            return ArtifactRoundAction.StartRound;
        }

        /// <summary>Back to idle (restart, or the hall was skipped).</summary>
        public void Stop()
        {
            Stage = ArtifactRoundStage.Idle;
            Round = 0;
            RestoredCount = 0;
        }
    }
}
