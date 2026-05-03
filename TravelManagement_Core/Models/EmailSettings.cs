namespace TravelManagement.Core.Models
{
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ProcessedFolder { get; set; } = string.Empty;
        public int PollIntervalSeconds { get; set; }
        public string SubjectKeyword { get; set; } = string.Empty;
    }
}