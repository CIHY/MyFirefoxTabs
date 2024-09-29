using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyFirefoxTabs
{
    public static class SpecialAction
    {
        private static readonly Dictionary<string, bool> _async = new Dictionary<string, bool>();

        /// <summary>
        /// If all asynchronous tasks have been completed, is true.
        /// </summary>
        public static bool AllTaskDone
        {
            get
            {
                foreach (var item in _async.Values)
                {
                    if (!item)
                        return false;
                }

                return true;
            }
        }

        public static SortedDictionary<string, SortedSet<string>> TelegraphDownloader(SortedDictionary<string, SortedSet<string>> groupedLinks)
        {
            const string __name = nameof(TelegraphDownloader);
            const string __key = "telegra.ph";

            if (!groupedLinks.ContainsKey(__key))
                return groupedLinks;

            var list = groupedLinks[__key].Distinct();
            _async.Add(__name, false);

            Task.Run(() =>
            {
                WriteLine(__name, "Start.");
                const int threadCount = 3;

                bool[] bucket = new bool[threadCount];
                foreach (var item in list)
                {
                    int id = -1;
                    do
                    {
                        for (int i = 0; i < bucket.Length; i++)
                        {
                            if (!bucket[i])
                            {
                                id = i;
                                break;
                            }
                        }
                    }
                    while (id == -1);
                    bucket[id] = true;

                    // Test code.
                    //Task.Run(() =>
                    //{
                    //    int sleep = new Random().Next(10, 50) * 100;
                    //    Thread.Sleep(sleep);
                    //    WriteLine(__name, $"t{id}={sleep}");
                    //    bucket[id] = false;
                    //});

                    Task.Run(async () =>
                    {
                        var proc = new TelegraphDown(item);
                        await proc.StartProcess().ConfigureAwait(false);
                        WriteLine(__name, proc.TaskName + " ok.");
                        bucket[id] = false;
                    });
                }

                bool completed = false;
                while (!completed)
                {
                    completed = true;

                    foreach (var item in bucket)
                    {
                        if (item)
                            completed = false;
                    }
                }

                // Example for single thread.
                //foreach (var item in list)
                //{
                //    var proc = new TelegraphDown(item);
                //    await proc.StartProcess().ConfigureAwait(false);
                //    WriteLine(__name, proc.TaskName + " ok.");
                //}

                _async[nameof(TelegraphDownloader)] = true;
            });

            groupedLinks.Remove(__key);
            return groupedLinks;
        }

        private static void WriteLine(string action, string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{action}] {message}");
        }
    }
}
