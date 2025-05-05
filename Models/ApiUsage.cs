namespace Prismon.Api.Models;
public class ApiUsage
{
     public Guid Id { get; set; }
        public Guid AppId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public App App { get; set; } = null!;
}