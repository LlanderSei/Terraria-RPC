using System.Text.RegularExpressions;

namespace TerrariaRPC.Core
{
  public static class PresenceTemplateEngine
  {
    private static readonly Regex ExpressionPattern = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

    public static string Format(string template, TerrariaGameState state)
      => Format(template, state, null);

    public static string Format(string template, TerrariaGameState state, IconManager? iconManager)
    {
      if (string.IsNullOrWhiteSpace(template))
        return "";

      string current = template;

      for (int i = 0; i < 20; i++)
      {
        string next = ExpressionPattern.Replace(current, match =>
        {
          string expression = match.Groups[1].Value;
          return TemplateExpressionEvaluator.Evaluate(expression, state, iconManager);
        });

        if (next == current)
          break;

        current = next;
      }

      return current;
    }
  }
}
