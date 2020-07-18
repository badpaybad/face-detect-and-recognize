# face-detect-and-recognize
face detect and recognize with emgucv dnn caffe usage check in Program.cs
- 1st: Have to detect which is faces in photo
----- 2nd: Crop faces and align face
- 3rd: Detect faces in training data ( eg: face in Identity card, faces from selfi, from avatar facebook) 
----- make sure train data just only face to indentify
- 4th: train with faces in 3rd
----- recognize step compare faces were crop in 2nd

# FaceRecognitionCombination

### Train and save your model file
        // listFaceToTrainHere obj {Face, Label}
        rekognitionCombind.Train( listFaceToTrainHere , out string eigenFileModel, out string lbphFileModel);

### Predict your photo ( will extract all face in photo to predict )
        var predictResult = rekognitionCombind.Predict(photo);

# detection

            Image<Bgr, byte> imgInput3jpg = new Image<Bgr, byte>(file3jpg);
            
            var detectedFace = new FaceDetection()
                .DetectByDnnCaffe(imgInput3jpg)
                //.DetectByHaarCascade(imgInput)
                ;

# Recognize
              var recognizedFace = new FaceRecognition().AddTrainData(duFace)
                .WithThreshold(0,100)
                .Train().RecognizePhoto(imgInput3jpg1);
                
# ffmpeg 
convert from image (jpg, png, gif) to video, transition 2 video

               var ffmpegConvertToVid = new FfmpegCommandBuilder()
                .WithInputFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.gif"), 5)
                .WithOutput(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sampledata/intro.mp4"))
                .WithOutDuration(5)
                .ToCommand().Run();

 