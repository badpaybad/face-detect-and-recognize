using Emgu.CV;
using Emgu.CV.Structure;
using FaceDetectAndRecognize.Core;
using System;
using System.Configuration;
using System.IO;

namespace FaceDetectAndRecognize.ConsoleFindInAlbum
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");


            var dirRoot = ConfigurationManager.AppSettings["DirRoot"];

            var rootIds = Directory.GetDirectories(dirRoot);

            foreach(var rp in rootIds)
            {
                var rid = GetIdByPath(rp);
                var identityDir = Directory.GetDirectories(Path.Combine(dirRoot, rid.ToString()));
                foreach(var ip in identityDir)
                {
                    var iid = GetIdByPath(ip);
                    var folderFace = Path.Combine(dirRoot, $"{rid}/{iid}/face");
                    var folderAlbum = Path.Combine(dirRoot, $"{rid}/{iid}/album");
                    var folderResult = Path.Combine(dirRoot, $"{rid}/{iid}/result");
                    
                    CreateDirIfNotExist(folderAlbum, folderFace, folderResult);

                    var listFace = Directory.GetFiles(folderFace);
                    var listPhoto = Directory.GetFiles(folderAlbum);

                    var faceRekognize = new FaceRecognition(iid);

                    foreach(var ff in listFace)
                    {
                        faceRekognize.AddTrainData(new Image<Bgr, byte>(ff));
                    }
                    faceRekognize.WithThreshold(3000, 44);
                    faceRekognize.Train();

                    foreach(var photo in listPhoto)
                    {
                        Image<Bgr, byte> pOrigin = new Image<Bgr, byte>(photo);
                        var pInfo = new FileInfo(photo);
                        var result= faceRekognize.RecognizePhoto(pOrigin);
                        if (result.Count > 0)
                        {
                            string dirFaceOfPhoto = Path.Combine(folderResult, pInfo.Name);
                         
                            CreateDirIfNotExist(dirFaceOfPhoto);

                            foreach (var r in result)
                            {
                                r.Face.Save(Path.Combine(dirFaceOfPhoto, $"id{r.EigenResult.Label}_e{r.EigenResult.Distance}_l{r.LbphResult.Distance}_f{r.FisherResult.Distance}_{pInfo.Name}"));
                            }

                            pOrigin.Save(Path.Combine(folderResult, pInfo.Name));
                        }
                    }
                }
            }

            Console.WriteLine("Enter to quit!");
            Console.ReadLine();
        }

        static int GetIdByPath(string path)
        {
            string[] arr = path.Split(new[] { ' ', '/', '\\' });
            return int.Parse(arr[arr.Length - 1]);
        }

        static void CreateDirIfNotExist(params string [] dirPaths)
        {
            foreach(var d in dirPaths)
            {
                if (Directory.Exists(d) == false) Directory.CreateDirectory(d);
            }
        }
    }
}
