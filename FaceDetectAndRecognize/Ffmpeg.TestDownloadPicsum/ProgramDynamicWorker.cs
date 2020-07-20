using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace Ffmpeg.TestDownloadPicsum
{
    class ProgramDynamicWorker
    {
        static int _maxWorker = 1;
        static int _maxParallel = 2;
        static int _maxQueueLength = 10;

        static ConcurrentQueue<string> _queueUrl = new ConcurrentQueue<string>();
        static ConcurrentQueue<MemoryStream> _queueDownloadedUrl = new ConcurrentQueue<MemoryStream>();
        static ConcurrentQueue<MemoryStream> _queeuResizedImage = new ConcurrentQueue<MemoryStream>();

        static List<MyLoopWorker> _workerInitUrl = new List<MyLoopWorker>();

        static List<MyLoopWorker> _workerDownload = new List<MyLoopWorker>();

        static List<MyLoopWorker> _workerResize = new List<MyLoopWorker>();

        static List<MyLoopWorker> _workerSaveFile = new List<MyLoopWorker>();

        static string _dirTemp;

        static void MainDynamic(string[] args)
        {
            Console.WriteLine("Hello World!");

            _dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(_dirTemp) == false) Directory.CreateDirectory(_dirTemp);

            while (true)
            {
                StartAndAutoMonitoring();

                var readAvailKey = Console.KeyAvailable;

                if (readAvailKey)
                {
                    Console.WriteLine("Type `quit` to exit");

                    var cmd = Console.ReadLine();
                    if (cmd == "quit")
                    {
                        //can do stop all worker here
                        Environment.Exit(0);
                        return;
                    }
                }

                Thread.Sleep(5000);
            }

            Console.WriteLine("Quiting!");

        }

        private static void StartSaveFileWorker()
        {
            _workerSaveFile.Add(new MyLoopWorker("SaveFileWorker", () =>
            {
                List<Task> tasks = new List<Task>();

                for (var i = 0; i < _maxQueueLength; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        if (_queeuResizedImage.TryDequeue(out MemoryStream ms) && ms != null)
                        {
                            var dateNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                            new Bitmap(ms).Save(Path.Combine(_dirTemp, $"{dateNow}_{Guid.NewGuid()}.jpg"));
                            ms.Dispose();
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());

            }, (ex) => { }).Start());
        }

        private static void StartResizeWorker()
        {
            _workerResize.Add(new MyLoopWorker("ResizeWorker", () =>
            {
                ParallelDo(() =>
                {
                    if (_queueDownloadedUrl.TryDequeue(out MemoryStream ms) && ms != null)
                    {
                        //resize
                        var outMs = new MemoryStream();

                        using (var image = SixLabors.ImageSharp.Image.Load(ms))
                        {
                            image.Mutate(x => x.Resize(200, 300));
                            image.SaveAsJpeg(outMs);
                        }

                        _queeuResizedImage.Enqueue(outMs);

                        ms.Dispose();
                    }
                }, _queeuResizedImage.Count);

            }, (ex) => { }).Start());
        }

        private static void StartDownloadWorker()
        {
            _workerDownload.Add(new MyLoopWorker("DownloadWorker", () =>
            {
                ParallelDo(() =>
                {
                    if (_queueUrl.TryDequeue(out string url) && !string.IsNullOrEmpty(url))
                    {
                        using (var httpClient = new HttpClient())
                        {
                            httpClient.BaseAddress = new Uri(url);
                            var ms = new MemoryStream();
                            var stream = httpClient.GetStreamAsync(url).GetAwaiter().GetResult();

                            stream.CopyTo(ms);
                            _queueDownloadedUrl.Enqueue(ms);
                        }
                    }
                }, _queueDownloadedUrl.Count);
            }, (ex) => { }).Start());
        }

        private static void StartUrlWorker()
        {
            _workerInitUrl.Add(new MyLoopWorker("UrlWorker", () =>
            {
                ParallelDo(() =>
                {
                    _queueUrl.Enqueue("https://picsum.photos/600/900");
                }, _queueUrl.Count);
            }, (ex) => { }).Start());
        }

        private static void StartAndAutoMonitoring()
        {
            ReadConfig();

            AddWorkerIfUnderMax(_workerInitUrl, StartUrlWorker);
            AddWorkerIfUnderMax(_workerDownload, StartDownloadWorker);
            AddWorkerIfUnderMax(_workerResize, StartResizeWorker);
            AddWorkerIfUnderMax(_workerSaveFile, StartSaveFileWorker);

            RemoveWorkerIfOverMax(_workerInitUrl);
            RemoveWorkerIfOverMax(_workerDownload);
            RemoveWorkerIfOverMax(_workerResize);
            RemoveWorkerIfOverMax(_workerSaveFile);

            var threadUrl = _workerInitUrl.Count;
            var threadDownload = _workerDownload.Count;
            var threadResise = _workerResize.Count;
            var threeadSaveFile = _workerSaveFile.Count;

            Console.WriteLine($"Worker - Url:{threadUrl} Download:{threadDownload} Resize:{threadResise} SaveFile:{threeadSaveFile}");
            Console.WriteLine($"Queue - Url:{_queueUrl.Count} Download:{_queueDownloadedUrl.Count} Resize:{_queeuResizedImage.Count}");

        }

        static void RemoveWorkerIfOverMax(List<MyLoopWorker> workers)
        {
            var threadUrlToKill = workers.Skip(_maxWorker).Take(int.MaxValue).ToList();
            foreach (var t in threadUrlToKill)
            {
                t.Stop();
            }

            var haveToRemove = workers.Where(i => i.IsStoped()).Select(i => i.Id).ToList();

            workers.RemoveAll(i => haveToRemove.Contains(i.Id));
        }

        static void AddWorkerIfUnderMax(List<MyLoopWorker> workers, Action start)
        {
            var haveToInit = _maxWorker - workers.Count;

            for (var i = 0; i < haveToInit; i++)
            {
                start();
            }
        }

        static void ParallelDo(Action a, int queueCount)
        {
            var temp = _maxParallel + 0;

            var needRun = _maxQueueLength - queueCount;

            if (needRun <= 0)
            {
                return;
            }

            for (var i = 0; i < needRun; i = i + temp)
            {
                List<Task> listTask = new List<Task>();

                for (var ik = 0; ik < temp; ik++)
                {
                    var aR = a;
                    var t = Task.Run(() => { aR(); });
                }

                Task.WhenAll(listTask).GetAwaiter().GetResult();
            }
        }

        private static void ReadConfig()
        {
            int.TryParse(ConfigurationManager.AppSettings["MaxWorker"], out _maxWorker);
            if (_maxWorker == 0) _maxWorker = 1;
            int.TryParse(ConfigurationManager.AppSettings["MaxParallelPerWorker"], out _maxParallel);
            if (_maxParallel == 0) _maxParallel = 2;
            int.TryParse(ConfigurationManager.AppSettings["MaxQueueLeghth"], out _maxQueueLength);
            if (_maxQueueLength == 0) _maxQueueLength = 10;

            Console.WriteLine($"Config - MaxWorker: {_maxWorker} MaxParallelPerWorker:{_maxParallel} MaxQueueLeghth:{_maxQueueLength}");
        }

    }


    public class MyLoopWorker : IDisposable
    {
        string _workerName;
        Action _job;
        bool _isStop;
        Thread _thread;
        bool _isStarted;
        public Guid Id { get; private set; }
        public MyLoopWorker(string workerName, Action job, Action<Exception> onError)
        {
            _workerName = workerName;
            Id = Guid.NewGuid();

            _job = job;
            _thread = new Thread(() =>
            {
                while (!_isStop)
                {
                    try
                    {
                        _job();
                    }
                    catch (Exception ex)
                    {
                        onError(ex);
                    }
                    finally
                    {
                        Thread.Sleep(100);
                    }
                }

                Console.WriteLine($"Stoped worker- {_workerName}:{Id}");
            });

            Console.WriteLine($"Init worker - {_workerName}:{Id}");
        }

        public bool IsStoped()
        {
            return _isStop;
        }

        public MyLoopWorker Start()
        {
            if (_isStarted)
            {
                if (_isStop == true) throw new Exception("Disposed can not start again");
                return this;
            }

            _isStop = false;
            _isStarted = true;

            _thread.Start();

            Console.WriteLine($"Started worker: {Id}");
            return this;
        }

        public MyLoopWorker Stop()
        {
            _isStop = true;
            return this;
        }

        public void Dispose()
        {
        }
    }

}
