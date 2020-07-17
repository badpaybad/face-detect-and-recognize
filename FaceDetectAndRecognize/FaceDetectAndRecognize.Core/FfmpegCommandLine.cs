using System.Collections.Generic;

namespace FaceDetectAndRecognize.Core
{
    public class FfmpegCommandLine
    {
        public int GroupOrder { get; set; }
        public string FileOutput { get; set; }
        public string FileInput { get; set; }
        public decimal OutputDuration { get; set; }

        /// <summary>
        /// main
        /// </summary>
        public string FfmpegCommand { get; set; }

        /// <summary>
        /// these file will delete after main command done
        /// </summary>
        public List<FfmpegCommandLine> CommandsToBeforeConvert { get; set; } = new List<FfmpegCommandLine>();
        /// <summary>
        /// these file will delete after main command done
        /// </summary>
        public List<FfmpegCommandLine> CommandsToConvert { get; set; } = new List<FfmpegCommandLine>();


        public bool IsValid()
        {
            if (FfmpegCommand.Length > 8000) return false;
            if (CommandsToConvert == null || CommandsToConvert.Count == 0) return true;

            foreach (var s in CommandsToConvert)
            {
                if (s.FfmpegCommand.Length > 8000) return false;
            }

            return true;
        }

        public FfmpegConvertedResult Run()
        {
            return new FfmpegCommandExecuter().Run(this);
        }
    }
}
