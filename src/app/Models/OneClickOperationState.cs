namespace WinOptimizationApp.Models;

public enum OneClickPhase { Idle, Analyzing, AwaitingConfirmation, Running, Cancelled, Completed, Failed }

/// <summary>Keeps late progress callbacks from reviving a finished or newer operation.</summary>
public sealed class OneClickOperationState
{
    public OneClickPhase Phase { get; private set; }
    public long Generation { get; private set; }
    public bool HasStartedExecution { get; private set; }
    public bool IsBusy => Phase is OneClickPhase.Analyzing or OneClickPhase.AwaitingConfirmation or OneClickPhase.Running;
    public bool ShowProgress => Phase is OneClickPhase.Analyzing or OneClickPhase.Running;

    public long Begin()
    {
        if (IsBusy) throw new InvalidOperationException("An operation is already active.");
        HasStartedExecution = false;
        Phase = OneClickPhase.Analyzing;
        return ++Generation;
    }

    public bool AcceptsProgress(long generation, bool running) => generation == Generation &&
        Phase == (running ? OneClickPhase.Running : OneClickPhase.Analyzing);

    public void AwaitConfirmation()
    {
        if (Phase != OneClickPhase.Analyzing) throw new InvalidOperationException("Analysis is not active.");
        Phase = OneClickPhase.AwaitingConfirmation;
    }

    public void StartRunning()
    {
        if (Phase != OneClickPhase.AwaitingConfirmation) throw new InvalidOperationException("Confirmation is required.");
        HasStartedExecution = true;
        Phase = OneClickPhase.Running;
    }

    public void Finish(OneClickPhase phase)
    {
        if (phase is not (OneClickPhase.Cancelled or OneClickPhase.Completed or OneClickPhase.Failed))
            throw new ArgumentOutOfRangeException(nameof(phase));
        Phase = phase;
    }
}
