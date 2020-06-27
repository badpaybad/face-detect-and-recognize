using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using FaceDetectAndRecognize.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FaceDetectAndRecognize.Core
{
    public class FaceRecognition
    {
        List<Image<Bgr, byte>> _face = new List<Image<Bgr, byte>>();
      
        string _studentDir;
        string _studentDirFace;
        string _studentDirAlbum;
        string _studentDirResult;

        double _eigenThreshold = 2500.0;
        double _lbphThreshold = 44.0;

        List<Image<Gray, byte>> _dataTrain = new List<Image<Gray, byte>>();


        EigenFaceRecognizer _eigenRecognizer;
        private LBPHFaceRecognizer _lbphRecognizer;
        private FisherFaceRecognizer _fisherRecognizer;

        FaceDetection _faceDetection = new FaceDetection();

        int _trainSizeWidth = 0;
        int _trainSizeHeight = 0;

        int _id = 0;

        public FaceRecognition(int id=0)
        {
            _id = id;
        }
       
        public FaceRecognition(string studentDir, string studentDirFace, string studentDirAlbum, string studentDirResult)
        {
            _studentDir = studentDir;
            _studentDirFace = studentDirFace;
            _studentDirAlbum = studentDirAlbum;
            _studentDirResult = studentDirResult;

            AddDataTrainFromFolder(_studentDirFace);

        }

        public FaceRecognition WithThreshold(double eigenThreshold = 2500.0, double lbphThreshold = 44.0)
        {
            _eigenThreshold = eigenThreshold;
            _lbphThreshold = lbphThreshold;

            return this;
        }

        public FaceRecognition AddDataTrainFromFolder(string studentDirFace)
        {
            _studentDirFace = studentDirFace;

            var fileFace = ImageHelper.GetImageOnly(Directory.GetFiles(_studentDirFace));

            foreach (var f in fileFace)
            {
                Image<Bgr, byte> img = new Image<Bgr, byte>(f);

                AddTrainData(img);
            }
            return this;
        }

        public FaceRecognition AddTrainData(params Image<Bgr, byte>[] imgs)
        {
            foreach (var img in imgs)
            {
                _face.Add(img);

                var foundFace = _faceDetection.DetectByHaarCascade(img);

                foreach (var x in foundFace)
                {
                    Image<Gray, byte> item = x.Key.Convert<Gray, byte>();
                    _dataTrain.Add(item);
                }
            }

            return this;
        }
        public FaceRecognition ClearTrainData()
        {
            _face.Clear();

            _dataTrain.Clear();

            return this;
        }
        public FaceRecognition Train(int witdh = 0, int height = 0)
        {
            if (_dataTrain.Count == 0) throw new Exception("No data to train");

            if (Directory.Exists(_studentDirResult) == false && !string.IsNullOrEmpty(_studentDirResult)) Directory.CreateDirectory(_studentDirResult);

            _trainSizeWidth = witdh;
            _trainSizeHeight = height;
            if (_trainSizeWidth == 0 || _trainSizeHeight == 0)
            {
                _trainSizeWidth = _face.Min(i => i.Width);
                _trainSizeHeight = _face.Min(i => i.Height);
            }

            List<Mat> mats = new List<Mat>();
            for (int i = 0; i < _dataTrain.Count; i++)
            {
                Image<Gray, byte> image = _dataTrain[i].Resize(_trainSizeWidth, _trainSizeHeight, Emgu.CV.CvEnum.Inter.Cubic);
                image._EqualizeHist();
                mats.Add(image.Mat);

                
            }

            Mat[] images = mats.ToArray();
            int[] labels = _dataTrain.Select(i => (int)_id).ToArray();

            _eigenRecognizer = new EigenFaceRecognizer(80, double.PositiveInfinity);

            _eigenRecognizer.Train(images, labels);

            _lbphRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);//50
            _lbphRecognizer.Train(images, labels);

            //var fisher = new List<Mat>();
            //fisher.AddRange(mats);
            //fisher.AddRange(mats);
            //var fisherLable = new List<int>();
            //fisherLable.AddRange(labels);
            //fisherLable.AddRange(labels);
            //_fisherRecognizer = new FisherFaceRecognizer(0, 3500);//4000
            //_fisherRecognizer.Train(fisher.ToArray(), fisherLable.ToArray());

            return this;
        }

        public void RecognizeAlbum(string resultFolder = "")
        {
            var fileAlbum = ImageHelper.GetImageOnly(Directory.GetFiles(_studentDirAlbum));

            if (!string.IsNullOrEmpty(resultFolder))
            {
                _studentDirResult = resultFolder;
            }

            if (Directory.Exists(_studentDirResult) == false && !string.IsNullOrEmpty(_studentDirResult)) Directory.CreateDirectory(_studentDirResult);

            fileAlbum.SplitToRun(5, (itms, idx) =>
            {
                List<Task> tks = new List<Task>();
                tks.Add(Task.Run(() =>
                {
                    foreach (var i in itms)
                    {
                        var fileInfo = new FileInfo(i);
                        Image<Bgr, byte> photo = new Image<Bgr, byte>(i);
                        var detected = RecognizePhoto(photo);
                        if (detected.Count > 0)
                        {
                            //foreach (var d in detected)
                            //{
                            //    photo.Draw(d.Position, new Bgr(Color.Yellow), 2);
                            //}
                            var min = detected.OrderBy(i => i.LbphResult.Distance).FirstOrDefault();
                            photo.Draw(min.Position, new Bgr(Color.Red), 6);

                            var max = detected.OrderByDescending(i => i.EigenResult.Distance).FirstOrDefault();
                            photo.Draw(max.Position, new Bgr(Color.Yellow), 2);

                            string fileName = Path.Combine(_studentDirResult, $"{_id}_detected_l{min.LbphResult.Distance}_e{max.EigenResult.Distance}_{fileInfo.Name}");
                            photo.Save(fileName);

                            Console.WriteLine($"YES: {fileName}");
                        }
                        else
                        {
                            Console.WriteLine($"NO: {i}");
                        }
                    }
                }));
                Task.WaitAll(tks.ToArray());
            });

        }

        public List<FaceDetection.Result> RecognizePhoto(Image<Bgr, byte> photo)
        {
            var faceInPhoto = _faceDetection.DetectByDnnCaffe(photo);

            //var refilter = new List<KeyValuePair<Image<Bgr, byte>, Rectangle>>();

            //foreach (var f in faceInPhoto)
            //{
            //    var detected = new FaceDetection().DetectByHaarCascade(f.Key);
            //    if (detected.Count > 0)
            //    {
            //        refilter.Add(f);
            //    }
            //}

            List<FaceDetection.Result> resultFound = new List<FaceDetection.Result>();

            foreach (var fip in faceInPhoto)
            {
                var f = fip.Key.Convert<Gray, byte>().Resize(_trainSizeWidth, _trainSizeHeight, Emgu.CV.CvEnum.Inter.Cubic);
                f._EqualizeHist();

                var fm = f.Mat.IsContinuous ? f.Mat : f.Mat.Clone();

                var r0 = _eigenRecognizer.Predict(fm);
                // var r1 = _fisherRecognizer.Predict(fm);
                var r2 = _lbphRecognizer.Predict(fm);
                if (r0.Label == _id && r2.Label == _id)
                {
                    //you may want to check
                    if (r0.Distance >= _eigenThreshold && r2.Distance <= _lbphThreshold)
                    {
                        resultFound.Add(new FaceDetection.Result
                        {
                            Face = fip.Key,
                            Position = fip.Value,
                            EigenResult = r0,
                            // FisherResult = r1,
                            LbphResult = r2,
                        });
                    }

                }
            }

            return resultFound;
        }


    }
}
