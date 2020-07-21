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
                            Console.WriteLine($"{name} error:{ex.Message}");
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
            await Task.WhenAll(allTask);
            allSw.Stop();
            Console.WriteLine($"{name} DONE remain:{queueInput.Count} ");
            return allSw.ElapsedMilliseconds;
        }

        public static async Task Main()
        {
            ConcurrentQueue<string> queueUrl = new ConcurrentQueue<string>();
            ConcurrentQueue<byte[]> queueDownloaded = new ConcurrentQueue<byte[]>();
            ConcurrentQueue<byte[]> queueResized = new ConcurrentQueue<byte[]>();

            var totalItem = 1000;

            for (var i = 0; i < totalItem; i++)
            {
                queueUrl.Enqueue("https://picsum.photos/600/900");
            }

            var t = Task.Run(async () =>
            {
                while (queueUrl.Count != 0 || queueDownloaded.Count != 0 || queueResized.Count != 0)
                {
                    Console.WriteLine($"Queue remain url: {queueUrl.Count} download:{queueDownloaded.Count} resize:{queueResized.Count} thread count:{ThreadPool.ThreadCount}");

                    await Task.Delay(1000);
                }
            });

            var downloadParallel = 24*3;
            var resizeParallel = 12;
            var saveFileParallel = 12;

            var sw = Stopwatch.StartNew();
            var startTime = DateTime.Now;

            var td = DoMaxParallelTask<string, byte[]>("Worker Download", queueUrl, queueDownloaded, async (objIn) =>
            {
               //  Console.WriteLine($"Download Thread {ThreadPool.ThreadCount} ThreadId:{Thread.CurrentThread.ManagedThreadId}");
               HttpClient httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(objIn);

                var r = await httpClient.GetByteArrayAsync(objIn);
                queueDownloaded.Enqueue(r);

            }, downloadParallel, totalItem);

            var tr = DoMaxParallelTask<byte[], byte[]>("Worker Resize", queueDownloaded, queueResized, (objIn) =>
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
            }, resizeParallel, totalItem);

            var dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(dirTemp)) Directory.CreateDirectory(dirTemp);

            var ts = DoMaxParallelTask("Worker Save file", queueResized, (ConcurrentQueue<string>)null, (objIn) =>
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

            }, saveFileParallel, totalItem);

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
