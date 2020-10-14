using System;
using System.Collections.Generic;
using NDesk.Options;
using System.Threading;
using System.IO;
using System.Linq;

namespace ttycputemp
{
    class Program
    {
        /* storage for clock display along the bottom */
        private const int HeightPadding = 7; /* 2 lines above; * 4 lines + cursor line below */
        private const int WidthPadding = 14;
        private const int ClockWidth = 7;
        private readonly TimeSpan OneMinute = new TimeSpan(0, 1, 0);

        private const int MinRows = HeightPadding + 6;
        private const int MinColumns = WidthPadding + 6;
        private static readonly string usage =
        "Usage: {0} [<options>]\n" +
        "\n" +
        " Available options:\n" +
        "  -h -- show this help, then exit\n" +
        "  -v -- show version info, then exit\n" +
        "  -m -- monochrome mode (no ANSI escapes)\n" +
        "  -t -- threshold value, number of cores by default" +
        "  -c cols -- how wide is the screen?\n" +
        "  -r rows -- and how high?\n" +
        "     (these override the default auto-detect)\n" +
        "  -i secs\n" +
        "     Alter the number of seconds in " +
        "the interval between refreshes.\n" +
        "     The default is 4, and the minimum " +
        "is 1, which is silently clamped.\n\n" +
        "  (Note: use ctrl-C to quit)";
        private static int clockpad;
        private static int clocks;
        private static string version = "1.0";

        private static void WriteWithColor(char ch, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(ch);
            Console.ForegroundColor = oldColor;
        }

        private static int intSecs = 4;

        private static String hostname;
        private static LinkedList<Reading> readings = new LinkedList<Reading>();
        private static bool monochrome = false;

        private static (int, int) GetTermSize()
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }

        private static void PrintHeader()
        {
            Console.WriteLine("{0}   {1:N2}, {2:N2}, {3:N2}   {4:HH:mm:ss}       ttycputemp, v{5}\n",
                hostname,
                readings.Select(r => r.MinTemperature).Min(),
                readings.Select(r => r.AvgTemperature).Average(),
                readings.Select(r => r.MaxTemperature).Max(),
                readings.Last.Value.Time,
                version);
        }


        static void Main(string[] args)
        {
            bool errflag = false, versflag = false;
            String basename = System.AppDomain.CurrentDomain.FriendlyName;

            var (cols, rows) = GetTermSize();

            var options = new OptionSet(){
                {"i|interval=", "timing interval in seconds", (int s) => intSecs = s},
                {"m", "monochrome mode", m => monochrome = m != null },
                {"r|rows=", "rows", (int r) => rows = r},
                {"c|cols=", "columns", (int c) => cols = c},
                {"v", "show version", v => versflag = v != null },
                {"h|help", "show help", h => errflag = h != null }
            };

            try
            {
                options.Parse(args);
            }
            catch (OptionException)
            {
                errflag = true;
            }

            /* version info requested, show it: */
            if (versflag)
            {
                Console.Error.WriteLine("{0} version {1}", basename, version);
                return;
            }
            /* error, show usage: */
            if (errflag)
            {
                Console.Error.WriteLine(usage, basename);
                return;
            }

            hostname = System.Environment.MachineName;

            //Console.Clear();

            if (rows < MinRows)
            {
                Console.Error.WriteLine($"Sorry, {basename} requires at least {MinRows} rows to run.");
                return;
            }
            if (cols < MinColumns)
            {
                Console.Error.WriteLine($"Sorry, {basename} requires at least {MinColumns} cols to run.");
                return;
            }

            intSecs = Math.Max(1, intSecs); /* must be positive */
            var height = rows - HeightPadding - 1;
            var width = cols - WidthPadding;
            clocks = Math.Max(width / intSecs, width / ClockWidth);
            clockpad = (width / clocks) - ClockWidth;

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();

            DateTime nextRun = DateTime.Now.AddSeconds(intSecs);

            while (!Console.KeyAvailable || Console.ReadKey().Key != ConsoleKey.Escape)
            {
                MoveCursorHome();
                CycleReadingList(new Reading(), width);
                PrintHeader();
                ShowTemperatures(height);
                Thread.Sleep(Math.Max(0, (int)(nextRun - DateTime.Now).TotalMilliseconds));
                nextRun = nextRun.AddSeconds(intSecs);
            }
        }

        private static int CoreCount()
        {
            var lines = File.ReadAllLines("/proc/cpuinfo");

            return lines.Where(x => x.StartsWith("core id")).GroupBy(x => x).Count();
        }

        private class IndexedValue<T> : IComparable<IndexedValue<T>> where T : IComparable<T>
        {
            public IndexedValue(int index, T value)
            {
                this.Index = index;
                this.Value = value;

            }
            public int Index { get; private set; }
            public T Value { get; private set; }

            public int CompareTo(IndexedValue<T> other)
            {
                return Value.CompareTo(other.Value);
            }
        }

        private static void ShowTemperatures(int maxHeight)
        {
            int maxTemp = readings.Select(reading => reading.MaxTemperature).Max();

            foreach (var reading in readings)
            {
                reading.ComputeHeights(maxTemp, maxHeight);
            }

            for (int height = 0; height <= maxHeight; height++)
            {
                Console.Write("{0,6:F2}   ", maxTemp * (maxHeight - height) / maxHeight);
                var readingNode = readings.First;
                while (readingNode != null)
                {
                    var color = ConsoleColor.Black;
                    char ch = ' ';
                    for (int i = 0; i < readingNode.Value.Heights.Length; i++)
                    {
                        ch = GraphCharacter(readingNode, height, n => n.Value.Heights[i]);
                        if (ch != ' ')
                        {
                            color = (ConsoleColor)(i+1);
                            break;
                        }
                    }
                    WriteWithColor(ch, color);
                    readingNode = readingNode.Next;
                }
                Console.WriteLine();
            }

            var lastTime = readings.First.Value.Time;
        }

        private static void MoveCursorHome()
        {
            Console.SetCursorPosition(0, 0);
        }

        private static void CycleReadingList(Reading newReading, int width)
        {
            while (readings.Count >= width)
            {
                readings.RemoveFirst();
            }
            readings.AddLast(newReading);
        }

        private static decimal Max(decimal x, decimal y, decimal z)
        {
            return Math.Max(x, Math.Max(y, z));
        }
        private static decimal Min(decimal x, decimal y, decimal z)
        {
            return Math.Min(x, Math.Min(y, z));
        }

        private static char GraphCharacter(LinkedListNode<Reading> readingNode, int height, Func<LinkedListNode<Reading>, int> selector)
        {
            var previousNode = readingNode.Previous;
            if (height == selector(readingNode))
            {
                if (IsHorizontal(readingNode, selector))
                {
                    return '─';
                }
                else if (IsRising(readingNode, selector))
                {
                    return '╭';
                }
                else if (IsFalling(readingNode, selector))
                {
                    return '╰';
                }
            }
            else if (height < selector(readingNode) && previousNode != null)
            {
                if (height == selector(previousNode))
                {
                    return '╮';
                }
                else if (height > selector(previousNode))
                {
                    return '│';
                }
            }
            else if (previousNode != null)
            { // height > readingNode.Value.height
                if (height == selector(previousNode))
                {
                    return '╯';
                }
                else if (height < selector(previousNode))
                {
                    return '│';
                }
            }
            return ' ';
        }

        private static bool IsHorizontal(LinkedListNode<Reading> readingNode, Func<LinkedListNode<Reading>, int> selector)
        {
            var previousNode = readingNode.Previous;
            return previousNode == null || selector(readingNode) == selector(previousNode);
        }
        private static bool IsRising(LinkedListNode<Reading> readingNode, Func<LinkedListNode<Reading>, int> selector)
        {
            var previousNode = readingNode.Previous;
            return previousNode != null && selector(readingNode) < selector(previousNode);
        }
        private static bool IsFalling(LinkedListNode<Reading> readingNode, Func<LinkedListNode<Reading>, int> selector)
        {
            var previousNode = readingNode.Previous;
            return previousNode != null && selector(readingNode) > selector(previousNode);
        }
    }
}