using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FaceDetectAndRecognize.Core
{
    //https://docs.opencv.org/2.4/modules/contrib/doc/facerec/facerec_tutorial.html
    //https://csharp.hotexamples.com/examples/Emgu.CV/EigenObjectRecognizer/-/php-eigenobjectrecognizer-class-examples.html
    public class FaceDetection
    {
        public class Result
        {
            public Image<Bgr, Byte> Face { get; set; }
            public Rectangle Position { get; set; }
            public FaceRecognizer.PredictionResult EigenResult { get; set; }
            public FaceRecognizer.PredictionResult FisherResult { get; set; }
            public FaceRecognizer.PredictionResult LbphResult { get; set; }
        }

        //http://www.emgu.com/wiki/index.php/Camera_Capture_in_7_lines_of_code
        string _pathFileImgOrigin;
        Image<Bgr, Byte> _imgOrigin;
        string _imgOriginFileExt;
        string _fileName;
        static CascadeClassifier _faceDetector;
        static CascadeClassifier _eyeLeftDetector;
        static CascadeClassifier _eyeRightDetector;
        static Emgu.CV.Dnn.Net _dnnNetCaffe;
        static FaceDetection()
        {
            _faceDetector = new CascadeClassifier(
               Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
               "emgumodel/haarcascade_frontalface_default.xml"
               //"emgumodel/haarcascade_frontalcatface.xml"
               ));

            _eyeLeftDetector = new CascadeClassifier(
                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emgumodel/haarcascade_lefteye_2splits.xml"));
            _eyeRightDetector = new CascadeClassifier(
                      Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emgumodel/haarcascade_righteye_2splits.xml"));

            _dnnNetCaffe = Emgu.CV.Dnn.DnnInvoke.ReadNetFromCaffe(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/deploy.prototxt.txt")
             , Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/res10_300x300_ssd_iter_140000.caffemodel")
             //, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/face_model.caffemodel")
             );
        }

        public List<KeyValuePair<Image<Bgr, byte>, Rectangle>> DetectByHaarCascade(Image<Bgr, byte> imgInput)
        {
            Image<Gray, Byte> faceInput = imgInput.Convert<Gray, byte>();

            faceInput._EqualizeHist();

            List<KeyValuePair<Image<Bgr, byte>, Rectangle>> faces = new List<KeyValuePair<Image<Bgr, byte>, Rectangle>>();

            //Detect the faces  from the gray scale image and store the locations as rectangle                   
            Rectangle[] facesDetected = _faceDetector.DetectMultiScale(faceInput, 1.05, 2, new Size(10, 10));

            foreach (var r in facesDetected)
            {
                faces.Add(new KeyValuePair<Image<Bgr, byte>, Rectangle>(imgInput.GetSubRect(r), r));
            }

            List<KeyValuePair<Image<Bgr, byte>, Rectangle>> founds = new List<KeyValuePair<Image<Bgr, byte>, Rectangle>>();


            foreach (var f in faces)
            {
                Rectangle[] eyeL = _eyeLeftDetector.DetectMultiScale(f.Key, 1.05, 2, new Size(10, 10));
                Rectangle[] eyeR = _eyeRightDetector.DetectMultiScale(f.Key, 1.05, 2, new Size(10, 10));
                if (eyeL.Length > 0 || eyeR.Length > 0)
                {
                    founds.Add(f);
                }
            }
            //if (founds.Count == 0) return faces;

            return founds;
        }

        public List<KeyValuePair<Image<Bgr, byte>, Rectangle>> DetectByDnnCaffe(Image<Bgr, Byte> imgInput, float confidenceThreshold = 0.15f)
        {

            //https://github.com/BVLC/caffe
            //https://github.com/m8/EmguCV-Caffe-Image-Classifier-EmguCV-Object-Detection-
            //https://raw.githubusercontent.com/opencv/opencv_extra/master/testdata/dnn/bvlc_googlenet.prototxt
            //https://github.com/BVLC/caffe/tree/master/models/bvlc_googlenet

            //https://github.com/emgucv/emgucv/issues/223

            Size size = new Size(300, 300);
            MCvScalar scalar = new MCvScalar(104, 117, 123);

            Mat blob = Emgu.CV.Dnn.DnnInvoke.BlobFromImage(imgInput.Mat, 0.85, size, scalar);

            //netCaffe.SetInput(blob, "data");
            _dnnNetCaffe.SetInput(blob);

            //Mat prob = netCaffe.Forward("detection_out");
            Mat prob = _dnnNetCaffe.Forward();
            //https://www.died.tw/2017/11/opencv-dnn-speed-compare-in-python-c-c.html

            //string[] Labels = { "background", "aeroplane", "bicycle", "bird", "boat", "bottle", "bus", "car", "cat", "chair", "cow", "diningtable", "dog", "horse", "motorbike", "person", "pottedplant", "sheep", "sofa", "train", "tvmonitor" };
            //MCvScalar[] Colors = new MCvScalar[21];
            //Random rnd = new Random();
            //for (int i = 0; i < 21; i++)
            //{
            //    Colors[i] = new Rgb(rnd.Next(0, 256), rnd.Next(0, 256), rnd.Next(0, 256)).MCvScalar;
            //}

            //string[] classNames = ReadClassNames();

            //// GetMaxClass(probBlob, out classId, out classProb);
            ////Mat matRef = probBlob.MatRef();
            //Mat probMat = prob.Reshape(1, 1); //reshape the blob to 1x1000 matrix
            //Point minLoc = new Point(), maxLoc = new Point();
            //double minVal = 0, maxVal = 0;
            //CvInvoke.MinMaxLoc(probMat, ref minVal, ref maxVal, ref minLoc, ref maxLoc);
            //var classId = maxLoc.X;

            //var xxx = "Best class: " + classNames[classId] + ". ClassId: " + classId + ". Probability: " + maxVal;

            //https://github.com/emgucv/emgucv/blob/master/Emgu.CV.Test/AutoTestVarious.cs
            //find face
            //float confidenceThreshold = 0.14f;

            List<KeyValuePair<Image<Bgr, byte>, Rectangle>> result = new List<KeyValuePair<Image<Bgr, byte>, Rectangle>>();

            int[] dim = prob.SizeOfDimension;
            int step = dim[3] * sizeof(float);
            IntPtr start = prob.DataPointer;
            for (int i = 0; i < dim[2]; i++)
            {
                float[] values = new float[dim[3]];
                Marshal.Copy(new IntPtr(start.ToInt64() + step * i), values, 0, dim[3]);
                float confident = values[2];

                if (confident > confidenceThreshold)
                {
                    float xLeftBottom = values[3] * imgInput.Cols;
                    float yLeftBottom = values[4] * imgInput.Rows;
                    float xRightTop = values[5] * imgInput.Cols;
                    float yRightTop = values[6] * imgInput.Rows;
                    RectangleF objectRegion = new RectangleF(xLeftBottom, yLeftBottom, xRightTop - xLeftBottom, yRightTop - yLeftBottom);
                    Rectangle faceRegion = Rectangle.Round(objectRegion);

                    result.Add(new KeyValuePair<Image<Bgr, byte>, Rectangle>(imgInput.GetSubRect(faceRegion), faceRegion));
                }
            }

            return result;
            //using (FacemarkLBFParams facemarkParam = new Emgu.CV.Face.FacemarkLBFParams())
            //using (FacemarkLBF facemark = new Emgu.CV.Face.FacemarkLBF(facemarkParam))
            //using (VectorOfRect vr = new VectorOfRect(faceRegions.ToArray()))
            //using (VectorOfVectorOfPointF landmarks = new VectorOfVectorOfPointF())
            //{
            //    facemark.LoadModel(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/lbfmodel.yaml"));
            //    facemark.Fit(_imgOrigin, vr, landmarks);

            //    foreach (Rectangle face in faceRegions)
            //    {
            //        CvInvoke.Rectangle(_imgOrigin, face, new MCvScalar(0, 255, 0));
            //    }

            //    int len = landmarks.Size;
            //    for (int i = 0; i < landmarks.Size; i++)
            //    {
            //        using (VectorOfPointF vpf = landmarks[i])
            //            FaceInvoke.DrawFacemarks(_imgOrigin, vpf, new MCvScalar(255, 0, 0));
            //    }

            //}

            //  CvInvoke.Imwrite("rgb_ssd_facedetect.jpg", _imgOrigin);

            //_imgOrigin.Save(_pathFileImgOrigin + $"detected.{_imgOriginFileExt}");
            //foreach (var f in faceRegions)
            //{
            //    CircleF circle = new CircleF();
            //    float x = (int)(f.X + (f.Width / 2));
            //    float y = (int)(f.Y + (f.Height / 2));
            //    circle.Radius = f.Width / 2;

            //    _imgOrigin.Draw(new CircleF(new PointF(x, y), circle.Radius), new Bgr(Color.Yellow), 2);

            //    _imgOrigin.Draw(f, new Bgr(Color.Red), 3);
            //}
            // _imgOrigin.Save(_pathFileImgOrigin + $"detected.{_imgOriginFileExt}");
        }

        #region test

        void BuildFileOrigin(string file)
        {
            _imgOrigin = new Image<Bgr, byte>(file);
            var fileInfo = new FileInfo(file);

            _imgOriginFileExt = fileInfo.Extension;
            _fileName = fileInfo.Name;
            _pathFileImgOrigin = file;
        }

        public void TestDnnYolo3(string file)
        {
            BuildFileOrigin(file);
            //     private static readonly Scalar[] Colors = Enumerable.Repeat(false, 80).Select(x => Scalar.RandomColor()).ToArray();

            //get labels from coco.names
            string[] Labels = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/coco.names.txt")).ToArray();

            const float threshold = 0.5f;       //for confidence 
            const float nmsThreshold = 0.3f;    //threshold for nms
            var model = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/coco.names.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/coco.names.txt"));

            //setting blob, size can be:320/416/608
            //opencv blob setting can check here https://github.com/opencv/opencv/tree/master/samples/dnn#object-detection
            Mat blob = Emgu.CV.Dnn.DnnInvoke.BlobFromImage(_imgOrigin.Mat, 0.85, new Size(416, 416), new MCvScalar(), true, false);

            var net = Emgu.CV.Dnn.DnnInvoke.ReadNetFromDarknet(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/yolov3.cfg"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dnnmodel/yolov3.weights"));

            net.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
            /*
          0:DNN_BACKEND_DEFAULT 
          1:DNN_BACKEND_HALIDE 
          2:DNN_BACKEND_INFERENCE_ENGINE
          3:DNN_BACKEND_OPENCV 
           */
            net.SetPreferableTarget(0);
            /*
            0:DNN_TARGET_CPU 
            1:DNN_TARGET_OPENCL
            2:DNN_TARGET_OPENCL_FP16
            3:DNN_TARGET_MYRIAD 
            4:DNN_TARGET_FPGA 
             */

            net.SetInput(blob);

            var prob = net.Forward(net.UnconnectedOutLayersNames[0]);

            float confidenceThreshold = 0.5f;

            List<Rectangle> faceRegions = new List<Rectangle>();

            int[] dim = prob.SizeOfDimension;
            int step = dim[3] * sizeof(float);
            IntPtr start = prob.DataPointer;
            for (int i = 0; i < dim[2]; i++)
            {
                float[] values = new float[dim[3]];
                Marshal.Copy(new IntPtr(start.ToInt64() + step * i), values, 0, dim[3]);
                float confident = values[2];

                if (confident > confidenceThreshold)
                {
                    float xLeftBottom = values[3] * _imgOrigin.Cols;
                    float yLeftBottom = values[4] * _imgOrigin.Rows;
                    float xRightTop = values[5] * _imgOrigin.Cols;
                    float yRightTop = values[6] * _imgOrigin.Rows;
                    RectangleF objectRegion = new RectangleF(xLeftBottom, yLeftBottom, xRightTop - xLeftBottom, yRightTop - yLeftBottom);
                    Rectangle faceRegion = Rectangle.Round(objectRegion);
                    faceRegions.Add(faceRegion);

                }
            }
            foreach (var f in faceRegions)
            {
                CircleF circle = new CircleF();
                float x = (int)(f.X + (f.Width / 2));
                float y = (int)(f.Y + (f.Height / 2));
                circle.Radius = f.Width / 2;

                _imgOrigin.Draw(new CircleF(new PointF(x, y), circle.Radius), new Bgr(Color.Yellow), 2);

                _imgOrigin.Draw(f, new Bgr(Color.Red), 3);
            }
            _imgOrigin.Save(_pathFileImgOrigin + $"detected.{_imgOriginFileExt}");
        }


        /// <summary>
        /// the lowest Distance will be more accurate
        /// </summary>
        /// <param name="fileFaceToCompare"></param>
        /// <returns></returns>
        public List<Result> CompareTo(string photo, params string[] fileFaceToCompare)
        {
            BuildFileOrigin(photo);

            //https://www.codeproject.com/Articles/261550/EMGU-Multiple-Face-Recognition-using-PCA-and-Paral

            //Eigen face recognizer
            //Parameters:	
            //      num_components – The number of components (read: Eigenfaces) kept for this Prinicpal 
            //          Component Analysis. As a hint: There’s no rule how many components (read: Eigenfaces) 
            //          should be kept for good reconstruction capabilities. It is based on your input data, 
            //          so experiment with the number. Keeping 80 components should almost always be sufficient.
            //
            //      threshold – The threshold applied in the prediciton. This still has issues as it work inversly to LBH and Fisher Methods.
            //          if you use 0.0 recognizer.Predict will always return -1 or unknown if you use 5000 for example unknow won't be reconised.
            //          As in previous versions I ignore the built in threhold methods and allow a match to be found i.e. double.PositiveInfinity
            //          and then use the eigen distance threshold that is return to elliminate unknowns. 
            //
            //NOTE: The following causes the confusion, sinc two rules are used. 
            //--------------------------------------------------------------------------------------------------------------------------------------
            //Eigen Uses
            //          0 - X = unknown
            //          > X = Recognised
            //
            //Fisher and LBPH Use
            //          0 - X = Recognised
            //          > X = Unknown
            //
            // Where X = Threshold value
            //var facesFromOrigin = DetectByDnnCaffe(_imgOrigin);
            var facesFromOrigin = DetectByHaarCascade(_imgOrigin);

            if (facesFromOrigin.Count == 0)
            {
                throw new Exception($"Not found any faces in original to compare: {_pathFileImgOrigin}");
            }

            List<Image<Bgr, byte>> listInputToTrain = new List<Image<Bgr, byte>>();

            foreach (var f in fileFaceToCompare)
            {
                var imgInput = new Image<Bgr, byte>(f);
                var facesToCompare = DetectByHaarCascade(imgInput);

                if (facesToCompare.Count == 1)
                {
                    var inputFace = imgInput.GetSubRect(facesToCompare[0].Value);
                    listInputToTrain.Add(inputFace);
                }
            }

            if (listInputToTrain.Count <= 0) throw new Exception("No face to compare, check your fileFaceToCompare");

            var maxWidth = listInputToTrain.Min(i => i.Width);
            var maxHeight = listInputToTrain.Min(i => i.Height);

            var minWOrigin = facesFromOrigin.Min(i => i.Key.Width);
            var minHOrigin = facesFromOrigin.Min(i => i.Key.Height);

            maxWidth = maxWidth > minWOrigin ? minWOrigin : maxWidth;
            maxHeight = maxHeight > minHOrigin ? minHOrigin : maxHeight;

            List<Image<Gray, byte>> listInputGrayToTrain = new List<Image<Gray, byte>>();

            foreach (var img in listInputToTrain)
            {
                var temp = img.Resize(maxWidth, maxHeight, Emgu.CV.CvEnum.Inter.Cubic)
                    .Convert<Gray, byte>();
                temp._EqualizeHist();

                listInputGrayToTrain.Add(temp);
            }

            Mat[] trainData = listInputGrayToTrain.Select(i => i.Mat.IsContinuous ? i.Mat : i.Mat.Clone()).ToArray();
            int[] trainLabel = listInputGrayToTrain.Select(i => 1).ToArray();

            FaceRecognizer eigenRecognizer = new EigenFaceRecognizer(80, double.PositiveInfinity);

            eigenRecognizer.Train(trainData, trainLabel);

            var trainDataFisher = trainData.ToList();
            trainDataFisher.AddRange(trainData);
            FaceRecognizer fisherRecognizer = new FisherFaceRecognizer(0, 3500);//4000
            fisherRecognizer.Train(trainDataFisher.ToArray(), trainDataFisher.Select(i => 1).ToArray());

            FaceRecognizer lbphRecognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 100);//50
            lbphRecognizer.Train(trainData, trainLabel);

            // listInputGrayToTrain[0].Save(_pathFileImgOrigin + ".inputTrainData." + _imgOriginFileExt);

            List<Result> resultFound = new List<Result>();

            List<FaceRecognizer.PredictionResult> predictR = new List<FaceRecognizer.PredictionResult>();

            List<FaceRecognizer.PredictionResult> allPredict = new List<FaceRecognizer.PredictionResult>();

            for (int i = 0; i < facesFromOrigin.Count; i++)
            {
                //Image<Bgr, byte> item = _imgOrigin.GetSubRect(facesFromOrigin[i].Value).Convert<Bgr, byte>();

                Image<Bgr, byte> item = facesFromOrigin[i].Key;

                var f = item.Resize(maxWidth, maxHeight, Emgu.CV.CvEnum.Inter.Cubic).Convert<Gray, byte>();

                f._EqualizeHist();

                // f.Save(_pathFileImgOrigin + $".needDetect{i}." + _imgOriginFileExt);

                var fm = f.Mat.IsContinuous ? f.Mat : f.Mat.Clone();

                var r0 = eigenRecognizer.Predict(fm);
                //var r1 = fisherRecognizer.Predict(fm);
                var r2 = lbphRecognizer.Predict(fm);
                allPredict.Add(r0);
                allPredict.Add(r2);
                if (r0.Label == 1 && r2.Label == 1)
                {
                    resultFound.Add(new Result
                    {
                        Face = facesFromOrigin[i].Key,
                        Position = facesFromOrigin[i].Value,
                        EigenResult = new FaceRecognizer.PredictionResult
                        {
                            Distance = r2.Distance,
                            Label = r0.Label
                        }
                    });
                    predictR.Add(r0);
                    predictR.Add(r2);
                }

            }

            return resultFound;
        }

        #endregion
    }
}
