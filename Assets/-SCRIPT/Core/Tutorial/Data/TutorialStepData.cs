using UnityEngine;
using System;

namespace AnadoluFethi.Core.Tutorial
{
    public enum TutorialStepType
    {
        Message,
        Highlight,
        Action,
        Wait
    }

    public enum TutorialPointerDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    public enum TutorialCompletionType
    {
        Manual,
        Auto,
        Condition,
        Timer
    }

    [Serializable]
    public class TutorialStepData
    {
        [Header("Identification")]
        public string stepId;

        [Header("Content")]
        public TutorialStepType stepType = TutorialStepType.Message;
        [TextArea(2, 5)]
        public string title;
        [TextArea(3, 8)]
        public string description;
        public Sprite icon;

        [Header("Targeting")]
        public string targetObjectTag;
        public string targetObjectName;
        public Vector2 customPosition;
        public bool useCustomPosition;

        [Header("Pointer")]
        public TutorialPointerDirection pointerDirection = TutorialPointerDirection.None;
        public Vector2 pointerOffset;

        [Header("Highlight")]
        public bool highlightTarget = true;
        public bool blockRaycasts = true;
        public bool allowTargetInteraction = true;

        [Header("Completion")]
        public TutorialCompletionType completionType = TutorialCompletionType.Manual;
        public float autoCompleteDelay = 2f;
        public string conditionId;

        [Header("Timing")]
        public float delayBefore;
        public float delayAfter;

        [Header("Audio")]
        public AudioClip voiceOver;
        public AudioClip sfx;
    }
}
