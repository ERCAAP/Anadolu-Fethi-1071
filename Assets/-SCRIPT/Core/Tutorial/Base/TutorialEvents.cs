using System;

namespace AnadoluFethi.Core.Tutorial
{
    public static class TutorialEvents
    {
        public static event Action<string> OnTutorialStarted;
        public static event Action<string> OnTutorialCompleted;
        public static event Action<string> OnTutorialSkipped;
        public static event Action<TutorialStepData, int, int> OnStepStarted;
        public static event Action<TutorialStepData, int, int> OnStepCompleted;
        public static event Action<float> OnProgressChanged;

        internal static void TutorialStarted(string tutorialId) => OnTutorialStarted?.Invoke(tutorialId);
        internal static void TutorialCompleted(string tutorialId) => OnTutorialCompleted?.Invoke(tutorialId);
        internal static void TutorialSkipped(string tutorialId) => OnTutorialSkipped?.Invoke(tutorialId);
        internal static void StepStarted(TutorialStepData step, int current, int total) => OnStepStarted?.Invoke(step, current, total);
        internal static void StepCompleted(TutorialStepData step, int current, int total) => OnStepCompleted?.Invoke(step, current, total);
        internal static void ProgressChanged(float progress) => OnProgressChanged?.Invoke(progress);
    }
}
