using System;
using RunRich3D.Common;
using UnityEngine;

namespace RunRich3D.GameEngine
{
    public sealed class RunRichGameController
    {
        private readonly IPlayerProgressService progressService;

        public RunRichGameState State { get; }
        public event Action<RunRichGameState> StateChanged;

        public RunRichGameController(IPlayerProgressService progressService)
        {
            this.progressService = progressService;
            State = new RunRichGameState
            {
                Level = progressService.LoadLevel(),
                Wealth = 50
            };
        }

        public bool TryStartRun()
        {
            if (State.IsCompleted || State.IsRunning)
                return false;

            State.IsRunning = true;
            progressService.StartLevel();
            NotifyChanged();
            return true;
        }

        public void AddCoin()
        {
            State.Coins++;
            NotifyChanged();
        }

        public void ChangeWealth(int delta)
        {
            State.Wealth = Mathf.Clamp(State.Wealth + delta, 0, 100);
            NotifyChanged();
        }

        public bool TryFailLevel()
        {
            if (State.IsCompleted)
                return false;

            State.IsCompleted = true;
            State.IsFailed = true;
            State.IsRunning = false;
            NotifyChanged();
            return true;
        }

        public bool TryCompleteLevel()
        {
            if (State.IsCompleted)
                return false;

            State.IsCompleted = true;
            State.IsFailed = false;
            State.IsRunning = false;
            NotifyChanged();
            return true;
        }

        public void SaveCompletedLevel()
        {
            progressService.SaveUnlockedLevel(State.Level + 1);
        }

        public bool TryBeginTransition()
        {
            if (!State.IsCompleted || State.IsTransitioning)
                return false;

            State.IsTransitioning = true;
            NotifyChanged();
            return true;
        }

        public void AdvanceLevel()
        {
            State.Level++;
        }

        public void RestartCurrentLevel()
        {
            progressService.RestartLevel();
        }

        public void ResetLevel()
        {
            State.Wealth = 50;
            State.IsRunning = false;
            State.IsCompleted = false;
            State.IsFailed = false;
            State.IsTransitioning = false;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke(State);
        }
    }
}
