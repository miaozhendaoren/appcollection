﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Damany.Imaging.Common;
using Damany.Imaging.PlugIns;
using RemoteImaging.Query;
using FaceRecognition;

namespace RemoteImaging.ImportPersonCompare
{
    public partial class ImmediatelyModel : DevExpress.XtraEditors.XtraForm
    {
        public ImmediatelyModel()
        {
            InitializeComponent();
            btnOK.Enabled = false;
        }

        public void AddSuspects(PersonOfInterestDetectionResult compareResult)
        {

            this.listPersons.Add(compareResult);

            var lvi = new ListViewItem(new string[] { "",
                                                            //compareResult.Details.Name,
                                                            //compareResult.Details.Gender.ToString(),
                                                            //compareResult.Details.Age.ToString(),
                                                            //compareResult.Details.ID,
                                                            string.Empty});
            lvi.Tag = compareResult;
            var added = this.suspectsList.Items.Add(lvi);

            suspectsList.SelectedItems.Clear();
            suspectsList.EnsureVisible(this.suspectsList.Items.Count - 1);

            this.suspectsList.Items[this.suspectsList.Items.Count - 1].Selected = true;

            if (!Visible)
            {
                this.Show(Application.OpenForms[0]);
            }

        }


        public ImageDirSys SetWarnInfo
        {
            set
            {
                if (value != null)
                {
                    lblDate.Text = string.Format("日期： {0}-{1}-{2}", value.Year, value.Month, value.Day);
                    lblTime.Text = string.Format("时间： {0}:{1}:{2}", value.Hour, value.Minute, value.Second);
                    lblAddress.Text = string.Format("地址： {0}", value.CameraID.ToString());
                }
            }
        }

        private Image picCheckImg = null;
        public Image PicCheckImg
        {
            private get { return picCheckImg; }
            set { suspectImage.Image = (Image)value; }
        }



        private void btnOK_Click(object sender, EventArgs e)
        {
            //将比对好的图片 另外存入一个文件夹中
            if (suspectsList.FocusedItem == null)
            {
                MessageBox.Show("请选择报警信息！", "警告");
                return;
            }
            bool res = false;
            if (MessageBox.Show("确定要保存当前选择的报警信息？", "提示", MessageBoxButtons.OKCancel) == DialogResult.OK)
                res = true;
            else
                return;

            if (res)
                SaveCurrentSelectedInfo();
        }

        private void SaveCurrentSelectedInfo()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.ShowNewFolderButton = true;
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string path = Path.Combine(dlg.SelectedPath, GetFileName());
                    suspectImage.Image.Save(path);
                }
            }
        }

        //generic pic filename
        protected string GetFileName()
        {
            DateTime dtime = DateTime.Now;
            string year = dtime.Year.ToString();
            return string.Format("{0}{1}{2}{3}{4}{5}{6}.jpg", year.Substring(2, 2),
                                                                    dtime.Month.ToString("d2"),
                                                                    dtime.Day.ToString("d2"),
                                                                    dtime.Hour.ToString("d2"),
                                                                    dtime.Minute.ToString("d2"),
                                                                    dtime.Second.ToString("d2"),
                                                                    dtime.Millisecond.ToString("d3"));
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            foreach (var item in suspectsList.SelectedItems)
            {
                this.suspectsList.Items.Remove((ListViewItem)item);
            }
        }

        private void lvPersonInfo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suspectsList.SelectedItems.Count > 0)
            {
                var lvi = suspectsList.SelectedItems[suspectsList.SelectedItems.Count - 1];
                var result = (PersonOfInterestDetectionResult)lvi.Tag;

                lblTextSim.Text = string.Format("相似度: {0:F0}%", result.Similarity);
                //犯罪分子图片显示
                if (personOfInterestImage.Image != null)
                {
                    personOfInterestImage.Image.Dispose();
                    personOfInterestImage.Image = null;
                }

                //personOfInterestImage.Image = result.Details.GetIpl().ToBitmap();
                //this.suspectImage.Image = result.Portrait.GetIpl().ToBitmap();
                btnOK.Enabled = true;
            }
        }

        private void ImmediatelyModel_Shown(object sender, EventArgs e)
        {
            if (this.suspectsList.Items.Count > 0)
            {
                this.suspectsList.Items[0].Selected = true;
                this.suspectsList.Select();
            }

        }

        private readonly List<PersonOfInterestDetectionResult> listPersons
            = new List<PersonOfInterestDetectionResult>();

        private void ImmediatelyModel_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.FormOwnerClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void clearAll_Click(object sender, EventArgs e)
        {
            this.suspectsList.Items.Clear();
            ClearImages();
            this.Hide();
        }

        private void ClearImages()
        {
            this.suspectImage.Image = null;
            this.personOfInterestImage.Image = null;
        }
    }


}
