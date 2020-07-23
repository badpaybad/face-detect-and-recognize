using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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
    public class MyActionBlock<T>
    {
        private readonly Func<T, Task> _func;
        private readonly SemaphoreSlim _semaphore;
        private readonly List<Task> _outstandingTasks;
        private long _completed;

        public MyActionBlock(Func<T, Task> func, int maxDegreeOfParallelism = 1)
        {
            _func = func;
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
            _outstandingTasks = new List<Task>();
            _completed = 0;
        }

        public async Task<bool> EnqueueAsync(T item)
        {
            if (Interlocked.Read(ref _completed) == 1)
            {
                return false;
            }

            await _semaphore.WaitAsync();

            _outstandingTasks.Add(
            Task.Run(
            async () =>
            {
                try
                {
                    await _func(item);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            )
            );

            return true;
        }

        public void Complete()
        {
            Interlocked.CompareExchange(ref _completed, 1, 0);
        }

        public Task Completion
        {
            get
            {
                var running = _outstandingTasks.Where(x => x.Status != TaskStatus.RanToCompletion)
                .Where(x => x.Status != TaskStatus.Faulted)
                .Where(x => x.Status != TaskStatus.Canceled)
                .ToList();

                return Task.WhenAll(running);
            }
        }
    }

    public class SlimActionBlock<T>
    {
        public class Options
        {
            public int MaxParallel = 2;
        }
        SemaphoreSlim _semaphore;
        Action<T> _doJob;
        bool _isStop;
        Options _option;
        ConcurrentQueue<T> _data = new ConcurrentQueue<T>();
        Task _task;
        List<Task> _subTask = new List<Task>();
        public SlimActionBlock(Action<T> doJob, Options opt = null)
        {
            _doJob = doJob;
            _option = opt ?? new Options();

            _semaphore = new SemaphoreSlim(_option.MaxParallel);

            _task = Task.Run(async () =>
              {
                  while (!_isStop)
                  {
                      if (_data.TryDequeue(out T itm))
                      {
                          await _semaphore.WaitAsync();
                          var t = Task.Run(()=> {
                              _doJob(itm);
                              _semaphore.Release();
                          });
                          _subTask.Add(t);
                      }

                      await Task.Delay(1);
                  }

              });
        }

        public async Task Request(T item)
        {
            _data.Enqueue(item);
            await Task.Delay(1);
        }

        public async Task Complete(bool forceComplete=false)
        {
            _isStop = true;
           
            await Task.WhenAll(_subTask.Where(x => x.Status != TaskStatus.RanToCompletion)
               .Where(x => x.Status != TaskStatus.Faulted)
               .Where(x => x.Status != TaskStatus.Canceled));
            await _task;
            
            if (forceComplete) { return; }

            while (_data.TryDequeue(out T itm))
            {
                _doJob(itm);
            }
        }
    }

    class Program
    {


        static int w = 200;
        static int h = 300;

        private static async Task<long> DoMaxParallelTask<Tin, Tout>(string name, ConcurrentQueue<Tin> queueInput, ConcurrentQueue<Tout> queueOut
            , Action<Tin> doOne, int maxParallel, int totalSize)
        {
            List<Task> allTask = new List<Task>(); var allSw = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(maxParallel);
            Tout _nullObj = default(Tout); Tin objIn = default(Tin); var counter = 0;
            while (counter < totalSize)
            {
                if ((queueInput.TryDequeue(out objIn) && objIn != null))
                {
                    await semaphore.WaitAsync();
                    counter++;
                    var td = Task.Run(() =>
                    {
                        if (objIn == null) return;

                        Tout objOut = _nullObj;
                        try
                        {
                            doOne(objIn);
                        }
                        catch (Exception ex)
                        {
                            queueInput.Enqueue(objIn);
                            Console.WriteLine($"{name} {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    allTask.Add(td);
                }

                await Task.Delay(1);
            }
            Console.WriteLine($"{name}:{queueInput.Count} ");
          

            await Task.WhenAll(allTask.Where(x => x.Status != TaskStatus.RanToCompletion)
               .Where(x => x.Status != TaskStatus.Faulted)
               .Where(x => x.Status != TaskStatus.Canceled));
            allSw.Stop();
            return allSw.ElapsedMilliseconds;
        }

        public static async Task Main()
        {
            ConcurrentQueue<string> queueUrl = new ConcurrentQueue<string>();
            ConcurrentQueue<byte[]> queueDownloaded = new ConcurrentQueue<byte[]>();
            ConcurrentQueue<byte[]> queueResized = new ConcurrentQueue<byte[]>();

            var totalItem = 100;

            for (var i = 0; i < totalItem; i++)
            {
                queueUrl.Enqueue("https://picsum.photos/600/900");
            }

            var t = Task.Run(async () =>
            {
                while (queueUrl.Count != 0 || queueDownloaded.Count != 0 || queueResized.Count != 0)
                {
                    Console.WriteLine($"url: {queueUrl.Count} download:{queueDownloaded.Count} resize:{queueResized.Count}");

                    await Task.Delay(1000);
                }
            });

            var sw = Stopwatch.StartNew();
            var startTime = DateTime.Now;

            var td = DoMaxParallelTask<string, byte[]>("Download", queueUrl, queueDownloaded, async (objIn) =>
            {
                //  Console.WriteLine($"Download Thread {ThreadPool.ThreadCount} ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                HttpClient httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(objIn);

                var r = await httpClient.GetByteArrayAsync(objIn);
                queueDownloaded.Enqueue(r);

            }, 96, totalItem);

            var tr = DoMaxParallelTask<byte[], byte[]>("Resize", queueDownloaded, queueResized, (objIn) =>
            {
                // Console.WriteLine($"Resize Thread {ThreadPool.ThreadCount} ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                var ms = new MemoryStream();
                using (var image = SixLabors.ImageSharp.Image.Load(objIn))
                {
                    image.Mutate(x => x.Resize(w, h));
                    image.SaveAsJpeg(ms, new JpegEncoder
                    {
                        Quality = 80
                    });
                }
                queueResized.Enqueue(ms.ToArray());
            }, 48, totalItem);

            var dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(dirTemp)) Directory.CreateDirectory(dirTemp);

            var ts = DoMaxParallelTask("Save file", queueResized, (ConcurrentQueue<string>)null, (objIn) =>
            {
                //Console.WriteLine($"Save File Thread {ThreadPool.ThreadCount} ThreadId:{Thread.CurrentThread.ManagedThreadId}");
                string filename = Path.Combine(dirTemp, $"{Guid.NewGuid()}.jpg");
                var ms = new MemoryStream();
                using (var image = SixLabors.ImageSharp.Image.Load(objIn))
                {
                    image.Save(filename);
                }

                int cfile = Directory.GetFiles(dirTemp).Count();
                if (cfile == totalItem)
                {
                    var dNow = DateTime.Now;
                    var elasped = dNow.Subtract(startTime).TotalMilliseconds;
                    Console.WriteLine($"All file save to disk {cfile} in {elasped}");
                }

            }, 96, totalItem);

            await Task.WhenAll(new List<Task> { td, tr, ts });

            sw.Stop();

            await t;

            Console.WriteLine($"All in {sw.ElapsedMilliseconds}");


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
