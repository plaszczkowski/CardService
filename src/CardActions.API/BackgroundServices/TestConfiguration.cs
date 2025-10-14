namespace CardActions.API.BackgroundServices
{
    public class TestConfiguration
    {
        public bool Enabled { get; set; } = true;
        public int StartupDelayMs { get; set; } = 3000;
        public int DelayBetweenTestsMs { get; set; } = 500;
        public double CriticalSuccessRate { get; set; } = 80.0;
        public string BaseUrl { get; set; } = "https://localhost:49510";
        public TestCategory[] TestCategories { get; set; } = new[] { TestCategory.Smoke, TestCategory.Functional };
    }

    public enum TestCategory
    {
        Smoke,
        Functional,
        Performance
    }
}
