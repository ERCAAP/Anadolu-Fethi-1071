namespace AnadoluFethi.Core.Tutorial
{
    public interface ITutorialCondition
    {
        bool IsMet { get; }
        void StartListening();
        void StopListening();
    }
}
