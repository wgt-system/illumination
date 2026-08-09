namespace Illumination.Domain.Learning;

public sealed class LearningState
{
    internal LearningState(DateTimeOffset dueAt)
        : this(isNew: true, dueAt: dueAt)
    {
    }

    internal LearningState(bool isNew, DateTimeOffset dueAt)
    {
        DueAt = dueAt;
        IsNew = isNew;
    }

    public bool IsNew { get; }

    public DateTimeOffset DueAt { get; private set; }

    public bool IsDueAt(DateTimeOffset instant) => DueAt <= instant;

    internal void MarkImmediatelyDue(DateTimeOffset dueAt)
    {
        DueAt = dueAt;
    }
}
