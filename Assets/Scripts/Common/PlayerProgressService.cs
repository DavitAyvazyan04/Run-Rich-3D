using UnityEngine;
using ButchersGames;

namespace RunRich3D.Common
{
    public sealed class PlayerProgressService : IPlayerProgressService
    {
        private readonly LevelManager levelManager;

        public PlayerProgressService(LevelManager levelManager)
        {
            this.levelManager = levelManager;
        }

        public int LoadLevel()
        {
            return Mathf.Max(1, LevelManager.CurrentLevel);
        }

        public void StartLevel()
        {
            levelManager.StartLevel();
        }

        public void RestartLevel()
        {
            LevelManager.CurrentAttempt++;
            levelManager.RestartLevel();
        }

        public void SaveUnlockedLevel(int level)
        {
            LevelManager.CompleteLevelCount = Mathf.Max(LevelManager.CompleteLevelCount, level - 1);
            levelManager.NextLevel();
            PlayerPrefs.Save();
        }
    }
}
