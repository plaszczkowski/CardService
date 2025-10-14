namespace CardActions.API.BackgroundServices
{
    public class TestResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public int ExpectedStatus { get; set; }
        public int ActualStatus { get; set; }
        public bool Success { get; set; }
        public long DurationMs { get; set; }
        public string? ResponseContent { get; set; }
        public string? Error { get; set; }
    }
}