using System.Security.Cryptography;
using System.Text;

namespace JohnnyLike.Engine;

public static class TraceHelper
{
    public static string ComputeTraceHash(List<Domain.Abstractions.TraceEvent> events)
    {
        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            sb.AppendLine($"{evt.Time:F6}|{evt.ActorId?.ToString() ?? ""}|{evt.EventType}");
            foreach (var kvp in evt.Details.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  {kvp.Key}={kvp.Value}");
            }
        }
        
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
