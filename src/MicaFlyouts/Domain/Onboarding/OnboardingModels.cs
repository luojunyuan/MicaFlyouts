namespace MicaFlyouts.Domain.Onboarding;

public enum OnboardingStep
{
    Media,
    Volume,
    LockKeys,
}

public sealed record OnboardingSnapshot(
    OnboardingStep CurrentStep,
    bool IsComplete,
    IReadOnlySet<OnboardingStep> CompletedSteps)
{
    public static OnboardingSnapshot Initial { get; } = new(
        OnboardingStep.Media,
        false,
        new HashSet<OnboardingStep>());
}

public static class OnboardingRules
{
    public static OnboardingStep? Next(OnboardingStep step) => step switch
    {
        OnboardingStep.Media => OnboardingStep.Volume,
        OnboardingStep.Volume => OnboardingStep.LockKeys,
        _ => null,
    };

    public static OnboardingStep? Previous(OnboardingStep step) => step switch
    {
        OnboardingStep.Volume => OnboardingStep.Media,
        OnboardingStep.LockKeys => OnboardingStep.Volume,
        _ => null,
    };
}
