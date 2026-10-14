namespace StargateAPI.Controllers
{
    /// <summary>Endpoint metadata: the request is not an operation, so no RequestOutcome row is written.</summary>
    public sealed class SkipRequestOutcomeLog
    {
        public static readonly SkipRequestOutcomeLog Instance = new();

        private SkipRequestOutcomeLog() { }
    }
}
