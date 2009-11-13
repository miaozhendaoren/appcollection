﻿using System;
using System.Collections.Generic;
using System.IO;
using ImageProcess;
using RemoteImaging.Core;
using JSZN.Component;
using System.Drawing;
using OpenCvSharp;
using System.Threading;
using System.Diagnostics;
using System.Text;

namespace RemoteImaging.RealtimeDisplay
{
    public class Presenter : IImageScreenObserver
    {
        IImageScreen screen;
        ICamera camera;
        System.ComponentModel.BackgroundWorker worker;
        System.Timers.Timer timer = new System.Timers.Timer();
        System.Timers.Timer videoFileCheckTimer = new System.Timers.Timer();

        Queue<Frame[]> framesQueue = new Queue<Frame[]>();
        Queue<Frame> motionFrames = new Queue<Frame>();
        Queue<Frame> rawFrames = new Queue<Frame>();

        object locker = new object();
        object rawFrameLocker = new object();
        object bgLocker = new object();
        object camLocker = new object();

        AutoResetEvent goSearch = new AutoResetEvent(false);
        AutoResetEvent goDetectMotion = new AutoResetEvent(false);

        Thread motionDetectThread = null;


        private IplImage _BackGround;
        public IplImage BackGround
        {
            get
            {
                return _BackGround;
            }
            set
            {
                _BackGround = value;
                value.IsEnabledDispose = false;
            }
        }

        public void UpdateBG()
        {
            byte[] imgData = null;
            lock (this.camLocker)
                imgData = this.camera.CaptureImageBytes();

            Image img = Image.FromStream(new MemoryStream(imgData));

            lock (this.bgLocker)
                this.BackGround = BitmapConverter.ToIplImage((Bitmap)img);

            img.Save("BG.jpg");
        }



        /// <summary>
        /// Initializes a new instance of the Presenter class.
        /// </summary>
        /// <param name="screen"></param>
        /// <param name="uploadWatcher"></param>
        public Presenter(IImageScreen screen,
            ICamera camera)
        {
            this.screen = screen;
            this.camera = camera;

            motionDetectThread =
                Properties.Settings.Default.DetectMotion ?
                new Thread(this.DetectMotion) : new Thread(this.BypassDetectMotion);
            motionDetectThread.IsBackground = true;
            motionDetectThread.Name = "motion detect";


            this.screen.Observer = this;
            this.worker = new System.ComponentModel.BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.DoWork += worker_DoWork;

            this.timer.Interval = 1000 / int.Parse(Properties.Settings.Default.FPs);
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);

            videoFileCheckTimer.Interval = 1000 * 60;
            videoFileCheckTimer.Elapsed += new System.Timers.ElapsedEventHandler(videoFileCheckTimer_Elapsed);

            if (File.Exists("bg.jpg"))
                BackGround = OpenCvSharp.IplImage.FromFile(@"bg.jpg");
        }

        private static void DeleteVideoFileAt(DateTime time)
        {
            string m4vFile = FileSystemStorage.VideoFilePathNameAt(time, 2);
            if (File.Exists(m4vFile))
            {
                System.Diagnostics.Debug.WriteLine(m4vFile);
                File.Delete(m4vFile);
            }

            string idvFile = m4vFile.Replace(".m4v", ".idv");
            if (File.Exists(idvFile))
            {
                System.Diagnostics.Debug.WriteLine(idvFile);
                File.Delete(idvFile);
            }
        }


        void videoFileCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime time = DateTime.Now.AddMinutes(-2);

            if (!FileSystemStorage.FacesCapturedAt(2, time))
                DeleteVideoFileAt(time);


        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.QueryRawFrame();
        }


        void worker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            this.SearchFace();

        }

        void worker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            ImageDetail[] iconImgs = e.Result as ImageDetail[];

            if (iconImgs.Length > 0)
            {
                if (screen.SelectedCamera.ID == -1
                || screen.SelectedCamera.ID == iconImgs[0].FromCamera)
                {
                    screen.ShowImages(iconImgs);
                }
            }


            if (framesQueue.Count > 0)
            {
                screen.ShowProgress = true;
                worker.RunWorkerAsync(framesQueue.Dequeue());
            }
            else
            {
                screen.ShowProgress = false;
            }
        }


        private void QueryRawFrame()
        {
            byte[] image;

            lock (this.camLocker)
            {
                image = camera.CaptureImageBytes();
            }


            MemoryStream memStream = new MemoryStream(image);
            Bitmap bmp = null;
            try
            {
                bmp = (Bitmap)Image.FromStream(memStream);
            }
            catch (System.ArgumentException)//图片格式出错
            {
                return;
            }

            Frame f = new Frame();
            f.timeStamp = DateTime.Now.Ticks;
            f.cameraID = 2;

            IplImage ipl = BitmapConverter.ToIplImage(bmp);
            ipl.IsEnabledDispose = false;
            f.image = ipl;

            System.Diagnostics.Debug.Assert(f.image.CvPtr != IntPtr.Zero);


            lock (this.rawFrameLocker) rawFrames.Enqueue(f);

            goDetectMotion.Set();
        }


        private Frame GetNewFrame()
        {
            Frame newFrame = null;

            lock (this.rawFrameLocker)
            {
                if (rawFrames.Count > 0)
                {
                    newFrame = rawFrames.Dequeue();
                }
            }
            return newFrame;
        }


        private static bool IsStaticFrame(Frame aFrame)
        {
            return aFrame.image == null ||
                (aFrame.searchRect.Width == 0 || aFrame.searchRect.Height == 0);
        }

        private void DetectMotion()
        {
            int count = 0;
            while (true)
            {
                Frame nextFrame = GetNewFrame();
                if (nextFrame != null)
                {
                    Frame lastFrame = new Frame();

                    bool groupCaptured = Program.motionDetector.DetectFrame(nextFrame, lastFrame);


                    if (IsStaticFrame(lastFrame))
                    {
                        if (lastFrame.image != null)
                        {
                            lastFrame.image.IsEnabledDispose = true;
                            lastFrame.image.Dispose();
                        }

                    }
                    else
                    {
                        FileSystemStorage.SaveFrame(lastFrame);
                        motionFrames.Enqueue(lastFrame);
                    }

                    if (groupCaptured)
                    {
                        Frame[] frames = motionFrames.ToArray();
                        motionFrames.Clear();

                        if (frames.Length <= 0) continue;

                        lock (locker) framesQueue.Enqueue(frames);

                        goSearch.Set();

                    }

                }
                else
                    goDetectMotion.WaitOne();

            }
        }

        void BypassDetectMotion()
        {
            while (true)
            {
                Frame f = this.GetNewFrame();

                if (f.image != null)
                {
                    IplImage ipl = f.image;
                    ipl.IsEnabledDispose = false;
                    f.searchRect.Width = ipl.Width;
                    f.searchRect.Height = ipl.Height;

                    motionFrames.Enqueue(f);
                    FileSystemStorage.SaveFrame(f);

                    if (motionFrames.Count == 6)
                    {
                        Frame[] frames = motionFrames.ToArray();
                        motionFrames.Clear();
                        lock (locker) framesQueue.Enqueue(frames);
                        goSearch.Set();
                    }
                }
                else
                    goDetectMotion.WaitOne();

            }

        }

        public void Start()
        {

            this.timer.Enabled = false;
            this.timer.Enabled = true;

            videoFileCheckTimer.Enabled = true;


            if (!motionDetectThread.IsAlive)
            {
                motionDetectThread.Start();
            }

            if (!this.worker.IsBusy)
            {
                this.worker.RunWorkerAsync();
            }
        }

        private string PrepareDestFolder(ImageDetail imgToProcess)
        {
            string parentOfBigPicFolder = Directory.GetParent(imgToProcess.ContainedBy).FullName;
            string destFolder = Path.Combine(parentOfBigPicFolder, Properties.Settings.Default.IconDirectoryName);
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            return destFolder;
        }


        ImageDetail[] SaveImage(Target[] targets)
        {
            IList<ImageDetail> imgs = new List<ImageDetail>();

            foreach (Target t in targets)
            {
                Frame frame = t.BaseFrame;

                DateTime dt = DateTime.FromBinary(frame.timeStamp);

                for (int j = 0; j < t.Faces.Length; ++j)
                {
                    string facePath = FileSystemStorage.GetFacePath(frame, dt, j);
                    t.Faces[j].SaveImage(facePath);
                    imgs.Add(ImageDetail.FromPath(facePath));
                }

            }

            ImageDetail[] details = new ImageDetail[imgs.Count];
            imgs.CopyTo(details, 0);

            return details;

        }


        void SearchFace()
        {
            while (true)
            {
                try
                {
                    Frame[] frames = null;
                    lock (locker)
                    {
                        if (framesQueue.Count > 0)
                        {
                            frames = framesQueue.Dequeue();
                        }
                    }

                    if (frames != null && frames.Length > 0)
                    {
                        foreach (var f in frames)
                        {
                            Program.faceSearch.AddInFrame(f);
                        }

                        IntPtr target = IntPtr.Zero;

                        ImageProcess.Target[] targets = Program.faceSearch.SearchFaces();

                        ImageDetail[] imgs = this.SaveImage(targets);
                        this.screen.ShowImages(imgs);


                        Array.ForEach(frames, f => { IntPtr cvPtr = f.image.CvPtr; OpenCvSharp.Cv.Release(ref cvPtr); f.image.Dispose(); });
                        Array.ForEach(targets, t =>
                        {
                            Array.ForEach(t.Faces, ipl => ipl.Dispose());
                            t.BaseFrame.image.Dispose();
                        });
                    }
                    else
                        goSearch.WaitOne();
                }

                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("exception");
                }


                //                    foreach (var t in targets)
                //                     {
                //                         foreach (var face in t.Faces)
                //                         {
                //                             IntPtr normalizeFace = IntPtr.Zero;
                // 
                //                             NativeIconExtractor.NormalizeFace(
                //                                 t.BaseFrame.image,
                //                                 ref normalizeFace,
                //                                 face.OriginalFace,
                //                                 0);
                // 
                //                             IplImage normalIpl = new IplImage(normalizeFace);
                //                             normalIpl.IsEnabledDispose = false;
                // 
                //                             float[] imgData = NativeIconExtractor.ResizeIplTo(normalIpl, 20, 20, BitDepth.U8, 1);
                // 
                //                             FaceRecognition.RecognizeResult[] results = new
                //                                  FaceRecognition.RecognizeResult[Program.ImageSampleCount];
                // 
                // 
                //                             fixed (float* pImageData = &imgData[0])
                //                             {
                //                                 FaceRecognition.FaceRecognizer.Recognize(
                //                                                                     imgData,
                //                                                                     Program.ImageSampleCount,
                //                                                                     results,
                //                                                                     Program.ImageLen, Program.EigenNum);
                //                             }
                // 
                // 
                //                             FaceRecognition.RecognizeResult maxSim
                //                                 = new FaceRecognition.RecognizeResult();
                // 
                //                             Array.ForEach(results, r => { if (r.similarity > maxSim.similarity)  maxSim = r; });
                // 
                //                             if (maxSim.similarity > 0.7)
                //                             {
                //                                 Bitmap capturedFace = face.Img.ToBitmap();
                // 
                //                                 System.Diagnostics.Debug.Assert(maxSim.fileName != null);
                // 
                //                                 string fName = maxSim.fileName;
                // 
                //                                 int idx = fName.IndexOf('_');
                // 
                //                                 string path = @"C:\faceRecognition\selectedFace\" + fName.Remove(idx, 5);
                // 
                //                                 System.Diagnostics.Debug.Assert(System.IO.File.Exists(path));
                // 
                //                                 Bitmap faceInLib = (Bitmap)Bitmap.FromFile(path);
                //                                 screen.ShowFaceRecognitionResult(capturedFace, faceInLib, maxSim.similarity);
                //                             }
                // 
                //                         }
                //                     }




            }
        }


        #region IImageScreenObserver Members

        public void SelectedCameraChanged()
        {
            throw new NotImplementedException();
        }



        public void SelectedImageChanged()
        {
            ImageDetail img = this.screen.SelectedImage;
            if (img != null && !string.IsNullOrEmpty(img.Path))
            {
                string bigPicPathName = FileSystemStorage.BigImgPathFor(img);
                ImageDetail bigImageDetail = ImageDetail.FromPath(bigPicPathName);
                this.screen.BigImage = bigImageDetail;

            }
        }

        #endregion
    }
}
