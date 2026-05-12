using System.Text;
using System.Text.Json;

namespace ExcelMcp.Deployment.Installation;

public static class SetupResumeCodec
{
    public static string Encode(SetupResumeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static SetupResumeState Decode(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException("Encoded resume state is required.");
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var state = JsonSerializer.Deserialize<SetupResumeState>(json);
        return state ?? throw new InvalidOperationException("Encoded resume state is invalid.");
    }
}
