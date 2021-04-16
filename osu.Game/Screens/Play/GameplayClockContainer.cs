// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Encapsulates gameplay timing logic and provides a <see cref="GameplayClock"/> via DI for gameplay components to use.
    /// </summary>
    public abstract class GameplayClockContainer : Container, IAdjustableClock
    {
        /// <summary>
        /// The final clock which is exposed to gameplay components.
        /// </summary>
        public GameplayClock GameplayClock { get; private set; }

        /// <summary>
        /// Whether gameplay is paused.
        /// </summary>
        public readonly BindableBool IsPaused = new BindableBool();

        /// <summary>
        /// The adjustable source clock used for gameplay. Should be used for seeks and clock control.
        /// </summary>
        protected readonly DecoupleableInterpolatingFramedClock AdjustableSource;

        /// <summary>
        /// Creates a new <see cref="GameplayClockContainer"/>.
        /// </summary>
        /// <param name="sourceClock">The source <see cref="IClock"/> used for timing.</param>
        protected GameplayClockContainer(IClock sourceClock)
        {
            RelativeSizeAxes = Axes.Both;

            AdjustableSource = new DecoupleableInterpolatingFramedClock { IsCoupled = false };
            AdjustableSource.ChangeSource(sourceClock);

            IsPaused.BindValueChanged(OnIsPausedChanged);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.CacheAs(GameplayClock = CreateGameplayClock(AdjustableSource));
            GameplayClock.IsPaused.BindTo(IsPaused);

            return dependencies;
        }

        /// <summary>
        /// Starts gameplay.
        /// </summary>
        public virtual void Start()
        {
            if (!AdjustableSource.IsRunning)
            {
                // Seeking the decoupled clock to its current time ensures that its source clock will be seeked to the same time
                // This accounts for the clock source potentially taking time to enter a completely stopped state
                Seek(GameplayClock.CurrentTime);

                AdjustableSource.Start();
            }

            IsPaused.Value = false;
        }

        /// <summary>
        /// Seek to a specific time in gameplay.
        /// </summary>
        /// <param name="time">The destination time to seek to.</param>
        public virtual void Seek(double time) => AdjustableSource.Seek(time);

        /// <summary>
        /// Stops gameplay.
        /// </summary>
        public virtual void Stop() => IsPaused.Value = true;

        /// <summary>
        /// Restarts gameplay.
        /// </summary>
        public virtual void Restart()
        {
            AdjustableSource.Seek(0);
            AdjustableSource.Stop();

            if (!IsPaused.Value)
                Start();
        }

        protected override void Update()
        {
            if (!IsPaused.Value)
                GameplayClock.UnderlyingClock.ProcessFrame();

            base.Update();
        }

        /// <summary>
        /// Invoked when the value of <see cref="IsPaused"/> is changed to start or stop the <see cref="AdjustableSource"/> clock.
        /// </summary>
        /// <param name="isPaused">Whether the clock should now be paused.</param>
        protected virtual void OnIsPausedChanged(ValueChangedEvent<bool> isPaused)
        {
            if (isPaused.NewValue)
                AdjustableSource.Stop();
            else
                AdjustableSource.Start();
        }

        /// <summary>
        /// Creates the final <see cref="GameplayClock"/> which is exposed via DI to be used by gameplay components.
        /// </summary>
        /// <remarks>
        /// Any intermediate clocks such as platform offsets should be applied here.
        /// </remarks>
        /// <param name="source">The <see cref="IFrameBasedClock"/> providing the source time.</param>
        /// <returns>The final <see cref="GameplayClock"/>.</returns>
        protected abstract GameplayClock CreateGameplayClock(IFrameBasedClock source);

        #region IAdjustableClock

        bool IAdjustableClock.Seek(double position)
        {
            Seek(position);
            return true;
        }

        void IAdjustableClock.Reset()
        {
            Restart();
            Stop();
        }

        public void ResetSpeedAdjustments()
        {
        }

        double IAdjustableClock.Rate
        {
            get => GameplayClock.Rate;
            set => throw new NotSupportedException();
        }

        double IClock.Rate => GameplayClock.Rate;

        public double CurrentTime => GameplayClock.CurrentTime;

        public bool IsRunning => GameplayClock.IsRunning;

        #endregion
    }
}
