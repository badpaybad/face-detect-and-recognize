using Emgu.CV;
using Emgu.CV.Structure;
using FaceDetectAndRecognize.Core;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FaceDetectAndRecognize.UsageConsoleSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");


            string file3jpg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/3.jpg");

            //recognition

            var duFace = new Image<Bgr, byte>(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/du.jpg"));
            Image<Bgr, byte> imgInput3jpg1 = new Image<Bgr, byte>(file3jpg);

            var recognizedFace = new FaceRecognition().AddTrainData(duFace)
                .WithThreshold(0,100)
                .Train().RecognizePhoto(imgInput3jpg1);

            for (int i = 0; i < recognizedFace.Count; i++)
            {
                FaceDetection.Result f = recognizedFace[i];               

                f.Face.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"sampledata/recoginsed_{i}_l{f.LbphResult.Distance}_e{f.EigenResult.Distance}.jpg"));
            }

            var minDistance = recognizedFace.OrderBy(i => i.EigenResult.Distance).FirstOrDefault();
            imgInput3jpg1.Draw(minDistance.Position, new Bgr(Color.Red), 3);

            imgInput3jpg1.Save(file3jpg + $".faceRecognized.jpg");

            //ffmpeg

            var ffmpegConvertToVid = new FfmpegCommandBuilder()
                .WithInputFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.gif"), 5)
                .WithOutput(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.mp4"))
                .WithOutDuration(5)
                .ToCommand().Run();

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ffmpegConvertToVid));

            //detection
            Image<Bgr, byte> imgInput3jpg = new Image<Bgr, byte>(file3jpg);
            
            var detectedFace = new FaceDetection()
                .DetectByDnnCaffe(imgInput3jpg)
                //.DetectByHaarCascade(imgInput)
                ;

            for (int i = 0; i < detectedFace.Count; i++)
            {
                System.Collections.Generic.KeyValuePair<Image<Bgr, byte>, Rectangle> d = detectedFace[i];
                d.Key.Save(file3jpg+$".cropface_{i}.jpg");
                imgInput3jpg.Draw(d.Value, new Bgr(Color.Yellow),3);
            }
            imgInput3jpg.Save(file3jpg + $".faceDetected.jpg");



            Console.WriteLine("Enter to quit");
            Console.ReadLine();
        }
    }
}
