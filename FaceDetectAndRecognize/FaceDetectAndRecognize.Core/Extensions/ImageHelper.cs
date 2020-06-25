using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FaceDetectAndRecognize.Core.Extensions
{
    public enum FileType
    {
        Unknown,
        Jpeg,
        Bmp,
        Gif,
        Png,
        Pdf,
        Mp4
    }

    public static class ImageHelper
    {


        private static readonly Dictionary<FileType, byte[]> KNOWN_FILE_HEADERS = new Dictionary<FileType, byte[]>()
    {
        { FileType.Jpeg, new byte[]{ 0xFF, 0xD8 }}, // JPEG
		{ FileType.Bmp, new byte[]{ 0x42, 0x4D }}, // BMP
		{ FileType.Gif, new byte[]{ 0x47, 0x49, 0x46 }}, // GIF
		{ FileType.Png, new byte[]{ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }}, // PNG
		{ FileType.Pdf, new byte[]{ 0x25, 0x50, 0x44, 0x46 }} // PDF
	};

        public static FileType GetKnownFileType(ReadOnlySpan<byte> data)
        {
            foreach (var check in KNOWN_FILE_HEADERS)
            {
                if (data.Length >= check.Value.Length)
                {
                    var slice = data.Slice(0, check.Value.Length);
                    if (slice.SequenceEqual(check.Value))
                    {
                        return check.Key;
                    }
                }
            }

            return FileType.Unknown;
        }
        public static FileType GetKnownFileType(MemoryStream stream)
        {
            return GetKnownFileType(stream.ToArray());
        }

        public static FileType GetKnownFileType(string pathFile)
        {
            pathFile = pathFile.Trim(new[] { ' ', '/', '\\', '\r', '\n' });

            if (pathFile.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) return FileType.Mp4;
            if (pathFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return FileType.Jpeg;
            if (pathFile.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) return FileType.Jpeg;
            if (pathFile.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) return FileType.Gif;
            if (pathFile.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) return FileType.Bmp;
            if (pathFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return FileType.Png;

            using (var m = new MemoryStream())
            {
                using (var file = File.OpenRead(pathFile))
                {
                    file.CopyTo(m);
                    return GetKnownFileType(m);
                }
            }

        }

        public static List<string> GetImageOnly(params string[] files)
        {
            List<string> res = new List<string>();

            foreach (var f in files)
            {
                var t = GetKnownFileType(f);
                if (t == FileType.Bmp || t == FileType.Gif || t == FileType.Png || t == FileType.Jpeg)
                { res.Add(f); }
            }

            return res;
        }
    }
}
