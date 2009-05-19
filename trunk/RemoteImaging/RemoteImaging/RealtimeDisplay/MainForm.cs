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

namespace RemoteImaging.RealtimeDisplay
{
    public partial class MainForm : Form, IImageScreen
    {
        public MainForm()
        {
            InitializeComponent();

            for (int i = 0; i < 5; i++)
            {
                this.squareNumber.Items.Add(i + 1);
            }

            this.squareNumber.SelectedItem = Properties.Settings.Default.ColumnNumber;
            Camera[] cams = new Camera[Configuration.Instance.Cameras.Count];
            Configuration.Instance.Cameras.CopyTo(cams, 0);
            this.Cameras = cams;

            Properties.Settings setting = Properties.Settings.Default;

            float left = float.Parse(setting.IconLeftExtRatio);
            float top = float.Parse(setting.IconTopExtRatio);
            float right = float.Parse(setting.IconRightExtRatio);
            float bottom = float.Parse(setting.IconBottomExtRatio);

            int minFaceWidth = int.Parse(setting.MinFaceWidth);
            int maxFaceWidth = int.Parse(setting.MaxFaceWidth);

            float ratio = (float)maxFaceWidth / minFaceWidth;

            SetupExtractor(left, right, top, bottom, minFaceWidth, ratio);

        }

        private Camera getSelCamera()
        {
            if (this.cameraComboBox.ComboBox.SelectedItem != null)
            {
                Camera cam = this.cameraComboBox.ComboBox.SelectedItem as Camera;
                return cam;

            }

            return null;
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

        private void showPicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] files
               = System.IO.Directory.GetFiles(@"d:\20090505");

            ImageCell[] cells = new ImageCell[100];
            for (int i = 0; i < cells.Length; i++)
            {
                Image img = Image.FromFile(files[i]);
                Graphics g = Graphics.FromImage(img);
                string text = DateTime.Now.ToShortTimeString() + ":" + i.ToString();
                g.DrawString(text, SystemFonts.CaptionFont, Brushes.Black, 0, 0);
                ImageCell newCell = new ImageCell() { Image = img, Path = "", Text = text, Tag = null };
                cells[i] = newCell;
            }

            this.squareListView1.ShowImages(cells);
        }


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
                this.cameraComboBox.ComboBox.DataSource = value;
                this.cameraComboBox.ComboBox.DisplayMember = "Name";
                this.cameraComboBox.SelectedIndex = 0;
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

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Camera[] cams = new Camera[] {
                new Camera() { Name = "所有", ID = -1, IpAddress = "192.168.1.1" },
                new Camera() { Name = "南门", ID = 1, IpAddress = "192.168.1.1" },
                new Camera() { Name = "北门", ID = 2, IpAddress = "192.168.1.1" },
                new Camera() { Name = "西门", ID = 3, IpAddress = "192.168.1.1" },
            };
            this.Cameras = cams;
        }

        private void squareNumber_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.squareNumber.Text))
            {
                return;
            }

            int n = 0;

            if (int.TryParse(this.squareNumber.Text, out n))
            {
                if (n < 1)
                {
                    MessageBox.Show("数字应该 > 0");
                    return;
                }

                if (n == this.squareListView1.NumberOfColumns)
                {
                    return;
                }

                this.squareListView1.NumberOfColumns = n;
            }
            else
            {
                MessageBox.Show("无效输入, 应该输入数字, 且数字 >= 1");
            }
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            new RemoteImaging.Query.PicQueryForm().ShowDialog(this);
        }

        private static void SetupExtractor(float leftRatio,
            float rightRatio,
            float topRatio,
            float bottomRatio,
            int minFaceWidth,
            float maxFaceWidthRatio)
        {
            IconExtractor.Default.SetExRatio(topRatio,
                                    bottomRatio,
                                    leftRatio,
                                    rightRatio);

            IconExtractor.Default.SetFaceParas(minFaceWidth, maxFaceWidthRatio);
        }

        private void optionsButton_Click(object sender, EventArgs e)
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
                    Configuration.Instance.Cameras = frm.Cameras;
                    Configuration.Instance.Save();

                    Properties.Settings.Default.Save();

                    this.Cameras = frm.Cameras.ToArray<Camera>();

                    

                    float ratio = (float)frm.MaxFaceWidth / frm.MinFaceWidth;

                    SetupExtractor(frm.LeftExtRatio, frm.RightExtRatio, frm.TopExtRatio, frm.BottomExtRatio, frm.MinFaceWidth, ratio);
                }
            }
        }

        private void squareNumber_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.squareListView1.NumberOfColumns = (int)this.squareNumber.SelectedItem;
        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {
            new RemoteImaging.Query.VideoQueryForm().ShowDialog(this);
        }



    }
}
