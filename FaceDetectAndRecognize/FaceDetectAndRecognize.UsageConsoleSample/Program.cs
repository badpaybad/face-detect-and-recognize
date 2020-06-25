using FaceDetectAndRecognize.Core;
using System;
using System.IO;

namespace FaceDetectAndRecognize.UsageConsoleSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

           var ffmpegConvertToVid= new FfmpegCommandBuilder()
                .WithInputFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.gif"), 5)
                .WithOutput(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.mp4"))
                .WithOutDuration(5)
                .ToCommand().Run();

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ffmpegConvertToVid));

            Console.ReadLine();
        }
    }
}
