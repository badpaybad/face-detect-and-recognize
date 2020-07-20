using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FaceDetectAndRecognize.Core
{
    public class FaceRecognitionCombination
    {
        public class FaceDataTrain
        {
            public Image<Bgr, byte> Face;
            public int Identity;
            public Rectangle FaceBound;
        }

        public class Result
        {
            public Image<Bgr, byte> Face;
            public Rectangle FaceBound;
            public EigenFaceRecognizer.PredictionResult EigenResult;
            public LBPHFaceRecognizer.PredictionResult LbphResult;
            public FisherFaceRecognizer.PredictionResult FisherResult { get; set; }
        }

        LBPHFaceRecognizer _lBPHFaceRecognizer;
        EigenFaceRecognizer _eigenRecognizer;
        FisherFaceRecognizer _fisherFaceRecognizer;

        List<FaceDataTrain> _originImageToTrain = new List<FaceDataTrain>();

        List<FaceDataTrain> _originFaceDetectedToTrain = new List<FaceDataTrain>();
        List<FaceDataTrain> _dataTrainAlign = new List<FaceDataTrain>();

        int _faceWidth;
        int _faceHeight;

        public FaceRecognitionCombination()
        {
            _eigenRecognizer = new EigenFaceRecognizer(80, double.PositiveInfinity);
            _lBPHFaceRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);//50
            _fisherFaceRecognizer = new FisherFaceRecognizer(0, 3500);//4000
        }

        /// <summary>
        /// will try find face and align face
        /// </summary>
        /// <param name="faceToTrain"></param>
        /// <returns></returns>
        public FaceRecognitionCombination TrainOrLoadModel(List<FaceDataTrain> faceToTrain, out string eigenModelFile, out string lbphModelFile)
        {
            _originImageToTrain = faceToTrain;

            eigenModelFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eigenModel.txt");
            lbphModelFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lbphModel.txt");
            if (File.Exists(eigenModelFile) && File.Exists(lbphModelFile))
            {
                _eigenRecognizer.Read(eigenModelFile);
                _lBPHFaceRecognizer.Read(lbphModelFile);

                return this;
            }

            BatchProcess.SplitToRun(_originImageToTrain, (itemsOrg) =>
            {
                foreach (var f1 in itemsOrg)
                {
                    var f = f1;
                    var found = DetectFace(f.Face);
                    foreach (var i in found)
                    {
                        _originFaceDetectedToTrain.Add(new FaceDataTrain { Identity = f.Identity, Face = i.Key, FaceBound = i.Value });
                    }
                }
            }, 3);

            _faceWidth = (int)(_originFaceDetectedToTrain.Select(i => i.Face.Width).Sum() / _originFaceDetectedToTrain.Count);
            _faceHeight = (int)(_originFaceDetectedToTrain.Select(i => i.Face.Height).Sum() / _originFaceDetectedToTrain.Count);

            var groupBy = _originFaceDetectedToTrain.GroupBy(i => i.Identity).Select(i => new { Identity = i.Key, Faces = i.DefaultIfEmpty() }).ToList();

            List<Mat> data = new List<Mat>();
            List<int> dataId = new List<int>();

            BatchProcess.SplitToRun(groupBy, (itemsGroupBy) =>
            {
                foreach (var f1 in itemsGroupBy)
                {
                    var f = f1;

                    foreach (var fa1 in f.Faces)
                    {
                        var fa = fa1;
                        var temp = AlignFace(fa.Face, _faceWidth, _faceHeight);

                        _dataTrainAlign.Add(new FaceDataTrain { FaceBound = fa.FaceBound, Face = fa.Face, Identity = fa.Identity });
                        data.Add(temp.Mat.IsContinuous ? temp.Mat : temp.Mat.Clone());
                        dataId.Add(fa.Identity);
                    }
                }

            }, 3);

            if (File.Exists(eigenModelFile) == false || File.Exists(lbphModelFile) == false)
            {

                Mat[] images = data.ToArray();
                int[] labels = dataId.ToArray();

                var tEigen = Task.Run(() =>
                {
                    _eigenRecognizer.Train(images, labels);
                });

                var tLbph = Task.Run(() =>
                {
                    _lBPHFaceRecognizer.Train(images, labels);
                });

                //_fisherFaceRecognizer.Train(images, labels);

                Task.WaitAll(tEigen, tLbph);

                _eigenRecognizer.Write(eigenModelFile);

                _lBPHFaceRecognizer.Write(lbphModelFile);
            }
            return this;
        }

        /// <summary>
        /// will return face found in photo and predict result for it, find face and align face
        /// </summary>
        /// <param name="photo"></param>
        /// <returns></returns>
        public List<Result> Predict(Image<Bgr, byte> photo)
        {
            var faces = DetectFace(photo);
            List<Result> results = new List<Result>();

            if (_faceWidth == 0)
            {
                _faceWidth = faces.Select(i => i.Key.Width).Sum() / faces.Count;
            }
            if (_faceHeight == 0)
            {
                _faceHeight = faces.Select(i => i.Key.Height).Sum() / faces.Count;
            }
            foreach (var f1 in faces)
            {
                var f = f1;
                var temp = AlignFace(f.Key, _faceWidth, _faceHeight);

                var fm = temp.Mat.IsContinuous ? temp.Mat : temp.Mat.Clone();
                results.Add(new Result
                {
                    Face = f.Key,
                    FaceBound = f.Value,
                    EigenResult = _eigenRecognizer.Predict(fm),
                    LbphResult = _lBPHFaceRecognizer.Predict(fm),
                    FisherResult = new FisherFaceRecognizer.PredictionResult() //_fisherFaceRecognizer.Predict(fm)
                });
            }


            return results;
        }

        public Image<Gray, byte> AlignFace(Image<Bgr, byte> src, int faceWidth, int faceHeight)
        {
            var temp = src.Resize(faceWidth, faceHeight, Emgu.CV.CvEnum.Inter.Cubic).Convert<Gray, byte>();
            temp._EqualizeHist();
            return temp;
        }

        public List<KeyValuePair<Image<Bgr, byte>, Rectangle>> DetectFace(Image<Bgr, byte> photo)
        {
            using (FaceDetection _faceDetection = new FaceDetection())
            {
                var faceInPhoto = _faceDetection.DetectByHaarCascade(photo);
                // var faceInPhoto = _faceDetection.DetectByDnnCaffe(photo);

                var refilter = new List<KeyValuePair<Image<Bgr, byte>, Rectangle>>();

                for (int i = 0; i < faceInPhoto.Count; i++)
                {
                    KeyValuePair<Image<Bgr, byte>, Rectangle> f = faceInPhoto[i];
                    var detected = _faceDetection.DetectByDnnCaffe(f.Key, 0.50f);

                    //f.Key.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"_face_dnn_{i}_{Math.Abs(Guid.NewGuid().GetHashCode())}.png"));

                    if (detected.Count > 0)
                    {
                        refilter.Add(f);
                    }
                }

                return refilter;
            }
        }

        public void Test(int schoolId)
        {
            var schoolDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ko/{schoolId}");

            var studentIds = Directory.GetDirectories(schoolDir).Select(i => __getId(i)).ToList();

            var rekognitionCombind = new FaceRecognitionCombination();

            List<KeyValuePair<string, int>> fileFaceToTrain = new List<KeyValuePair<string, int>>();

            var schoolIds = Directory.GetDirectories(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ko")).Select(i => __getId(i)).ToList();

            foreach (var scid in schoolIds)
            {
                var student_Ids = Directory.GetDirectories(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ko/{scid}")).Select(i => __getId(i)).ToList();

                foreach (var sid in student_Ids)
                {
                    if (sid == 0) continue;

                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ko/{scid}/{sid}/face");
                    if (Directory.Exists(path))
                    {
                        var fileFaces = Directory.GetFiles(path);

                        foreach (var f in fileFaces)
                        {
                            fileFaceToTrain.Add(new KeyValuePair<string, int>(f, sid));
                        }

                    }
                }
            }

            rekognitionCombind.TrainOrLoadModel(fileFaceToTrain.Select(i => new FaceDataTrain { Face = new Image<Bgr, byte>(i.Key), Identity = i.Value }).ToList(), out string eigenFileModel, out string lbphFileModel);

            var csvFileResult = Path.Combine(schoolDir, $"result_{schoolId}.csv");
            if (File.Exists(csvFileResult) == false)
            {
                var sw = new StreamWriter(csvFileResult, false);
                sw.WriteLine($"Filename,CreatedAt,LbphDistance,LbphLable,EigenDistance,EigenLabel,FisherDistance,FisherLabel,FaceBound,FaceFile,PhotoFile,DrawBoundFile");
                sw.Flush();
                sw.Close();
            }

            foreach (var sid in studentIds)
            {
                if (sid == 0) continue;

                var studentAlbum = Path.Combine(schoolDir, $"{sid}/album");

                var studentFace = Path.Combine(schoolDir, $"{sid}/face");


                var foundDir = Path.Combine(studentAlbum, "found");
                if (Directory.Exists(foundDir) == false) Directory.CreateDirectory(foundDir);

                var files = Directory.GetFiles(studentAlbum);

                foreach (var file in files)
                {
                    Image<Bgr, byte> photo = new Image<Bgr, byte>(file);

                    Console.WriteLine($"Detecting: {file}");

                    var predictResult = rekognitionCombind.Predict(photo);

                    var fileInfo = new FileInfo(file);

                    string fileDrawFaceBound = Path.Combine(foundDir, fileInfo.Name);

                    for (int i = 0; i < predictResult.Count; i++)
                    {
                        Result r = predictResult[i];
                        var groupId = Math.Abs(Guid.NewGuid().GetHashCode());

                        string fileFaceFound = Path.Combine(foundDir, $"{groupId}_{fileInfo.Name}");
                        r.Face.Save(fileFaceFound);

                        if (i % 2 == 0)
                        {
                            photo.Draw(r.FaceBound, new Bgr(Color.Black), i * 2);
                        }
                        else
                        {
                            photo.Draw(r.FaceBound, new Bgr(Color.Red), i * 2);
                        }

                        var sw = new StreamWriter(csvFileResult, true);

                        var rectangleBound = $"{r.FaceBound.X}:{r.FaceBound.Y}:{r.FaceBound.Width}:{r.FaceBound.Height}";

                        sw.WriteLine($"{fileInfo.Name},{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}" +
                            $",{r.LbphResult.Distance},{r.LbphResult.Label}" +
                            $",{r.EigenResult.Distance},{r.EigenResult.Label}" +
                            $",{r.FisherResult.Distance},{r.FisherResult.Label}" +
                            $",{rectangleBound},{fileFaceFound},{file},{fileDrawFaceBound}");
                        sw.Flush();
                        sw.Close();

                        Console.WriteLine($"Result: {System.Text.Json.JsonSerializer.Serialize(new { r.EigenResult, r.LbphResult })}");
                    }

                    photo.Save(fileDrawFaceBound);
                    Console.WriteLine($"Detected: {file}");
                }

            }

            int __getId(string path)
            {
                var arr = path.Split(new char[] { '/', '\\' });
                int.TryParse(arr[arr.Length - 1], out int id);
                return id;

            }
        }
    }
    public class BatchProcess
    {
        public static void SplitToRun<T>(List<T> allItems, Action<List<T>> doBatch, int batchSize = 10)
        {
            var skip = 0;
            while (true)
            {
                var batch = allItems.Skip(skip).Take(batchSize).Distinct().ToList();

                if (batch == null || batch.Count == 0) { return; }

                doBatch(batch);

                skip = skip + batchSize;
            }

        }
    }
}
