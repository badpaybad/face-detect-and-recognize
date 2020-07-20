using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ffmpeg.TestDownloadPicsum
{
    class Program
    {
        static DateTime _start;
        static string _dirTemp;

        static ConcurrentQueue<string> _queueUrl = new ConcurrentQueue<string>();
        static ConcurrentQueue<byte[]> _queueDownloadedUrl = new ConcurrentQueue<byte[]>();

        static ConcurrentQueue<string> _queueSaved = new ConcurrentQueue<string>();

        static ConcurrentQueue<MemoryStream> _queeuResizedImage = new ConcurrentQueue<MemoryStream>();

        static List<long> _timeDownloads = new List<long>();
        static List<long> _timeResize = new List<long>();
        static List<long> _timeSaveFile = new List<long>();

        static int _totalItem = 1000;

        static int _batchDownload = 100;

        static int _batchResize = 12;

        static int _batchSaveFile = 12;

        static object _lock = new object();

        public static void Main()
        {
            var totalItem = _totalItem;

            _dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(_dirTemp) == false) Directory.CreateDirectory(_dirTemp);

            List<string> urls = new List<string>();

            for (var i = 0; i < totalItem; i++)
            {
                urls.Add("https://picsum.photos/600/900");

                _queueUrl.Enqueue("https://picsum.photos/600/900");
            }

            Console.WriteLine($"Init total items: {_totalItem}");
            _start = DateTime.Now;

            WorkerKeepMaxRunning downloadRunner = new WorkerKeepMaxRunning(
                "UrlDownloader",
               (ctx) =>
               {
                   if (!_queueUrl.TryDequeue(out string url) || string.IsNullOrEmpty(url))
                   {
                       return;
                   }

                   Task.Run(async () =>
                   {
                       var sw = Stopwatch.StartNew();
                       using (HttpClient httpClient = new HttpClient())
                       {
                           httpClient.BaseAddress = new Uri(url);
                           using (var ms = new MemoryStream())
                           {
                               using (var stream = await httpClient.GetStreamAsync(url))
                               {
                                   stream.CopyTo(ms);
                                   _queueDownloadedUrl.Enqueue(ms.ToArray());
                                   //var base64 = Convert.ToBase64String(ms.ToArray());
                               }
                           }
                       }
                       sw.Stop();
                       _timeDownloads.Add(sw.ElapsedMilliseconds);
                       if (_queueUrl.Count == 0)
                       {
                           if (ctx != null) ctx.Stop();
                       }
                   });
               }
                , _batchDownload);

            downloadRunner.OnStoped += (ctx) =>
            {
                if (_queueUrl.Count == 0 && ctx.IsStoped())
                {
                    var dNow = DateTime.Now;
                    Console.WriteLine($"Download all in: {dNow.Subtract(_start).TotalMilliseconds}");
                }
            };

            int w = 200;
            int h = 300;

            WorkerKeepMaxRunning resizeRunner = new WorkerKeepMaxRunning(
                "ImageResizer",
               (ctx) =>
               {
                   if (!_queueDownloadedUrl.TryDequeue(out byte[] ms) || ms == null)
                   {
                       return;
                   }

                   var sw = Stopwatch.StartNew();
                   var outMs = new MemoryStream();
                   using (var image = SixLabors.ImageSharp.Image.Load(ms))
                   {
                       image.Mutate(x => x.Resize(w, h));
                       image.SaveAsJpeg(outMs);
                       _queeuResizedImage.Enqueue(outMs);
                   }

                   sw.Stop();
                   _timeResize.Add(sw.ElapsedMilliseconds);

               }
                , _batchResize);


            WorkerKeepMaxRunning saveFileRunner = new WorkerKeepMaxRunning(
                "FileSaver",
              (ctx) =>
               {
                   if (!_queeuResizedImage.TryDequeue(out MemoryStream ms) || ms == null)
                   {
                       return;
                   }

                   var sw = Stopwatch.StartNew();
                   ///var dateNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                   using (var bmp = new Bitmap(ms))
                   {
                       string filename = Path.Combine(_dirTemp, $"{Guid.NewGuid()}.jpg");
                       bmp.Save(filename);

                       _queueSaved.Enqueue(filename);
                   }

                   sw.Stop();
                   _timeSaveFile.Add(sw.ElapsedMilliseconds);

               }
                , _batchSaveFile);


            downloadRunner.Start();

            resizeRunner.Start();

            saveFileRunner.Start();


            while (true)
            {
                Console.WriteLine($"{_queueSaved.Count} --DownloadWorker:{downloadRunner.CurrentCount()} remain:{_queueUrl.Count} " +
                    $"--ResizeWorker:{resizeRunner.CurrentCount()} remain:{_queueDownloadedUrl.Count} "
                    + $"--SaveFileWorker:{saveFileRunner.CurrentCount()} remain:{_queeuResizedImage.Count}"
                    );

                if (_queueSaved.Count >= totalItem)
                {
                    CaclculateStop();

                    downloadRunner.Stop();
                    resizeRunner.Stop();
                    saveFileRunner.Stop();

                    break;
                }

                if (Console.KeyAvailable)
                {
                    Console.WriteLine("Type `quit` to exit");

                    var cmd = Console.ReadLine();
                    if (cmd == "quit")
                    {
                        downloadRunner.Stop();
                        resizeRunner.Stop();
                        // saveFileRunner.Stop();
                        break;
                    }
                }
                Thread.Sleep(2000);
            }

            Console.WriteLine("Done");
        }



        static void CaclculateStop()
        {
            var dateNow = DateTime.Now;

            var distance = dateNow.Subtract(_start);

            Console.WriteLine("Total download items: " + _totalItem);
            Console.WriteLine("Total download in miliseconds: " + distance.TotalMilliseconds);
            Console.WriteLine("Total download in seconds: " + distance.TotalSeconds);

            Console.WriteLine("Everage download in miliseconds: " + _timeDownloads.Sum() / _totalItem);
            Console.WriteLine("Everage resize in miliseconds: " + _timeResize.Sum() / _totalItem);
            Console.WriteLine("Everage save file in miliseconds: " + _timeSaveFile.Sum() / _totalItem);

        }

        static int _bufferDơnload = 1024 * 4;


        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[_bufferDơnload];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }
    }

    public class WorkerKeepMaxRunning
    {
        int _max = 1;
        readonly Action<WorkerKeepMaxRunning> _a;
        object _lock = new object();
        bool _isStop;
        int _current;
        Thread _thread;
        string _name;
        int _sleep;
        SemaphoreSlim _semaphoreObject;

        List<Task> _tasks = new List<Task>();

        public event Action<WorkerKeepMaxRunning> OnStoped;

        public WorkerKeepMaxRunning(string name, Action<WorkerKeepMaxRunning> a, int max = 2, int sleep = 10)
        {
            _name = name;
            _a = a;
            _max = max;
            _sleep = sleep;
            //_semaphoreObject = new SemaphoreSlim(1, _max);
            _thread = new Thread(() => { Loop(); });
        }

        public int CurrentCount()
        {
            lock (_lock) { return _isStop ? 0 : _current; }
            //return _semaphoreObject == null ? 0 : _semaphoreObject.CurrentCount;
        }

        public bool IsStoped()
        {
            return _isStop;
        }
        void Loop()
        {
            while (!_isStop)
            {
                try
                {

                    if (_current >= _max) continue;

                    lock (_lock) _current++;

                    var t = Task.Run(() =>
                         {
                             _a(this);

                             lock (_lock) _current--;
                         });
                    _tasks.Add(t);
                }
                finally
                {
                    Thread.Sleep(_sleep);
                }
            }

            Task.WhenAll(_tasks).GetAwaiter().GetResult();

            if (OnStoped != null)
            {
                OnStoped(this);
            }

            Console.WriteLine($"{_name} stoped");
        }

        public void Start()
        {
            _thread.Start();

            Console.WriteLine($"{_name} started");
            //await Loop();
        }

        public void Stop()
        {
            _isStop = true;

        }
    }
}
