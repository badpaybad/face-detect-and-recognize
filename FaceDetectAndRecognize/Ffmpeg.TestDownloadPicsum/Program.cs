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

        static ConcurrentQueue<byte[]> _queueDownloadedUrl = new ConcurrentQueue<byte[]>();

        static ConcurrentQueue<MemoryStream> _queeuResizedImage = new ConcurrentQueue<MemoryStream>();

        static List<long> _timeDownloads = new List<long>();
        static List<long> _timeResize = new List<long>();
        static List<long> _timeSaveFile = new List<long>();

        static int _totalItem = 1000;

        static int _batchDownload = 10;

        static int _batchResize = 100;

        static int _batchSaveFile = 100;

        public static void Main()
        {
            var totalItem = _totalItem;

            _dirTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            if (Directory.Exists(_dirTemp) == false) Directory.CreateDirectory(_dirTemp);

            List<string> urls = new List<string>();

            for (var i = 0; i < totalItem; i++)
            {
                urls.Add("https://picsum.photos/600/900");
            }

            var counterDownload = 0;

            _start = DateTime.Now;

            new Thread(async () =>
            {
                while (true)
                {
                    if (counterDownload > totalItem) return;

                    var items = urls.Skip(counterDownload).Take(_batchDownload).ToList();

                    //Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = batchDownload }, (url) =>
                    //{

                    //    if (counterDownload > totalItem) return;

                    //    counterDownload++;
                    //    var sw = Stopwatch.StartNew();
                    //    using (var httpClient = new HttpClient())
                    //    {
                    //        httpClient.BaseAddress = new Uri(url);
                    //        using (var ms = new MemoryStream())
                    //        {
                    //            var stream = httpClient.GetStreamAsync(url).GetAwaiter().GetResult();

                    //            stream.CopyTo(ms);
                    //            _queueDownloadedUrl.Enqueue(ms.ToArray());
                    //        }                               

                    //        //var base64 = Convert.ToBase64String(ms.ToArray());
                    //    }
                    //    sw.Stop();
                    //    Console.WriteLine($"Download in {sw.ElapsedMilliseconds} miliseconds");

                    //});

                    List<Task> tasks = new List<Task>();

                    foreach (var ms in items)
                    {
                        var url = ms;
                        tasks.Add(Task.Run(async () =>
                        {
                            if (counterDownload > totalItem) return;

                            counterDownload++;
                            var sw = Stopwatch.StartNew();
                            using (var httpClient = new HttpClient())
                            {
                                httpClient.BaseAddress = new Uri(url);
                                using (var ms = new MemoryStream())
                                {
                                    var stream = await httpClient.GetStreamAsync(url);

                                    stream.CopyTo(ms);
                                    _queueDownloadedUrl.Enqueue(ms.ToArray());
                                }

                                //var base64 = Convert.ToBase64String(ms.ToArray());
                            }
                            sw.Stop();
                            _timeDownloads.Add(sw.ElapsedMilliseconds);
                        }));
                    }

                    await Task.WhenAll(tasks);

                    await Task.Delay(1);
                    //Console.WriteLine($"Downloaded {counterDownload}");
                }
            }).Start();

            new Thread(() =>
           {
               var countRezise = 0;
               while (true)
               {
                   if (countRezise > totalItem) return;

                   List<byte[]> items = new List<byte[]>();
                   for (var i = 0; i < _batchResize; i++)
                   {
                       if (_queueDownloadedUrl.TryDequeue(out byte[] ms) && ms != null)
                       {
                           items.Add(ms);
                       }
                   }

                   Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = _batchResize }, (ms) =>
                   {
                       if (countRezise > totalItem) return;

                       countRezise++;
                       var sw = Stopwatch.StartNew();
                       var outMs = new MemoryStream();
                       using (var image = SixLabors.ImageSharp.Image.Load(ms))
                       {
                           image.Mutate(x => x.Resize(200, 300));
                           image.SaveAsJpeg(outMs);
                       }
                       _queeuResizedImage.Enqueue(outMs);
                       sw.Stop();
                       _timeResize.Add(sw.ElapsedMilliseconds);
                   });

                   Thread.Sleep(100);
                   //Console.WriteLine($"Resized {countRezise}");
               }

           }).Start();

            new Thread(async () =>
            {
                var countSaveFile = 0;
                while (true)
                {
                    if (countSaveFile >= totalItem)
                    {
                        CaclculateStop();
                        return;
                    }

                    List<MemoryStream> items = new List<MemoryStream>();
                    for (var i = 0; i < _batchSaveFile; i++)
                    {
                        if (_queeuResizedImage.TryDequeue(out MemoryStream ms) && ms != null)
                        {
                            items.Add(ms);
                        }
                    }

                    //Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = batchResize }, (ms) =>
                    //{
                    //    if (countSaveFile >= totalItem) {
                    //        CaclculateStop();
                    //        return;
                    //    }

                    //    countSaveFile++;

                    //    var dateNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                    //    new Bitmap(ms).Save(Path.Combine(_dirTemp, $"{dateNow}_{Guid.NewGuid()}.jpg"));
                    //    ms.Dispose();
                    //});

                    List<Task> tasks = new List<Task>();

                    foreach (var ms in items)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            if (countSaveFile >= totalItem)
                            {
                                CaclculateStop();
                                return;
                            }

                            countSaveFile++;
                            var sw = Stopwatch.StartNew();
                            var dateNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                            new Bitmap(ms).Save(Path.Combine(_dirTemp, $"{dateNow}_{Guid.NewGuid()}.jpg"));
                            ms.Dispose();
                            sw.Stop();
                            _timeSaveFile.Add(sw.ElapsedMilliseconds);
                        }));                      
                    }

                    await Task.WhenAll(tasks);
                    await Task.Delay(1);

                    //Console.WriteLine($"Saved {countSaveFile}");
                }
            }).Start();

            Console.WriteLine("Type `quit` to exit");

            var cmd = Console.ReadLine();
            if (cmd == "quit")
            {
                //can do stop all worker here
                Environment.Exit(0);
                return;
            }
        }

        static void CaclculateStop()
        {
            var dateNow = DateTime.Now;

            var distance = dateNow.Subtract(_start);

            Console.WriteLine("Total download items: " +  _totalItem);
            Console.WriteLine("Total download in miliseconds: " + distance.TotalMilliseconds);
            Console.WriteLine("Total download in seconds: " + distance.TotalSeconds);

            Console.WriteLine("Everage download in miliseconds: " + _timeDownloads.Sum()/_totalItem);
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
}
