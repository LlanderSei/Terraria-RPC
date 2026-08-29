using System;

namespace TerrariaRPC.Core
{
  internal static class ImageUrlGuard
  {
    public static string Normalize(string? url, string fallback = "")
    {
      if (string.IsNullOrWhiteSpace(url))
      {
        return fallback;
      }

      string trimmed = url.Trim();

      if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
      {
        return fallback;
      }

      if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
      {
        return fallback;
      }

      return trimmed;
    }
  }
}
