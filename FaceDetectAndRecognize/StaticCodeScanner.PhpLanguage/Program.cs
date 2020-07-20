using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace StaticCodeScanner.PhpLanguage
{
    class Program
    {
        static string _dir = @"C:\work\kowebapp\app";

        static ConcurrentQueue<string> _queueFilePhp = new System.Collections.Concurrent.ConcurrentQueue<string>();

        static ConcurrentQueue<string> _contents = new ConcurrentQueue<string>();

        static void Main(string[] args)
        {
            string fileLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.log");

            using (var sw = new StreamWriter(fileLog, false))
            {
                sw.WriteLine("class name, function name, file path");
                sw.Flush();
            }

            WorkerFindFilePhp();

            for (var i = 0; i < 12; i++)
            {
                WorkerExtractContent();
            }

            WorkerSaveToFile(fileLog);

            Console.ReadLine();
        }

        private static void WorkerSaveToFile(string fileLog)
        {
            new Thread(
                () =>
                {
                    while (true)
                    {
                        if (_contents.TryDequeue(out string l))
                        {
                            using (var sw = new StreamWriter(fileLog, true))
                            {
                                sw.WriteLine(l);
                                sw.Flush();
                            }
                        }

                        Thread.Sleep(100);
                    }
                }
                ).Start();
        }

        private static void WorkerExtractContent()
        {
            new Thread(() =>
            {
                while (true)
                {
                    if (_queueFilePhp.TryDequeue(out string file))
                    {
                        var content = string.Empty;
                        using (var sr = new StreamReader(file))
                        {
                            content = sr.ReadToEnd();
                        }

                        ExtractConent(content, file);
                    }

                    Thread.Sleep(100);
                }
            }).Start();
        }

        private static void WorkerFindFilePhp()
        {
            new Thread(() =>
            {
                FindFilePhp(_dir);
            }).Start();
        }

        private static void ExtractConent(string content, string filepath)
        {
            var lines = content.Split('\n');

            var clssName = string.Empty;
            foreach (var l in lines)
            {
                if (l.IndexOf(" class", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clssName = l.Trim().Trim('\r').Trim(',');
                    string item = $"{clssName},  , ";
                    _contents.Enqueue(item);
                }

                if (l.IndexOf(" function", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var ll = l.Trim().Trim('\r').Trim(',');
                    string item = $"{clssName}, {ll} ,{filepath}";
                    _contents.Enqueue(item);

                    Console.WriteLine(item);
                }
            }
        }

        private static void FindFilePhp(string dir)
        {
            var subDir = Directory.GetDirectories(dir);

            GetFileThenEnqueue(dir);

            foreach (var sdir in subDir)
            {

                FindFilePhp(sdir);
            }
        }

        private static void GetFileThenEnqueue(string sdir)
        {
            var files = Directory.GetFiles(sdir);
            foreach (var f in files)
            {
                if (f.EndsWith(".php", StringComparison.OrdinalIgnoreCase))
                {
                    _queueFilePhp.Enqueue(f);
                }
            }
        }
    }
}
