// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Gameplay
{
    [HeadlessTest]
    public class TestSceneDrainingHealthProcessor : OsuTestScene
    {
        private HealthProcessor processor;
        private ManualClock clock;

        [Test]
        public void TestInitialHealthStartsAtOne()
        {
            createProcessor(createBeatmap(1000, 2000));

            assertHealthEqualTo(1);
        }

        [Test]
        public void TestHealthNotDrainedBeforeGameplayStart()
        {
            createProcessor(createBeatmap(1000, 2000));

            setTime(100);
            assertHealthEqualTo(1);
            setTime(900);
            assertHealthEqualTo(1);
        }

        [Test]
        public void TestHealthDrainBetweenBreakAndObjects()
        {
            createProcessor(createBeatmap(0, 2000, new BreakPeriod(325, 375)));

            //               275    300    325    350    375    400    425
            // hitobjects            o                           o
            // break                        [-------------]
            // no drain              [---------------------------]

            setTime(285);
            setHealth(1);

            setTime(295);
            assertHealthNotEqualTo(1);

            setTime(305);
            setHealth(1);

            setTime(315);
            assertHealthEqualTo(1);

            setTime(365);
            assertHealthEqualTo(1);

            setTime(395);
            assertHealthEqualTo(1);

            setTime(425);
            assertHealthNotEqualTo(1);
        }

        [Test]
        public void TestHealthDrainDuringMaximalBreak()
        {
            createProcessor(createBeatmap(0, 2000, new BreakPeriod(300, 400)));

            //               275    300    325    350    375    400    425
            // hitobjects            o                           o
            // break                 [---------------------------]
            // no drain              [---------------------------]

            setTime(285);
            setHealth(1);

            setTime(295);
            assertHealthNotEqualTo(1);

            setTime(305);
            setHealth(1);

            setTime(395);
            assertHealthEqualTo(1);

            setTime(425);
            assertHealthNotEqualTo(1);
        }

        [Test]
        public void TestHealthNotDrainedAfterGameplayEnd()
        {
            createProcessor(createBeatmap(1000, 2000));
            setTime(2001); // After the hitobjects
            setHealth(1); // Reset the current health for assertions to take place

            setTime(2100);
            assertHealthEqualTo(1);
            setTime(3000);
            assertHealthEqualTo(1);
        }

        [Test]
        public void TestHealthDrainedDuringGameplay()
        {
            createProcessor(createBeatmap(0, 1000));

            setTime(500);
            assertHealthNotEqualTo(1);
        }

        [Test]
        public void TestHealthGainedAfterRewind()
        {
            createProcessor(createBeatmap(0, 1000));
            setTime(500);

            setTime(0);
            assertHealthEqualTo(1);
        }

        [Test]
        public void TestHealthGainedOnHit()
        {
            Beatmap beatmap = createBeatmap(0, 1000);

            createProcessor(beatmap);
            setTime(10); // Decrease health slightly
            assertHealthNotEqualTo(1);

            AddStep("apply hit result", () => processor.ApplyResult(new JudgementResult(beatmap.HitObjects[0], new Judgement()) { Type = HitResult.Perfect }));
            assertHealthEqualTo(1);
        }

        [Test]
        public void TestHealthRemovedOnRevert()
        {
            var beatmap = createBeatmap(0, 1000);
            JudgementResult result = null;

            createProcessor(beatmap);
            setTime(10); // Decrease health slightly
            AddStep("apply hit result", () => processor.ApplyResult(result = new JudgementResult(beatmap.HitObjects[0], new Judgement()) { Type = HitResult.Perfect }));

            AddStep("revert hit result", () => processor.RevertResult(result));
            assertHealthNotEqualTo(1);
        }

        private Beatmap createBeatmap(double startTime, double endTime, params BreakPeriod[] breaks)
        {
            var beatmap = new Beatmap
            {
                BeatmapInfo = { BaseDifficulty = { DrainRate = 10 } },
            };

            for (double time = startTime; time <= endTime; time += 100)
            {
                beatmap.HitObjects.Add(new JudgeableHitObject { StartTime = time });
            }

            beatmap.Breaks.AddRange(breaks);

            return beatmap;
        }

        private void createProcessor(Beatmap beatmap) => AddStep("create processor", () =>
        {
            Child = processor = new DrainingHealthProcessor(beatmap.HitObjects[0].StartTime).With(d =>
            {
                d.RelativeSizeAxes = Axes.Both;
                d.Clock = new FramedClock(clock = new ManualClock());
            });

            processor.ApplyBeatmap(beatmap);
        });

        private void setTime(double time) => AddStep($"set time = {time}", () => clock.CurrentTime = time);

        private void setHealth(double health) => AddStep($"set health = {health}", () => processor.Health.Value = health);

        private void assertHealthEqualTo(double value)
            => AddAssert($"health = {value}", () => Precision.AlmostEquals(value, processor.Health.Value, 0.0001f));

        private void assertHealthNotEqualTo(double value)
            => AddAssert($"health != {value}", () => !Precision.AlmostEquals(value, processor.Health.Value, 0.0001f));

        private class JudgeableHitObject : HitObject
        {
            public override Judgement CreateJudgement() => new Judgement();
            protected override HitWindows CreateHitWindows() => new HitWindows();
        }
    }
}
