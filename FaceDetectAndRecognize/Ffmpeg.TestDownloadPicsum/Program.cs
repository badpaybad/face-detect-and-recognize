using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
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

        static int _batchDownload = 12;

        static int _batchResize = 12;

        static int _batchSaveFile = 12;

        static object _lock = new object();

        static List<object> test = new List<object>();

        public static void Main()
        {
            int.TryParse(ConfigurationManager.AppSettings["total_item"], out _totalItem);
            int.TryParse(ConfigurationManager.AppSettings["batch_download"], out _batchDownload);
            int.TryParse(ConfigurationManager.AppSettings["batch_resize"], out _batchResize);
            int.TryParse(ConfigurationManager.AppSettings["batch_savefile"], out _batchSaveFile);

            int.TryParse(ConfigurationManager.AppSettings["pic_width"], out int picWidth);

            int.TryParse(ConfigurationManager.AppSettings["pic_height"], out int picHeight);
            if (picHeight == 0) picHeight = 900;
            if (picWidth == 0) picWidth = 600;
            //var xtest = new WorkerKeepMaxRunning("Test", (ctx) =>
            //{
            //    Task.Run(()=> {
            //        lock (_lock) test.Add(DateTime.Now);

            //        Thread.Sleep(2000);
            //    });


            //}, 100, 1, (ctx) =>
            //{
            //    lock (_lock) if (test.Count > 0) test.RemoveAt(0);
            //});
            //xtest.Start();

            //while (true)
            //{
            //    var counter = 0;
            //    lock (_lock) counter = test.Count;
            //    Console.WriteLine($"{counter} / {xtest.CurrentCount()}");

            //    Thread.Sleep(1000);
            //}

            //return;

            var totalItem = _totalItem;

            _dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(_dirTemp) == false) Directory.CreateDirectory(_dirTemp);

            List<string> urls = new List<string>();

            for (var i = 0; i < totalItem; i++)
            {
                string item = $"https://picsum.photos/{picWidth}/{picHeight}";
                urls.Add(item);

                _queueUrl.Enqueue(item);
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
                                   ///return ms.ToArray();
                               }
                           }
                       }
                       sw.Stop();
                       _timeDownloads.Add(sw.ElapsedMilliseconds);
                       if (_queueUrl.Count == 0)
                       {
                           if (ctx != null) ctx.Stop();
                       }
                   })
                   //.ContinueWith((stream) => {
                   //    using (var image = SixLabors.ImageSharp.Image.Load(stream.Result))
                   //    {
                   //        string filename = Path.Combine(_dirTemp, $"{Guid.NewGuid()}.jpg");
                   //        image.Save(filename);
                   //    }
                   //})
                   ;//.GetAwaiter().GetResult();
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

            //2048 × 1536
            int w = 2560;
            int h = 1440;

            WorkerKeepMaxRunning resizeRunner = new WorkerKeepMaxRunning(
                "ImageResizer",
               (ctx) =>
               {
                   if (!_queueDownloadedUrl.TryDequeue(out byte[] ms) || ms == null)
                   {
                       return;
                   }

                   Task.Run(async () =>
                   {
                       var sw = Stopwatch.StartNew();
                       var outMs = new MemoryStream();
                       using (var image = SixLabors.ImageSharp.Image.Load(ms))
                       {
                           image.Mutate(x => x.Resize(w, h));
                           //image.SaveAsJpeg(outMs);
                           //_queeuResizedImage.Enqueue(outMs);
                           string filename = Path.Combine(_dirTemp, $"{Guid.NewGuid()}.jpg");

                           _queueSaved.Enqueue(filename);

                           await image.SaveAsync(filename);
                       }

                       sw.Stop();
                       _timeResize.Add(sw.ElapsedMilliseconds);
                   });


               }
                , _batchResize);


            //WorkerKeepMaxRunning saveFileRunner = new WorkerKeepMaxRunning(
            //    "FileSaver",
            //  (ctx) =>
            //   {
            //       if (!_queeuResizedImage.TryDequeue(out MemoryStream ms) || ms == null)
            //       {
            //           return;
            //       }

            //       var sw = Stopwatch.StartNew();
            //       ///var dateNow = DateTime.Now.ToString("yyyyMMddHHmmss");
            //       using (var bmp = new Bitmap(ms))
            //       {
            //           string filename = Path.Combine(_dirTemp, $"{Guid.NewGuid()}.jpg");
            //           bmp.Save(filename);

            //           _queueSaved.Enqueue(filename);
            //       }

            //       sw.Stop();
            //       _timeSaveFile.Add(sw.ElapsedMilliseconds);

            //   }
            //    , _batchSaveFile);


            downloadRunner.Start();

            resizeRunner.Start();

            //saveFileRunner.Start();


            while (true)
            {
                Console.WriteLine($"{_queueSaved.Count} --DownloadWorker:{downloadRunner.CurrentCount()} remain:{_queueUrl.Count} " +
                    $"--ResizeWorker:{resizeRunner.CurrentCount()} remain:{_queueDownloadedUrl.Count} "
                   // + $"--SaveFileWorker:{saveFileRunner.CurrentCount()} remain:{_queeuResizedImage.Count}" 
                   + $" --ThreadCount:{ThreadPool.ThreadCount}"
                    );

                if (_queueSaved.Count >= totalItem)
                {
                    CaclculateStop();

                    downloadRunner.Stop();
                    resizeRunner.Stop();
                    //   saveFileRunner.Stop();

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
            Console.WriteLine($"Thread count: {ThreadPool.ThreadCount}");

        }

        static int _bufferDownload = 1024 * 4;

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[_bufferDownload];
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
        Action<WorkerKeepMaxRunning> _onRelease;
        public WorkerKeepMaxRunning(string name, Action<WorkerKeepMaxRunning> a, int max = 2, int sleep = 100, Action<WorkerKeepMaxRunning> onRelease = null)
        {
            _name = name;
            _a = a;
            _max = max;
            _sleep = sleep;
            //_semaphoreObject = new SemaphoreSlim(1, _max);
            _onRelease = onRelease;
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
                    lock (_lock)
                    {
                        if (_current >= _max)
                        {
                            continue;
                        }
                        _current++;
                    }

                    var t = Task.Run(() =>
                         {
                             try
                             {
                                 _a(this);

                                 lock (_lock) _current--;
                                 _onRelease?.Invoke(this);
                             }
                             catch (Exception ex)
                             {
                                 Console.WriteLine(ex.Message);
                             }
                         });
                    _tasks.Add(t);
                }
                finally
                {
                    Thread.Sleep(_sleep);
                }
            }

            Task.WhenAll(_tasks).GetAwaiter().GetResult();

            OnStoped?.Invoke(this);

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
