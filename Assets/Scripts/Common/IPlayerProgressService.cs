namespace RunRich3D.Common
{
    public interface IPlayerProgressService
    {
        int LoadLevel();
        void StartLevel();
        void RestartLevel();
        void SaveUnlockedLevel(int level);
    }
}
