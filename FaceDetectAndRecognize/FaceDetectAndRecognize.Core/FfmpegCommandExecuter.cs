using FaceDetectAndRecognize.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace FaceDetectAndRecognize.Core
{

    public class FfmpegCommandExecuter
    {
        static FfmpegCommandExecuter()
        {

        }

        public FfmpegConvertedResult Run(FfmpegCommandLine cmd)
        {
            if (cmd.IsValid() == false)
            {
                throw new Exception("Not valid ffmpeg command, because the commandline over 8000 character");
            }
            List<FfmpegConvertedResult> subResult = new List<FfmpegConvertedResult>();

            List<string> tempCmd = new List<string>();

            if (cmd.CommandsToBeforeConvert != null && cmd.CommandsToBeforeConvert.Count > 0)
            {
                cmd.CommandsToBeforeConvert.SplitToRun(3, (itms, idx) =>
                {
                    List<Task<FfmpegConvertedResult>> cmdTask = new List<Task<FfmpegConvertedResult>>();
                    foreach (var itm in itms)
                    {
                        tempCmd.Add(itm.FfmpegCommand);
                        cmdTask.Add(Task<FfmpegCommandLine>.Run(() =>
                        {
                            return InternalRun(itm.FfmpegCommand, itm.FileOutput);
                        }));
                    }

                    subResult.AddRange(Task.WhenAll(cmdTask).GetAwaiter().GetResult());
                });
            }

            if (cmd.CommandsToConvert != null && cmd.CommandsToConvert.Count > 0)
            {
                foreach (var subCmd in cmd.CommandsToConvert)
                {
                    FfmpegConvertedResult cmdSubRes = InternalRun(subCmd.FfmpegCommand, subCmd.FileOutput);
                    cmdSubRes.CommadExecuted = subCmd;
                    subResult.Add(cmdSubRes);
                    tempCmd.Add(subCmd.FfmpegCommand);
                }
            }
            tempCmd.Add(cmd.FfmpegCommand);

            var allcmd = string.Join("\r\n\r\n", tempCmd);

            var mainCmdResult = InternalRun(cmd.FfmpegCommand, cmd.FileOutput);

            mainCmdResult.SubResult = subResult;

            mainCmdResult.CommadExecuted = cmd;

            Task.Run(() =>
            {
                foreach (var subCmd in cmd.CommandsToBeforeConvert)
                {
                    try
                    {
                        File.Delete(subCmd.FileOutput);
                    }
                    catch { }
                }
                foreach (var subCmd in cmd.CommandsToConvert)
                {
                    try
                    {
                        File.Delete(subCmd.FileOutput);
                    }
                    catch { }
                }
            });

            return mainCmdResult;
        }

        private FfmpegConvertedResult InternalRun(string cmdLine, string fileOutput)
        {
            try
            {
                File.Delete(fileOutput);
            }
            catch { }

            Stopwatch sw = Stopwatch.StartNew();
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            System.Diagnostics.Process cmd = new System.Diagnostics.Process();

            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            cmd.StartInfo.WorkingDirectory = dir;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.FileName = "cmd.exe";
            //cmd.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            //cmd.StartInfo.StandardOutputEncoding = Encoding.Unicode;
            //cmd.StartInfo.StandardInputEncoding = Encoding.Unicode;
            cmd.Start();

            Console.WriteLine($"FfmpegCommandRunner Started: {fileOutput}");
            //cmd.StandardInput.WriteLine("chcp 65001");
            cmd.StandardInput.WriteLine(cmdLine);
            cmd.StandardInput.WriteLine("echo ##done##");

            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();

            var output = new List<string>();

            cmd.OutputDataReceived += new DataReceivedEventHandler(
                (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) output.Add(e.Data);
                });
            cmd.ErrorDataReceived += new DataReceivedEventHandler(
                (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) output.Add(e.Data);
                });

            cmd.BeginOutputReadLine();
            cmd.BeginErrorReadLine();
            cmd.WaitForExit();

            try
            {
                cmd.Close();
            }
            catch
            {
            }

            try
            {
                cmd.Kill();
            }
            catch { }

            sw.Stop();
            string outstring = string.Join("\r\n", output.ToArray());
            bool isOk = outstring.IndexOf("Error", StringComparison.OrdinalIgnoreCase) <= 0;
            if (!isOk)
            {
                Console.WriteLine("WARNING: " + fileOutput);
            }

            if (File.Exists(fileOutput) == false)
            {
                isOk = false;
                Console.WriteLine($"ERROR: Covert failed, not found file: {fileOutput}");
            }
            else
            {
                isOk = true;
            }

            return new FfmpegConvertedResult
            {
                ConvertInMiliseconds = sw.ElapsedMilliseconds,
                CmdOutput = outstring,
                Success = isOk,
                FfmpegCmd = cmdLine
            };
        }

        private string ReadLineByLine(StreamReader streamReader)
        {
            var line = "";
            var output = "";
            while (true)
            {
                var lastChr = streamReader.Read();
                if (lastChr <= 0) break;

                var outputChr = streamReader.CurrentEncoding.GetString(new byte[] { (byte)lastChr });
                line += outputChr;
                if (outputChr.IndexOf("\n") >= 0)
                {
                    output += line;
                    line = "";
                }
            }

            return output;
        }
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
