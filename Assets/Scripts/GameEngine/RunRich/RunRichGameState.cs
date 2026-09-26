namespace RunRich3D.GameEngine
{
    public sealed class RunRichGameState
    {
        public int Level { get; internal set; }
        public int Wealth { get; internal set; }
        public int Coins { get; internal set; }
        public bool IsRunning { get; internal set; }
        public bool IsCompleted { get; internal set; }
        public bool IsFailed { get; internal set; }
        public bool IsTransitioning { get; internal set; }
    }
}
