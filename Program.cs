using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyFirefoxTabs
{
    public class Program
    {
        /*
        public static void Main(string[] args)
        {
            string currDir = Directory.GetCurrentDirectory();
            currDir = currDir.Substring(0, currDir.LastIndexOf(Path.DirectorySeparatorChar) - 10);

            var target = Path.Combine(currDir, "failed.txt");
            if (!File.Exists(target))
                return;

            string[] lines = File.ReadAllLines(target);
            Task.Run(async () =>
            {
                foreach (string line in lines)
                {
                    await new TelegraphDown(line).StartProcess().ConfigureAwait(false);
                    Console.WriteLine("ok. => " + line);
                }

                Console.WriteLine("fin.");
            });

            while (true) { }
        }
        */

        public static void Main(string[] args)
        {
#if DEBUG
            string currDir = Directory.GetCurrentDirectory();
            currDir = currDir.Substring(0, currDir.LastIndexOf(Path.DirectorySeparatorChar) - 10);
#else
            string currDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();
#endif

            WriteLine("Program start.");

            // Search for the exported file in working directory.
            // - The exported file name will be something like => Firefox tabs 20240101.txt
            var exportedTabs = Directory.GetFiles(currDir, "Firefox tabs*.txt");
            WriteLine(exportedTabs.Length + " file(s) detected.");

            // Group all links.
            // - This 'Dictionary' save: <Top domain, Links>
            var groupedLinks = new SortedDictionary<string, SortedSet<string>>();
            foreach (var item in exportedTabs)
            {
                var lines = File.ReadAllLines(item);
                WriteLine("Reading => " + Path.GetFileName(item));

                foreach (var line in lines.Distinct())
                {
                    if (!HttpHelper.FetchTopDomain(line, out string topDomain))
                        continue;

                    if (groupedLinks.ContainsKey(topDomain))
                    { groupedLinks[topDomain].Add(line); }
                    else
                    { groupedLinks.Add(topDomain, new SortedSet<string> { line }); }
                }
            }

            // Special actions.
            WriteLine("Perform special actions...");
            SpecialAction.TelegraphDownloader(groupedLinks);

            // Export.
            WriteLine("Exporting...");
            StringBuilder output = new StringBuilder();
            foreach (var item in groupedLinks)
            {
                output.AppendLine("================================================== " + item.Key);
                foreach (var page in item.Value.Distinct())
                {
                    output.AppendLine(page);
                }
                output.AppendLine();
            }
            File.WriteAllText(Path.Combine(currDir, $"Firefox grouped tabs {DateTime.Now:yyyyMMdd}.txt"), output.ToString());

            while (!SpecialAction.AllTaskDone) { }
        }

        private static void WriteLine(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
    }
}
