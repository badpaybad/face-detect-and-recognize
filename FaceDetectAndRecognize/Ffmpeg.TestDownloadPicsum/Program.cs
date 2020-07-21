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


        static int w = 200;
        static int h = 300;
       
        private static void DoMaxParallelTask(ConcurrentQueue<string> queue, int maxParallel)
        {
            var dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(dirTemp) == false) Directory.CreateDirectory(dirTemp);
            Console.WriteLine($"Will save to dir: {dirTemp}");

            List<Task> allTask = new List<Task>();
            var allSw = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(maxParallel);
            List<long> timedownloads = new List<long>();
            List<long> timeSaveFile = new List<long>();
            while (queue.TryDequeue(out string url) && !string.IsNullOrEmpty(url))
            {
                semaphore.Wait();
                var td = Task.Run(async () =>
                  {
                      var ms = new MemoryStream();
                      try
                      {
                          var swd = Stopwatch.StartNew();
                          HttpClient httpClient = new HttpClient();
                          httpClient.BaseAddress = new Uri(url);
                          var stream = await httpClient.GetStreamAsync(url);
                          stream.CopyTo(ms);
                          swd.Stop();
                          timedownloads.Add(swd.ElapsedMilliseconds);
                      }
                      catch
                      {
                          queue.Enqueue(url);
                      }
                      finally
                      {
                          semaphore.Release();
                      }
                      var t = Task.Run(async () =>
                        {
                            var sw = Stopwatch.StartNew();
                            string filename = Path.Combine(dirTemp, $"{Guid.NewGuid()}.jpg");
                            using (var image = SixLabors.ImageSharp.Image.Load(ms.ToArray()))
                            {
                                image.Mutate(x => x.Resize(w, h));
                                await image.SaveAsync(filename);
                            }
                            sw.Stop();
                            timeSaveFile.Add(sw.ElapsedMilliseconds);
                        });
                      allTask.Add(t);
                  });
                allTask.Add(td);
            }
            Task.WaitAll(allTask.ToArray());
            allSw.Stop();
            Console.WriteLine($"All include while loop in {allSw.ElapsedMilliseconds} total download time {timedownloads.Sum()} time save file {timeSaveFile.Sum()}");

        }
        public static void Main()
        {
            int.TryParse(ConfigurationManager.AppSettings["batch_download"], out int batchDownload);

            ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

            for (var i= 0; i < 1000;i++)
            {
                queue.Enqueue("https://picsum.photos/600/900");
            }

            DoMaxParallelTask(queue, batchDownload);

            Console.ReadLine();


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
        public enum Type
        {
            Thread,
            Async
        }
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

        Type _type;
        int _maxDoneJob;
        int _currentDoneJob;


        public WorkerKeepMaxRunning(string name, int maxDoneJob, Action<WorkerKeepMaxRunning> a, int maxParallel = 2, int sleep = 100
            , Action<WorkerKeepMaxRunning> onRelease = null
            , Type type = Type.Thread)
        {
            _maxDoneJob = maxDoneJob;
            _name = name;
            _a = a;
            _max = maxParallel;
            _sleep = sleep;
            //_semaphoreObject = new SemaphoreSlim(1, _max);
            _onRelease = onRelease;
            _type = type;

            if (_type == Type.Thread) _thread = new Thread(async () => { await Loop(); });
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
        async Task Loop()
        {
            while (!_isStop)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_currentDoneJob >= _maxDoneJob) break;

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

                                  lock (_lock)
                                  {
                                      _currentDoneJob++;
                                      _current--;
                                  }
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
                    //Thread.Sleep(_sleep);
                    await Task.Delay(_sleep);
                }
            }

            await Task.WhenAll(_tasks);

            _isStop = true;

            OnStoped?.Invoke(this);

            Console.WriteLine($"{_name} stoped");
        }

        public void Start()
        {
            if (_type == Type.Thread)
            {
                _thread.Start();
            }
            else
            {
                Task.Run(async () => { await Loop(); });
            }

            Console.WriteLine($"{_name} started");
            //await Loop();
        }

        public void Stop()
        {
            _isStop = true;

        }
    }
}
