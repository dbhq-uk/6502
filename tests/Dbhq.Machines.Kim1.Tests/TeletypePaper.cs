using System.Text;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The paper in the teletype: what the printed characters look like once a
/// carriage return has sent the head back and a line feed has moved the paper
/// on. The same rules as the page's terminal (site/public/kim-1.js): CR goes
/// to the start of the line, LF to a new line, a printable character is
/// printed at the head and moves it on (over whatever was there), and every
/// other character, NUL, RUB OUT and XOFF among them, prints nothing.
/// </summary>
public sealed class TeletypePaper
{
    private readonly List<StringBuilder> _lines = [new()];
    private int _column;

    public void Print(string text)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '\r':
                    _column = 0;
                    break;
                case '\n':
                    _lines.Add(new StringBuilder());
                    break;
                case >= ' ' and <= '~':
                    StringBuilder line = _lines[^1];
                    if (_column < line.Length)
                    {
                        line[_column] = c;
                    }
                    else
                    {
                        line.Append(' ', _column - line.Length).Append(c);
                    }

                    _column++;
                    break;
            }
        }
    }

    /// <summary>Every line, with the spaces at its end taken off.</summary>
    public IReadOnlyList<string> Lines => [.. _lines.Select(l => l.ToString().TrimEnd())];

    /// <summary>The last <paramref name="count"/> lines.</summary>
    public IReadOnlyList<string> Last(int count) => [.. Lines.TakeLast(count)];

    public override string ToString() => string.Join("\n", Lines);
}
