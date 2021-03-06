﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MyControls;
using System.IO;
using DevExpress.XtraNavBar;
using ImageProcess;
using System.Runtime.InteropServices;
using System.Diagnostics;
using RemoteImaging.Core;
using Microsoft.Win32;

namespace RemoteImaging.RealtimeDisplay
{
    public partial class MainForm : Form, IImageScreen
    {
        public MainForm()
        {
            InitializeComponent();


            cpuCounter = new PerformanceCounter();
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";

            Camera[] cams = new Camera[Configuration.Instance.Cameras.Count];
            Configuration.Instance.Cameras.CopyTo(cams, 0);
            this.Cameras = cams;

            Properties.Settings setting = Properties.Settings.Default;

            InitStatusBar();
            float left = float.Parse(setting.IconLeftExtRatio);
            float top = float.Parse(setting.IconTopExtRatio);
            float right = float.Parse(setting.IconRightExtRatio);
            float bottom = float.Parse(setting.IconBottomExtRatio);

            int minFaceWidth = int.Parse(setting.MinFaceWidth);
            int maxFaceWidth = int.Parse(setting.MaxFaceWidth);

            float ratio = (float)maxFaceWidth / minFaceWidth;

            SetupExtractor(setting.EnvMode,
                left,
                right,
                top,
                bottom,
                minFaceWidth,
                ratio,
                new Rectangle(int.Parse(setting.SrchRegionLeft),
                    int.Parse(setting.SrchRegionTop),
                    int.Parse(setting.SrchRegionWidth),
                    int.Parse(setting.SrchRegionHeight))
                    );

            try
            {
                videoPlayerPath = (string)Registry.LocalMachine.OpenSubKey("Software")
                                .OpenSubKey("Videolan")
                                .OpenSubKey("vlc").GetValue(null);
            }
            catch (Exception)
            {
                videoPlayerPath = null;
            }


        }

        Camera allCamera = new Camera() { ID = -1 };

        private TreeNode getTopCamera(TreeNode node)
        {
            while (node.Tag == null || !(node.Tag is Camera))
            {
                node = node.Parent;
            }
            return node;
        }

        private Camera getSelCamera()
        {
            if (this.cameraTree.SelectedNode == null
                || this.cameraTree.SelectedNode.Level == 0)
            {
                return allCamera;
            }

            TreeNode nd = getTopCamera(this.cameraTree.SelectedNode);
            return nd.Tag as Camera;
        }


        #region IImageScreen Members

        public Camera SelectedCamera
        {
            get
            {
                if (this.InvokeRequired)
                {
                    System.Func<Camera> func = this.getSelCamera;
                    return this.Invoke(func) as Camera;
                }
                else
                {
                    return getSelCamera();
                }

            }

        }

        public ImageDetail SelectedImage
        {
            get
            {
                ImageDetail img = null;
                if (this.squareListView1.LastSelectedCell != null)
                {
                    Cell c = this.squareListView1.LastSelectedCell;
                    if (!string.IsNullOrEmpty(c.Path))
                    {
                        img = ImageDetail.FromPath(c.Path);
                    }

                }

                return img;
            }

        }

        public ImageDetail BigImage
        {
            set
            {
                Image img = Image.FromFile(value.Path);
                this.pictureEdit1.Image = img;
                this.pictureEdit1.Tag = value;
            }
        }

        public IImageScreenObserver Observer { get; set; }

        public void ShowImages(ImageDetail[] images)
        {
            ImageCell[] cells = new ImageCell[images.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                Image img = Image.FromFile(images[i].Path);
                string text = images[i].CaptureTime.ToString();
                ImageCell newCell = new ImageCell() { Image = img, Path = images[i].Path, Text = text, Tag = null };
                cells[i] = newCell;
            }

            this.squareListView1.ShowImages(cells);
        }

        #endregion


        private void MainForm_Shown(object sender, EventArgs e)
        {
            IIconExtractor extractor = IconExtractor.Default;

            ImageUploadWatcher watcher =
                new ImageUploadWatcher() { PathToWatch = Properties.Settings.Default.ImageUploadPool, };

            Presenter p = new Presenter(this, watcher, extractor);
            p.Start();
        }

        #region IImageScreen Members

        public Camera[] Cameras
        {
            set
            {
                this.cameraTree.Nodes.Clear();

                TreeNode rootNode = new TreeNode()
                {
                    Text = "所有摄像头",
                    ImageIndex = 0,
                    SelectedImageIndex = 0
                };

                Array.ForEach(value, camera =>
                {
                    TreeNode camNode = new TreeNode()
                    {
                        Text = camera.Name,
                        ImageIndex = 1,
                        SelectedImageIndex = 1,
                        Tag = camera,
                    };

                    Action<string> setupCamera = (ip) =>
                    {
                        using (FormConfigCamera form = new FormConfigCamera())
                        {
                            StringBuilder sb = new StringBuilder(form.Text);
                            sb.Append("-[");
                            sb.Append(ip);
                            sb.Append("]");

                            form.Navigate(ip);
                            form.Text = sb.ToString();
                            form.ShowDialog(this);
                        }
                    };

                    TreeNode setupNode = new TreeNode()
                    {
                        Text = "设置",
                        ImageIndex = 2,
                        SelectedImageIndex = 2,
                        Tag = setupCamera,
                    };
                    TreeNode propertyNode = new TreeNode()
                    {
                        Text = "属性",
                        ImageIndex = 3,
                        SelectedImageIndex = 3,
                    };
                    TreeNode ipNode = new TreeNode()
                    {
                        Text = "IP地址:" + camera.IpAddress,
                        ImageIndex = 4,
                        SelectedImageIndex = 4
                    };
                    TreeNode idNode = new TreeNode()
                    {
                        Text = "编号:" + camera.ID.ToString(),
                        ImageIndex = 5,
                        SelectedImageIndex = 5
                    };


                    propertyNode.Nodes.AddRange(new TreeNode[] { ipNode, idNode });
                    camNode.Nodes.AddRange(new TreeNode[] { setupNode, propertyNode });
                    rootNode.Nodes.Add(camNode);

                });

                this.cameraTree.Nodes.Add(rootNode);

                this.cameraTree.ExpandAll();
            }
        }

        #endregion


        private void squareListView1_SelectedCellChanged(object sender, EventArgs e)
        {
            if (this.Observer != null)
            {
                this.Observer.SelectedImageChanged();
            }
        }


        private void simpleButton3_Click(object sender, EventArgs e)
        {
            new RemoteImaging.Query.PicQueryForm().ShowDialog(this);
        }

        private static void SetupExtractor(int envMode, float leftRatio,
            float rightRatio,
            float topRatio,
            float bottomRatio,
            int minFaceWidth,
            float maxFaceWidthRatio,
            Rectangle SearchRectangle)
        {
            IconExtractor.Default.SetExRatio(topRatio,
                                    bottomRatio,
                                    leftRatio,
                                    rightRatio);

            IconExtractor.Default.SetROI(SearchRectangle.Left,
                SearchRectangle.Top,
                SearchRectangle.Width - 1,
                SearchRectangle.Height - 1);

            IconExtractor.Default.SetFaceParas(minFaceWidth, maxFaceWidthRatio);

            IconExtractor.Default.SetLightMode(envMode);
        }


        private void simpleButton4_Click(object sender, EventArgs e)
        {

        }


        private void searchPic_Click(object sender, EventArgs e)
        {
            new RemoteImaging.Query.PicQueryForm().ShowDialog(this);
        }


        /// Return Type: BOOL->int
        ///hWnd: HWND->HWND__*
        [DllImport("user32.dll", EntryPoint = "BringWindowToTop")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop([In()] IntPtr hWnd);

        /// Return Type: BOOL->int
        ///hWnd: HWND->HWND__*
        ///nCmdShow: int
        [DllImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow([In()] IntPtr hWnd, int nCmdShow);

        System.Diagnostics.Process videoDnTool;

        private void dnloadVideo_Click(object sender, EventArgs e)
        {
            if (videoDnTool != null && !videoDnTool.HasExited)
            {
                //restore window and bring it to top
                ShowWindow(videoDnTool.MainWindowHandle, 9);
                BringWindowToTop(videoDnTool.MainWindowHandle);
                return;
            }

            videoDnTool = System.Diagnostics.Process.Start(Properties.Settings.Default.VideoDnTool);
            videoDnTool.EnableRaisingEvents = true;
            videoDnTool.Exited += videoDnTool_Exited;

        }

        void videoDnTool_Exited(object sender, EventArgs e)
        {
            videoDnTool = null;
        }

        private void videoSearch_Click(object sender, EventArgs e)
        {
            new RemoteImaging.Query.VideoQueryForm().ShowDialog(this);
        }

        private void options_Click(object sender, EventArgs e)
        {
            using (OptionsForm frm = new OptionsForm())
            {
                IList<Camera> camCopy = new List<Camera>();

                foreach (Camera item in Configuration.Instance.Cameras)
                {
                    camCopy.Add(new Camera() { ID = item.ID, Name = item.Name, IpAddress = item.IpAddress });
                }


                frm.Cameras = camCopy;
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    Properties.Settings setting = Properties.Settings.Default;

                    Configuration.Instance.Cameras = frm.Cameras;
                    Configuration.Instance.Save();

                    setting.Save();

                    InitStatusBar();

                    this.Cameras = frm.Cameras.ToArray<Camera>();

                    var minFaceWidth = int.Parse(setting.MinFaceWidth);
                    float ratio = float.Parse(setting.MaxFaceWidth) / minFaceWidth;

                    SetupExtractor(setting.EnvMode,
                        float.Parse(setting.IconLeftExtRatio),
                        float.Parse(setting.IconRightExtRatio),
                        float.Parse(setting.IconTopExtRatio),
                        float.Parse(setting.IconBottomExtRatio),
                        minFaceWidth,
                        ratio,
                        new Rectangle(int.Parse(setting.SrchRegionLeft),
                                        int.Parse(setting.SrchRegionTop),
                                        int.Parse(setting.SrchRegionWidth),
                                        int.Parse(setting.SrchRegionHeight))
                                   );
                }
            }

        }

        private void column1by1_Click(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = 1;

        }

        private void column2by2_Click(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = 2;
        }

        private void column3by3_Click(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = 3;
        }

        private void column4by4_Click(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = 4;
        }

        private void column5by5_Click(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = 5;
        }

        private void InitStatusBar()
        {
            statusUploadFolder.Text = "上传目录：" + Properties.Settings.Default.ImageUploadPool;
            statusOutputFolder.Text = "输出目录：" + Properties.Settings.Default.OutputPath;
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
            about.Dispose();
        }

        private void realTimer_Tick(object sender, EventArgs e)
        {
            string statusTxt = string.Format("CPU占用率: {0}, 可用内存: {1}",
                this.getCurrentCpuUsage(), this.getAvailableRAM());

            this.statusCPUMemUsage.Text = statusTxt;

            statusTime.Text = DateTime.Now.ToString();
            this.StepProgress();
        }

        private void statusOutputFolder_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(@"explorer.exe",
                Properties.Settings.Default.OutputPath);
        }

        private void statusUploadFolder_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(@"explorer.exe",
               Properties.Settings.Default.ImageUploadPool);
        }

        private void cameraTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            Action<string> setupCamera = e.Node.Tag as Action<string>;
            if (setupCamera != null)
            {
                Camera cam = this.getTopCamera(e.Node).Tag as Camera;
                setupCamera(cam.IpAddress);

            }

        }





        #region IImageScreen Members


        public bool ShowProgress
        {
            set
            {
                if (this.InvokeRequired)
                {
                    Action ac = () => this.statusProgressBar.Visible = value;
                    //this.Invoke(ac);
                }
                else
                {
                    //this.statusProgressBar.Visible = value;
                }

            }
        }

        public void StepProgress()
        {
            if (InvokeRequired)
            {
                Action ac = () => this.statusProgressBar.PerformStep();

                this.Invoke(ac);
            }
            else
            {
                this.statusProgressBar.PerformStep();

            }

        }

        #endregion

        PerformanceCounter cpuCounter;
        PerformanceCounter ramCounter;

        private string getCurrentCpuUsage()
        {
            return String.Format("{0:F0}%", cpuCounter.NextValue());
        }

        private string getAvailableRAM()
        {
            return String.Format("{0}MB", ramCounter.NextValue());
        }

        private void ShowDetailPic(ImageDetail img)
        {
            FormDetailedPic detail = new FormDetailedPic();
            detail.Img = img;
            detail.ShowDialog(this);
            detail.Dispose();
        }

        private void pictureEdit1_DoubleClick(object sender, EventArgs e)
        {
            if (this.pictureEdit1.Tag == null)
            {
                return;
            }

            ImageDetail img = this.pictureEdit1.Tag as ImageDetail;

            ShowDetailPic(img);

        }

        private void ShowPic()
        {
            if (this.squareListView1.SelectedCell == null)
                return;
            string p = this.squareListView1.SelectedCell.Path;
            if (p == null) return;

            this.ShowDetailPic(ImageDetail.FromPath(p));
        }
        private void squareListView1_CellDoubleClick(object sender, CellDoubleClickEventArgs args)
        {
            ShowPic();
        }

        private void playRelateVideo_Click(object sender, EventArgs e)
        {
            Cell c = this.squareListView1.SelectedCell;
            if (c == null || c.Path == null) return;

            ImageDetail imgInfo = ImageDetail.FromPath(c.Path);

            string root = Path.Combine(Properties.Settings.Default.OutputPath, imgInfo.FromCamera.ToString("D2"));

            string[] videos = VideoSearch.FindVideos(root, imgInfo);

            if (videos.Length == 0)
            {
                MessageBox.Show(this, "没有找到相关视频", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (videoPlayerPath == null)
            {
                MessageBox.Show(this, "请安装相应播放器", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var file in videos)
            {
                sb.Append(file); sb.Append(' ');
            }

            sb.Append(@"vlc://quit"); sb.Append(' ');

            Process.Start(videoPlayerPath, sb.ToString());
        }


        string videoPlayerPath;

        private void StartRecord(Camera cam)
        {
            this.axCamImgCtrl1.CamImgCtrlStop();

            this.axCamImgCtrl1.ImageFileURL = @"liveimg.cgi";
            this.axCamImgCtrl1.ImageType = @"MPEG";
            this.axCamImgCtrl1.CameraModel = 1;
            this.axCamImgCtrl1.CtlLocation = @"http://" + cam.IpAddress;
            this.axCamImgCtrl1.uid = "admin";
            this.axCamImgCtrl1.pwd = "admin";
            this.axCamImgCtrl1.RecordingFolderPath
                = Path.Combine(Properties.Settings.Default.OutputPath, cam.ID.ToString("D2"));
            this.axCamImgCtrl1.RecordingFormat = 0;
            this.axCamImgCtrl1.UniIP = this.axCamImgCtrl1.CtlLocation;
            this.axCamImgCtrl1.UnicastPort = 3939;
            this.axCamImgCtrl1.ComType = 0;

            this.axCamImgCtrl1.CamImgCtrlStart();
            this.axCamImgCtrl1.CamImgRecStart();
        }

        private void cameraTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {

            TreeNode cameraNode = this.getTopCamera(e.Node);
            if (!(cameraNode.Tag is Camera)) return;


            Camera cam = cameraNode.Tag as Camera;

            StartRecord(cam);

        }

        private void axCamImgCtrl1_InfoChanged(object sender, AxIMGCTRLLib._ICamImgCtrlEvents_InfoChangedEvent e)
        {

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {

        }

        private void enhanceImg_Click(object sender, EventArgs e)
        {
            this.ShowPic();
        }

        private void panelControl1_SizeChanged(object sender, EventArgs e)
        {
            int height = this.panelControl1.Height - this.axCamImgCtrl1.Height;
            int x = (this.panelControl1.Width - this.axCamImgCtrl1.Width) / 2;
            this.axCamImgCtrl1.Left = x;
            this.squareListView1.Height = height - 15;
        }


    }
}
