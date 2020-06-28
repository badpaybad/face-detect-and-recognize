
using FaceDetectAndRecognize.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FaceDetectAndRecognize.Core
{
    public class FfmpegCommandBuilder
    {
        static List<string> _xfadeImageConst = new List<string> {
 "fade",
"wipeleft",
"wiperight",
"wipeup",
"wipedown",
"slideleft",
"slideright",
"slideup",
"slidedown",
"circlecrop",
"rectcrop",
"distance",
"fadeblack",
"fadewhite",
"radial",
"smoothleft",
"smoothright",
"smoothup",
"smoothdown",
"circleopen",
"circleclose",
"vertopen",
"vertclose",
"horzopen",
"horzclose",
"dissolve",
"pixelize",
"diagtl",
"diagtr",
"diagbl",
"diagbr",
"hlslice",
"hrslice",
"vuslice",
"vdslice"};

        static Random _rnd = new Random();
        public FileType InputFileType { get; private set; }

        public string InputFile { get; private set; }

        public string OutputFile { get; private set; }

        public decimal OutputDuration { get; private set; }
        public decimal InputDuration { get; private set; }

        public decimal Width { get; private set; }

        public decimal Height { get; private set; }

        public string CmdText { get; private set; }


        string _videoScale
        {
            get
            {
                if (Width == 0 || Height == 0) return "1280:720";

                return $"{Width}:{Height}";
            }
        }

        string _fps = "fps=fps=24";

        public FfmpegCommandBuilder WithInputFile(string filePath, decimal inputDuration)
        {
            InputFile = filePath;

            InputFileType = ImageHelper.GetKnownFileType(filePath);
            InputDuration = inputDuration;

            return this;
        }

        public FfmpegCommandBuilder WithOutDuration(decimal duration)
        {
            OutputDuration = duration;
            return this;
        }
        public FfmpegCommandBuilder WithScale(int width, int height)
        {
            Width = width;
            Height = height;
            return this;
        }
        public FfmpegCommandBuilder WithOutput(string fileOutput)
        {
            OutputFile = fileOutput;
            return this;
        }
        string _overlayFile;
        FileType _overlayFileType;
        decimal _overlayFromSeconds;
        decimal _overlayDuration;
        string _overlayScale;
        decimal _overlayPosX;
        decimal _overlayPosY;
        int _overlayBorderSize = 0;
        string _overlayBorderColor = "black";
        int _overlayRoate;
        public FfmpegCommandBuilder WithFileOverlay(string fileOverlay, decimal fromSeconds, decimal duration, string scale, decimal posX, decimal posY, int borderSize = 0, string borderColor = "black", int overlayRotate = 0)
        {
            _overlayBorderSize = borderSize;
            _overlayBorderColor = borderColor;
            _overlayFile = fileOverlay;
            _overlayFileType = ImageHelper.GetKnownFileType(fileOverlay);
            _overlayDuration = duration;
            _overlayFromSeconds = fromSeconds;
            _overlayScale = scale;

            _overlayPosX = posX;
            _overlayPosY = posY;
            _overlayRoate = overlayRotate;
            return this;
        }

        string _overlayText;
        string _overlayFontFilePath;
        int _overlayFontSize = 24;
        string _overlayTextColor = "white";
        bool _overlayTextAllowBg = true;
        string _overlayTextBgColor = "black";

        public FfmpegCommandBuilder WithTextOverlay(string overlayText, string overlayTextColor = "white", decimal posX = 0, decimal posY = 0, int overlayFontSize = 24, bool overlayTextAllowBg = true, string overlayTextBgColor = "black", string overlayFontFilePath = "")
        {
            _overlayPosX = posX;
            _overlayPosY = posY;
            _overlayFontFilePath = overlayFontFilePath;
            _overlayFontSize = overlayFontSize;
            _overlayTextColor = overlayTextColor;
            _overlayTextAllowBg = overlayTextAllowBg;
            _overlayTextBgColor = overlayTextBgColor;
            _overlayText = overlayText;
            if (string.IsNullOrEmpty(_overlayFontFilePath))
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts/arialbd.ttf");
            }
            return this;
        }

        string _audioFile;
        public FfmpegCommandBuilder WithAudio(string audioFile)
        {
            _audioFile = audioFile;
            return this;
        }

        string _fadeMode;
        int _fadeDuration;
        string _fadeFile;
        decimal _fadeFileDuration;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileVideoNext"></param>
        /// <param name="fadeFileDuration"></param>
        /// <param name="fadeDuration"></param>
        /// <param name="fadeMode">fade, wipeleft, wiperight, wipeup, wipedown, slideleft, slideright, slideup, slidedown, circlecrop, rectcrop, distance, fadeblack, fadewhite, radial, smoothleft, smoothright, smoothup, smoothdown, circleopen, circleclose, vertopen, vertclose, horzopen, horzclose, dissolve, pixelize, diagtl, diagtr, diagbl, diagbr, hlslice, hrslice, vuslice, vdslice</param>
        /// <returns></returns>
        public FfmpegCommandBuilder WithTransitionNext(string fileVideoNext, decimal fadeFileDuration, int fadeDuration, string fadeMode = "")
        {
            _fadeFileDuration = fadeFileDuration;
            _fadeDuration = fadeDuration;
            _fadeMode = fadeMode;
            _fadeFile = fileVideoNext;
            if (string.IsNullOrEmpty(_fadeMode))
            {
                _fadeMode = _xfadeImageConst[_rnd.Next(0, _xfadeImageConst.Count - 1)];
            }
            return this;
        }

        public FfmpegCommandLine ToCommand()
        {
            if (OutputDuration == 0 || string.IsNullOrEmpty(InputFile))
            {
                throw new Exception("OutputDuration do not allow = 0 or InputFile not allow empty ");
            }
            if (string.IsNullOrEmpty(OutputFile))
            {
                OutputFile = InputFile + ".mp4";
            }

            if (!string.IsNullOrEmpty(_overlayText) && !string.IsNullOrEmpty(_overlayFile))
            {
                throw new Exception("Just support one time per convert for text or file overlay");
            }

            if (!string.IsNullOrEmpty(_overlayText))
            {
                if (InputFileType != FileType.Mp4) throw new Exception($"Not support InputFileType, just support mp4: {InputFile}");

                CmdText = BuildTextOverlayCommand(InputFile, OutputFile, _overlayText, _overlayPosX, _overlayPosY
                              , _overlayFontFilePath, _overlayFontSize, _overlayTextColor, _overlayTextAllowBg ? 1 : 0, _overlayTextBgColor);

            }
            else if (!string.IsNullOrEmpty(_overlayFile))
            {
                if (InputFileType != FileType.Mp4) throw new Exception($"Not support InputFileType, just support mp4: {InputFile}");

                switch (_overlayFileType)
                {
                    case FileType.Mp4:
                        CmdText = BuildVideoOverlayCommand(InputFile, OutputFile, _overlayFile, _overlayFromSeconds, _overlayDuration, _overlayScale, _overlayPosX, _overlayPosY, _overlayBorderSize, _overlayBorderColor, _overlayRoate);
                        break;
                    case FileType.Gif:
                        CmdText = BuildGiftOverlayCommand(InputFile, OutputFile, _overlayFile, _overlayFromSeconds, _overlayDuration, _overlayScale, _overlayPosX, _overlayPosY, _overlayRoate);
                        break;
                    case FileType.Bmp:
                    case FileType.Jpeg:
                    case FileType.Png:
                        CmdText = BuildImageOverlayCommand(InputFile, OutputFile, _overlayFile, OutputDuration, _overlayFromSeconds, _overlayDuration, _overlayScale, _overlayPosX, _overlayPosY, _overlayRoate); ;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(_fadeFile))
            {
                if (InputFileType != FileType.Mp4) throw new Exception($"Not support InputFileType, just support mp4: {InputFile}");

                if (string.IsNullOrEmpty(_fadeMode)) throw new Exception($"Fade mode is not allow empty");

                var nextFileType = ImageHelper.GetKnownFileType(_fadeFile);

                if (nextFileType != FileType.Mp4) throw new Exception($"Not support transition file type, just support mp4: {_fadeFile}");

                CmdText = BuildFfmpegCommandTransitionXFade(InputFile, _fadeFile, OutputFile, InputDuration, _fadeFileDuration, _fadeDuration, _fadeMode);

            }
            else if (!string.IsNullOrEmpty(_audioFile))
            {
                if (InputFileType != FileType.Mp4) throw new Exception($"Not support InputFileType, just support mp4: {InputFile}");
                CmdText = BuildAudioCommand(InputFile, OutputFile, _audioFile, OutputDuration);
            }
            else
            {
                switch (InputFileType)
                {
                    case FileType.Gif:
                        CmdText = BuildCmdForGiftToVideo(InputFile, OutputFile, OutputDuration);
                        break;
                    case FileType.Bmp:
                    case FileType.Jpeg:
                    case FileType.Png:
                        CmdText = BuildCmdForImgToVideo(InputFile, OutputFile, OutputDuration);
                        break;
                    default:
                        CmdText = BuildCmdConvertToMp4(InputFile, OutputFile, OutputDuration);
                        break;
                }
            }

            return new FfmpegCommandLine
            {
                FfmpegCommand = CmdText,
                FileInput = InputFile,
                FileOutput = OutputFile,
                Duration = OutputDuration
            };
        }

        /// <summary>
        /// only suport 2 file input
        /// </summary>
        /// <param name="fileInput"></param>
        /// <param name="fileOutput"></param>
        /// <param name="timeOfEachInput"></param>
        /// <param name="fadeDuration"></param>
        /// <param name="fadeMethod"> fade, wipeleft, wiperight, wipeup, wipedown, slideleft, slideright, slideup, slidedown, circlecrop, rectcrop, distance, fadeblack, fadewhite, radial, smoothleft, smoothright, smoothup, smoothdown, circleopen, circleclose, vertopen, vertclose, horzopen, horzclose, dissolve, pixelize, diagtl, diagtr, diagbl, diagbr, hlslice, hrslice, vuslice, vdslice </param>
        /// <returns></returns>
        string BuildFfmpegCommandTransitionXFade(string fileInput0, string fileInput1, string fileOutput, decimal durationInput0, decimal durationInput1, decimal fadeDuration
            , string fadeMethod)
        {
            //https://trac.ffmpeg.org/wiki/Xfade
            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            if (!string.IsNullOrEmpty(_fadeMode))
            {
                fadeMethod = _fadeMode;
            }

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            string cmd = $"\"{ffmpegCmd}\" -y";
            string filterFadeIndex = "";
            string filterScaleImage = "";


            durationInput0 = Math.Round(durationInput0, 2);

            durationInput1 = Math.Round(durationInput1, 2);

            fadeDuration = Math.Round(fadeDuration, 2);

            var offset = durationInput0 - fadeDuration;
            offset = durationInput0 - fadeDuration;

            cmd += $" -i \"{fileInput0}\"";

            offset = Math.Round(offset, 2);

            if (offset <= 0) offset = (decimal)0.1;

            cmd += $" -i \"{fileInput1}\"";

            cmd += $" -t {durationInput0 + durationInput1} -i \"{fileAudioSilence}\"";

            filterScaleImage += $"[0:v]scale={_videoScale}:force_original_aspect_ratio=decrease,pad={_videoScale}:(ow-iw)/2:(oh-ih)/2,setsar=1,{_fps}[v0];";
            filterScaleImage += $"[1:v]scale={_videoScale}:force_original_aspect_ratio=decrease,pad={_videoScale}:(ow-iw)/2:(oh-ih)/2,setsar=1,{_fps}[v1];";

            filterFadeIndex += $"[v0][v1]";

            cmd += $" -filter_complex \"{filterScaleImage}{filterFadeIndex}xfade=transition={fadeMethod}:duration={fadeDuration}:offset={offset},format=yuv420p[v]\"";

            cmd += $" -map \"[v]\" -map 2:a \"{fileOutput}\"";

            while (cmd.IndexOf("\\") >= 0)
            {
                cmd = cmd.Replace("\\", "/");
            }

            return cmd;
        }

        string BuildAudioCommand(string fileInput, string fileOutput, string fileAudio, decimal videoDuration)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            if (string.IsNullOrEmpty(fileAudio))
            {
                fileAudio = fileAudioSilence;
            }

            string cmd = $"\"{ffmpegCmd}\" -y -i {fileInput} -stream_loop {videoDuration} -i {fileAudio} -c copy -map 0:v:0 -map 1:a:0 -shortest {fileOutput}";

            return cmd;
        }

        public string BuildImageOverlayCommand(string fileInput, string fileOutput, string fileImageOverlay, decimal videoDuration, decimal fromSeconds, decimal duration, string scale, decimal x, decimal y, int rotate)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");
            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");
            string tempScale = "";
            if (!string.IsNullOrEmpty(scale))
            {
                tempScale = $"scale={scale},";
            }
            string enableDuration = string.Empty;
            if (duration != 0)
            {
                enableDuration = $":enable='between(t, {fromSeconds}, {fromSeconds + duration})'";
            }
            var rotateFilter = "[ovrl]";
            if (rotate != 0)
            {
                rotateFilter = $"[xsc];[xsc]rotate={rotate}*PI/180:c=none:ow=rotw(iw):oh=roth(ih)[ovrl]";
            }
            string cmd = $"\"{ffmpegCmd}\" -y -i \"{fileInput}\" -loop 1 -t {videoDuration} -i \"{fileImageOverlay}\" -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"[1:v]{_fps},{tempScale}setsar=1{rotateFilter};[0:v][ovrl]overlay = {x}:{y}{enableDuration}[v]\" -map \"[v]\" -map 2:a -shortest \"{fileOutput}\"";

            return cmd;
        }

        public string BuildTextOverlayCommand(string fileInput, string fileOutput, string text, decimal x, decimal y, string pathfont, int fontSize, string fontColor, int allowBg = 1, string bgColor = "black")
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            pathfont = pathfont.Replace("\\", "/");

            var arrLineText = text.Split('\n');

            var xpos = "(w-text_w)/2";
            var ypos = "(h-text_h)/2";
            if (x != 0 && y != 0)
            {
                xpos = $"{x}";
                ypos = $"{y}";
            }

            var line = "";
            for (int i = 0; i < arrLineText.Length; i++)
            {
                string l = arrLineText[i].Trim(new[] { ' ', '\r', '\n' });

                var lineSpace = i * (fontSize / 3 + fontSize);

                line += $"drawtext=fontfile=\'{pathfont}\':text='{l}':fontcolor={fontColor}:fontsize={fontSize}:box={allowBg}:boxcolor={bgColor}@0.5:boxborderw=5:x={xpos}:y={lineSpace}+{ypos},";
            }
            line = line.Trim(',', ' ', '\r', '\n');
            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");
            string cmd = $"\"{ffmpegCmd}\" -y -i \"{fileInput}\" -vf \"[in]{line}[out]\" -codec:a copy \"{fileOutput}\"";

            return cmd;
        }

        public string BuildGiftOverlayCommand(string fileInput, string fileOutput, string fileGiftOverlay, decimal fromSeconds, decimal duration, string scale, decimal x, decimal y, int rotate)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");
            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            var enableDuration = string.Empty;
            string tempScale = "";
            if (!string.IsNullOrEmpty(scale))
            {
                tempScale = $"scale={scale},";
            }
            if (duration != 0)
            {
                enableDuration = $":enable='between(t, {fromSeconds}, {fromSeconds + duration})'";
            }

            var rotateFilter = "[ovrl]";
            if (rotate != 0)
            {
                rotateFilter = $"[xsc];[xsc]rotate={rotate}*PI/180:c=none:ow=rotw(iw):oh=roth(ih)[ovrl]";
            }

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            string cmd = $"\"{ffmpegCmd}\" -y -i \"{fileInput}\" -ignore_loop 0 -i \"{fileGiftOverlay}\" -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"[1:v]{_fps},{tempScale}setsar=1{rotateFilter};[0:v][ovrl]overlay = {x}:{y}{enableDuration}[v]\" -map \"[v]\" -map 2:a -shortest \"{fileOutput}\"";

            return cmd;
        }

        public string BuildVideoOverlayCommand(string fileInput, string fileOutput, string fileGiftOverlay, decimal fromSeconds, decimal duration, string scale, decimal x, decimal y, int borderSize, string borderColor, int rotate)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            var border = string.Empty;
            if (borderSize != 0)
            {

            }
            if (string.IsNullOrEmpty(borderColor)) borderColor = "black";

            var rotateFilter = "[ovrl]";
            if (rotate != 0)
            {
                rotateFilter = $"[xsc];[xsc]rotate={rotate}*PI/180:c=none:ow=rotw(iw):oh=roth(ih)[ovrl]";
            }

            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            var enableDuration = string.Empty;
            string tempScale = "";
            if (!string.IsNullOrEmpty(scale))
            {
                tempScale = $"scale={scale},";
            }
            if (duration != 0)
            {
                enableDuration = $":enable='between(t, {fromSeconds}, {fromSeconds + duration})'";
            }

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            string cmd = $"\"{ffmpegCmd}\" -y -i \"{fileInput}\" -stream_loop {duration} -i \"{fileGiftOverlay}\" -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"[1:v]{_fps},{tempScale}setsar=1{rotateFilter};[0:v][ovrl]overlay={x}:{y}{enableDuration}[v]\" -map \"[v]\" -map 2:a -shortest \"{fileOutput}\"";

            return cmd;
        }

        string BuildCmdForImgToVideo(string fileInput, string fileOutput, decimal duration)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            var loopInput = $" -loop 1 -t {duration} -i \"{fileInput}\"";

            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            //var filterScaleImage = $"[0:v]scale={_videoScale}:force_original_aspect_ratio=decrease,pad={_videoScale}:(ow-iw)/2:(oh-ih)/2,setsar=1,{_fps}[v0]";
            var filterScaleImage = $"[0:v]scale={_videoScale},setsar=1,{_fps}[v0]";

            string cmd = $"\"{ffmpegCmd}\" -y {loopInput} -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"{filterScaleImage};[v0]format=yuv420p[v]\" -map \"[v]\" -map 1:a \"{fileOutput}\"";

            return cmd;
        }

        string BuildCmdForGiftToVideo(string fileInput, string fileOutput, decimal duration)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            var filterScaleImage = $"[0:v]scale={_videoScale}:force_original_aspect_ratio=decrease,pad={_videoScale}:(ow-iw)/2:(oh-ih)/2,setsar=1,{_fps}[v0]";

            string cmd = $"\"{ffmpegCmd}\" -y -stream_loop {duration} -i \"{fileInput}\" -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"{filterScaleImage};[v0]format=yuv420p[v]\" -map \"[v]\"  -map 1:a -shortest \"{fileOutput}\"";

            return cmd;
        }
        string BuildCmdConvertToMp4(string fileInput, string fileOutput, decimal duration)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin");

            string ffmpegCmd = Path.Combine(dir, "ffmpeg.exe");

            var fileAudioSilence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window/ffmpeg/bin/silence.mp3");

            var filterScaleImage = $"[0:v]scale={_videoScale}:force_original_aspect_ratio=decrease,pad={_videoScale}:(ow-iw)/2:(oh-ih)/2,setsar=1,{_fps}[v0]";

            string cmd = $"\"{ffmpegCmd}\" -y -stream_loop {duration} -i \"{fileInput}\" -t {duration} -i \"{fileAudioSilence}\" -filter_complex \"{filterScaleImage};[v0]format=yuv420p[v]\" -map \"[v]\"  -map 1:a -shortest \"{fileOutput}\"";

            return cmd;
        }
    }
}
