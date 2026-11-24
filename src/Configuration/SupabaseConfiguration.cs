using MomentumGroves.Mobile.Data;

namespace MomentumGroves.Mobile.Configuration;

public sealed record SupabaseConfiguration(string Url, string Key)
{
    internal const string ProjectUrlKey = "data.supabase.url";
    internal const string PublishableKey = "data.supabase.publishable";
    internal const string CloudEnabledKey = "data.supabase.enabled";

    public override string ToString() => Url;

    public static SupabaseConfiguration Parse(string url, string key)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.PathAndQuery != "/" || uri.Fragment.Length != 0)
            throw new DataStoreException("Enter the HTTPS Supabase project URL.");

        key = key.Trim();
        const string prefix = "sb_publishable_";
        if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length == prefix.Length ||
            key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
            throw new DataStoreException("Enter a Supabase publishable key.");

        return new(uri.GetLeftPart(UriPartial.Authority), key);
    }
}
