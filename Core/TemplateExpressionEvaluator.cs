using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TerrariaRPC.Core
{
  internal static class TemplateExpressionEvaluator
  {
    public static string Evaluate(string expression, TerrariaGameState state)
    {
      if (string.IsNullOrWhiteSpace(expression))
        return "";

      try
      {
        var parser = new Parser(expression, BuildContext(state));
        object? value = parser.ParseExpression();
        return FormatValue(value);
      }
      catch
      {
        return $"{{{{{expression}}}}}";
      }
    }

    private static Dictionary<string, object?> BuildContext(TerrariaGameState state)
    {
      return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
      {
        // Session / screen
        ["IsAttached"] = state.IsAttached,
        ["RawMenuMode"] = state.RawMenuMode,
        ["NetMode"] = state.NetMode,
        ["GameMenu"] = state.GameMenu,
        ["Screen"] = state.Screen.ToString(),
        ["ScreenName"] = state.Screen.ToString(),
        ["IsInGame"] = state.Screen == GameScreen.InGameSinglePlayer || state.Screen == GameScreen.InGameMultiplayer,
        ["IsMainMenu"] = state.Screen == GameScreen.MainMenu,

        // World
        ["WorldName"] = state.WorldName,
        ["Biome"] = state.Biome,
        ["WorldSize"] = state.WorldSize,
        ["WorldEvilType"] = state.WorldEvil,
        ["WorldDifficulty"] = state.WorldRawDifficulty,
        ["WorldDifficultyWithFtwEscalation"] = state.WorldDifficulty,
        ["WorldIsHardmode"] = state.WorldIsHardmode,
        ["WorldSpecialSeeds"] = string.Join(", ", state.WorldSpecialSeeds),
        ["WorldSecretSeeds"] = string.Join(", ", state.WorldSecretSeeds),
        ["WorldSecretSeedsAsNum"] = state.WorldSecretSeedsAsNum,

        // Player
        ["PlayerHp"] = state.PlayerHp,
        ["PlayerMaxHp"] = state.PlayerMaxHp,
        ["PlayerMp"] = state.PlayerMp,
        ["PlayerMaxMp"] = state.PlayerMaxMp,
        ["PlayerAtk"] = state.PlayerAtk,
        ["PlayerHighestWeaponDmg"] = state.PlayerHighestWeaponDmg,
        ["PlayerHighestDps"] = state.PlayerHighestDps,
        ["PlayerDynamicWeaponDmg"] = state.PlayerDynamicWeaponDmg,
        ["PlayerDynamicDps"] = state.PlayerDynamicDps,
        ["PlayerDef"] = state.PlayerDef,
        ["PlayerItemHeld"] = state.PlayerItemHeld,
        ["PlayerHeldItem"] = state.PlayerItemHeld,
        ["PlayerItemPrefix"] = state.PlayerItemPrefix,
        ["PlayerHeldItemPrefix"] = state.PlayerItemPrefix,
        ["PlayerHeldItemWikiName"] = string.IsNullOrEmpty(state.PlayerItemHeld)
          ? ""
          : state.PlayerItemHeld.Replace(" ", "_"),

        // Boss and events
        ["ActiveBoss"] = state.ActiveBossName,
        ["ActiveBossHp"] = state.ActiveBossHp,
        ["ActiveBossMaxHp"] = state.ActiveBossMaxHp,
        ["ActiveBossHasShield"] = state.ActiveBossHasShield,
        ["ActiveBossSp"] = state.ActiveBossSp,
        ["ActiveBossMaxSp"] = state.ActiveBossMaxSp,
        ["ActiveEvent"] = state.ActiveEventName,
        ["ActiveProgressiveEvent"] = state.ActiveProgressiveEventName,
        ["ActiveEventProgress"] = state.ActiveEventProgress >= 0 ? state.ActiveEventProgress : null,
        ["ActiveEventWaveNum"] = state.ActiveEventWaveNum > 0 ? state.ActiveEventWaveNum : null,
        ["ActiveEventHasWaves"] = state.ActiveEventHasWaves,
        ["ActiveEventHasProgress"] = state.ActiveEventHasProgress,
        ["ActiveEventHasProgression"] = state.ActiveEventHasProgression,
        ["ActiveEventIsAtMaxWave"] = state.ActiveEventIsAtMaxWave,
        ["ActiveEventIsAtMaxProgression"] = state.ActiveEventIsAtMaxProgression,
        ["ActiveEventProgression"] = state.ActiveEventProgression,
        ["ActiveEventPoints"] = state.ActiveEventPoints,
        ["ActiveEventIsOoa"] = state.ActiveEventIsOoa,
        ["ActiveEventUsesPoints"] = state.ActiveEventUsesPoints,
        ["ActiveProgressUsesPts"] = state.ActiveProgressUsesPts,
        ["ActiveProgressIsAtMaxWave"] = state.ActiveProgressIsAtMaxWave,
        ["ActiveEventWaveText"] = state.ActiveEventWaveText,
        ["ActiveEventProgressText"] = state.ActiveEventProgressText,
        ["ActiveEventDetailText"] = state.ActiveEventDetailText,
        ["TorchGodActive"] = state.TorchGodActive,
        ["ActiveNonProgressiveEvent"] = state.ActiveNonProgressiveEventName,
        ["ActiveNonProgressiveEventValue"] = state.ActiveNonProgressiveEventValue,
        ["ActiveNonProgressiveEventText"] = state.ActiveNonProgressiveEventText,
        ["ActivePeacefulEvent"] = state.ActivePeacefulEventName,
        ["ActivePeacefulEventValue"] = state.ActivePeacefulEventValue,
        ["ActiveWeather"] = state.ActiveWeatherName,
        ["ActiveBossOrEventText"] = state.HasActiveBoss
          ? state.ActiveBossText
          : state.HasActiveEvent
            ? state.ActiveEventText
            : state.HasActiveNonProgressiveEvent
              ? state.ActiveNonProgressiveEventText
              : state.HasActivePeacefulEvent
                ? $"{state.ActivePeacefulEventName} is occuring."
                : state.HasActiveWeather
                  ? state.ActiveWeatherName
                  : ""
      };
    }

    private static string FormatValue(object? value)
    {
      return value switch
      {
        null => "",
        bool b => b ? "True" : "False",
        string s => s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
      };
    }

    private enum TokenType
    {
      Identifier,
      String,
      Number,
      True,
      False,
      Null,
      In,
      Not,
      Question,
      DoubleQuestion,
      Colon,
      OrOr,
      AndAnd,
      EqualEqual,
      NotEqual,
      Bang,
      Plus,
      Minus,
      OpenParen,
      CloseParen,
      OpenBracket,
      CloseBracket,
      Comma,
      End
    }

    private readonly record struct Token(TokenType Type, string Text, double Number);

    private sealed class Parser
    {
      private readonly List<Token> _tokens;
      private readonly Dictionary<string, object?> _context;
      private int _position;

      public Parser(string expression, Dictionary<string, object?> context)
      {
        _tokens = Tokenize(expression);
        _context = context;
      }

      public object? ParseExpression() => ParseConditional();

      private object? ParseConditional()
      {
        object? condition = ParseOr();

        if (Match(TokenType.Question) || Match(TokenType.DoubleQuestion))
        {
          object? whenTrue = ParseExpression();
          Consume(TokenType.Colon);
          object? whenFalse = ParseExpression();
          return IsTruthy(condition) ? whenTrue : whenFalse;
        }

        return condition;
      }

      private object? ParseOr()
      {
        object? left = ParseAnd();
        while (Match(TokenType.OrOr))
          left = IsTruthy(left) || IsTruthy(ParseAnd());
        return left;
      }

      private object? ParseAnd()
      {
        object? left = ParseEquality();
        while (Match(TokenType.AndAnd))
          left = IsTruthy(left) && IsTruthy(ParseEquality());
        return left;
      }

      private object? ParseEquality()
      {
        object? left = ParseMembership();
        while (true)
        {
          if (Match(TokenType.EqualEqual))
          {
            object? right = ParseMembership();
            left = AreEqual(left, right);
            continue;
          }

          if (Match(TokenType.NotEqual))
          {
            object? right = ParseMembership();
            left = !AreEqual(left, right);
            continue;
          }

          break;
        }

        return left;
      }

      private object? ParseMembership()
      {
        object? left = ParseAdditive();

        while (true)
        {
          bool negate = false;
          bool matched = false;

          if (Match(TokenType.Not))
          {
            if (!Match(TokenType.In))
              throw new InvalidOperationException("Expected 'in' after 'not'.");
            negate = true;
            matched = true;
          }
          else if (Match(TokenType.In))
          {
            matched = true;
          }

          if (!matched)
            break;

          object? right = ParseMembershipTarget();
          bool contains = IsContainedIn(left, right);
          left = negate ? !contains : contains;
        }

        return left;
      }

      private object? ParseMembershipTarget()
      {
        if (Match(TokenType.OpenBracket))
        {
          var items = new List<object?>();
          if (!Match(TokenType.CloseBracket))
          {
            while (true)
            {
              items.Add(ParseExpression());
              if (Match(TokenType.Comma))
                continue;

              Consume(TokenType.CloseBracket);
              break;
            }
          }

          return items;
        }

        return ParseAdditive();
      }

      private object? ParseAdditive()
      {
        object? left = ParseUnary();
        while (true)
        {
          if (Match(TokenType.Plus))
          {
            object? right = ParseUnary();
            if (left is string || right is string)
            {
              left = FormatValue(left) + FormatValue(right);
            }
            else
            {
              left = ToNumber(left) + ToNumber(right);
            }
            continue;
          }

          if (Match(TokenType.Minus))
          {
            object? right = ParseUnary();
            left = ToNumber(left) - ToNumber(right);
            continue;
          }

          break;
        }

        return left;
      }

      private object? ParseUnary()
      {
        if (Match(TokenType.Bang))
          return !IsTruthy(ParseUnary());

        if (Match(TokenType.Minus))
          return -ToNumber(ParseUnary());

        return ParsePrimary();
      }

      private object? ParsePrimary()
      {
        Token token = Peek();
        if (Match(TokenType.String))
          return token.Text;
        if (Match(TokenType.Number))
          return token.Number % 1 == 0 ? (int)token.Number : token.Number;
        if (Match(TokenType.True))
          return true;
        if (Match(TokenType.False))
          return false;
        if (Match(TokenType.Null))
          return null;
        if (Match(TokenType.Identifier))
          return ResolveIdentifier(token.Text);
        if (Match(TokenType.OpenParen))
        {
          object? value = ParseExpression();
          Consume(TokenType.CloseParen);
          return value;
        }

        throw new InvalidOperationException($"Unexpected token: {token.Type}");
      }

      private object? ResolveIdentifier(string name)
      {
        if (_context.TryGetValue(name, out var value))
          return value;
        return "";
      }

      private bool Match(TokenType type)
      {
        if (Peek().Type != type)
          return false;

        _position++;
        return true;
      }

      private void Consume(TokenType type)
      {
        if (!Match(type))
          throw new InvalidOperationException($"Expected token {type}, got {Peek().Type}");
      }

      private Token Peek()
      {
        if (_position >= _tokens.Count)
          return new Token(TokenType.End, "", 0);
        return _tokens[_position];
      }

      private static bool IsTruthy(object? value)
      {
        return value switch
        {
          null => false,
          bool b => b,
          string s => !string.IsNullOrEmpty(s),
          int i => i != 0,
          long l => l != 0,
          float f => Math.Abs(f) > float.Epsilon,
          double d => Math.Abs(d) > double.Epsilon,
          decimal m => m != 0,
          _ => true
        };
      }

      private static double ToNumber(object? value)
      {
        return value switch
        {
          null => 0,
          int i => i,
          long l => l,
          float f => f,
          double d => d,
          decimal m => (double)m,
          bool b => b ? 1 : 0,
          string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) => parsed,
          _ => 0
        };
      }

      private static bool AreEqual(object? left, object? right)
      {
        if (left == null || right == null)
          return left == null && right == null;

        if (left is bool lb || right is bool rb)
          return IsTruthy(left) == IsTruthy(right);

        if (left is string ls || right is string rs)
          return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        double ln = ToNumber(left);
        double rn = ToNumber(right);
        return Math.Abs(ln - rn) < 0.000001;
      }

      private static bool IsContainedIn(object? left, object? right)
      {
        if (right is List<object?> list)
          return list.Any(item => AreEqual(left, item));

        if (right is object?[] array)
          return array.Any(item => AreEqual(left, item));

        if (right is IEnumerable<object?> enumerable && right is not string)
          return enumerable.Any(item => AreEqual(left, item));

        return AreEqual(left, right);
      }

      private static List<Token> Tokenize(string expression)
      {
        var tokens = new List<Token>();
        int i = 0;

        while (i < expression.Length)
        {
          char c = expression[i];
          if (char.IsWhiteSpace(c))
          {
            i++;
            continue;
          }

          if (c == '?' && i + 1 < expression.Length && expression[i + 1] == '?')
          {
            tokens.Add(new Token(TokenType.DoubleQuestion, "??", 0));
            i += 2;
            continue;
          }

          if (c == '=' && i + 1 < expression.Length && expression[i + 1] == '=')
          {
            tokens.Add(new Token(TokenType.EqualEqual, "==", 0));
            i += 2;
            continue;
          }

          if (c == '!' && i + 1 < expression.Length && expression[i + 1] == '=')
          {
            tokens.Add(new Token(TokenType.NotEqual, "!=", 0));
            i += 2;
            continue;
          }

          if (c == '&' && i + 1 < expression.Length && expression[i + 1] == '&')
          {
            tokens.Add(new Token(TokenType.AndAnd, "&&", 0));
            i += 2;
            continue;
          }

          if (c == '|' && i + 1 < expression.Length && expression[i + 1] == '|')
          {
            tokens.Add(new Token(TokenType.OrOr, "||", 0));
            i += 2;
            continue;
          }

          if (c == '?')
          {
            tokens.Add(new Token(TokenType.Question, "?", 0));
            i++;
            continue;
          }

          if (c == ':')
          {
            tokens.Add(new Token(TokenType.Colon, ":", 0));
            i++;
            continue;
          }

          if (c == '!')
          {
            tokens.Add(new Token(TokenType.Bang, "!", 0));
            i++;
            continue;
          }

          if (c == '+')
          {
            tokens.Add(new Token(TokenType.Plus, "+", 0));
            i++;
            continue;
          }

          if (c == '-')
          {
            tokens.Add(new Token(TokenType.Minus, "-", 0));
            i++;
            continue;
          }

          if (c == '(')
          {
            tokens.Add(new Token(TokenType.OpenParen, "(", 0));
            i++;
            continue;
          }

          if (c == ')')
          {
            tokens.Add(new Token(TokenType.CloseParen, ")", 0));
            i++;
            continue;
          }

          if (c == '[')
          {
            tokens.Add(new Token(TokenType.OpenBracket, "[", 0));
            i++;
            continue;
          }

          if (c == ']')
          {
            tokens.Add(new Token(TokenType.CloseBracket, "]", 0));
            i++;
            continue;
          }

          if (c == ',')
          {
            tokens.Add(new Token(TokenType.Comma, ",", 0));
            i++;
            continue;
          }

          if (c == '"')
          {
            var sb = new StringBuilder();
            i++;
            while (i < expression.Length)
            {
              char ch = expression[i];
              if (ch == '\\' && i + 1 < expression.Length)
              {
                sb.Append(expression[i + 1]);
                i += 2;
                continue;
              }
              if (ch == '"')
              {
                i++;
                break;
              }
              sb.Append(ch);
              i++;
            }
            tokens.Add(new Token(TokenType.String, sb.ToString(), 0));
            continue;
          }

          if (char.IsDigit(c))
          {
            int start = i;
            while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
              i++;
            string text = expression[start..i];
            double number = double.Parse(text, CultureInfo.InvariantCulture);
            tokens.Add(new Token(TokenType.Number, text, number));
            continue;
          }

          if (char.IsLetter(c) || c == '_')
          {
            int start = i;
            while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
              i++;
            string ident = expression[start..i];
            TokenType type = ident switch
            {
              "in" => TokenType.In,
              "not" => TokenType.Not,
              "true" => TokenType.True,
              "false" => TokenType.False,
              "null" => TokenType.Null,
              _ => TokenType.Identifier
            };
            tokens.Add(new Token(type, ident, 0));
            continue;
          }

          throw new InvalidOperationException($"Unexpected character '{c}' in expression.");
        }

        return tokens;
      }
    }
  }
}
