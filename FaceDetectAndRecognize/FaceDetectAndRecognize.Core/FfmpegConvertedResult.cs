using System.Collections.Generic;

namespace FaceDetectAndRecognize.Core
{
    public class FfmpegConvertedResult
    {
        /// <summary>
        /// no mater what video still rendered
        /// </summary>
        public bool Success { get; set; }

        public string CmdOutput { get; set; }

        public long ConvertInMiliseconds { get; set; }
        public string FfmpegCmd { get;  set; }

        public List<FfmpegConvertedResult> SubResult { get; set; }

        public FfmpegCommandLine CommadExecuted { get; set; }
    }
}

/* 
 *    //string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");
            //var file1 = Path.Combine(dir, "1.jpg");
            //var file2 = Path.Combine(dir, "2.jpg");
            //var file3 = Path.Combine(dir, "3.mp3");
            //var file4 = Path.Combine(dir, "4.mp4");
            //var bat1 = Path.Combine(dir, "bat1.bat");

            //Console.WriteLine(file1);
            //Console.WriteLine(file2);
            //Console.WriteLine(file3);
            //Console.WriteLine(file4);
            //Console.WriteLine(bat1);

            //try
            //{
            //    File.Delete(file4);
            //}
            //catch
            //{

            //}

            //string args = $" -loop 1 -t 5 -i \"{file1}1\" -loop 1 -t 5 -i \"{file2}\" -i \"{file3}\" -filter_complex \"[0:v]scale = 1280:720:force_original_aspect_ratio = decrease,pad = 1280:720:(ow - iw) / 2:(oh - ih) / 2,setsar = 1,fade = t =out:st = 4:d = 1[v0];[1:v]scale = 1280:720:force_original_aspect_ratio = decrease,pad = 1280:720:(ow - iw) / 2:(oh - ih) / 2,setsar = 1,fade = t =in:st = 0:d = 1,fade = t =out:st = 4:d = 1[v1];[v0][v1]concat = n = 2:v = 1:a = 0,format = yuv420p[v]\" -map \"[v]\" -map 2:a -shortest \"{file4}\"\r\n";
            //args = "-version";
            //string cmdLine = $"\"{ffmpegCmd}\" {args}";
 * */
