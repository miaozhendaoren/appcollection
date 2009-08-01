﻿using System.Data;
using System.Configuration;
using System.Web;
using System.Management;
using System.Text;
using System.IO;
using System.Collections;
using System.Xml;
using System;
using System.Timers;
using System.Xml.Linq;

namespace RemoteImaging
{
    public class FileHandle
    {
        public static readonly string xmlPath = "SetVal.xml";

        #region 构造函数
        public FileHandle()
        {
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Interval = 3000;
            timer.Enabled = true;
        }
        #endregion

        #region 删除文件
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="Path">物理路径</param>
        public static void FileDel(string Path)
        {
            File.Delete(Path);
        }
        #endregion

        #region 递归删除文件夹目录及文件
        /// <summary>
        /// 递归删除文件夹目录及文件
        /// </summary>
        /// <param name="dir"></param>  
        /// <returns></returns>
        public static void DeleteFolder(string dir)
        {
            if (Directory.Exists(dir)) //如果存在这个文件夹删除之 
            {
                foreach (string d in Directory.GetFileSystemEntries(dir))
                {
                    if (File.Exists(d))
                        File.Delete(d); //直接删除其中的文件                        
                    else
                        DeleteFolder(d); //递归删除子文件夹 
                }
                Directory.Delete(dir, true); //删除已空文件夹                
            }
        }
        #endregion

        #region 删除文件和空间报警
        //删除图片和录像
        public static void DelVidAndImg(SaveNodeType savaType, int cameraId)//这个方法没有指定特定的摄像机 D:\ImageOutput
        {
            string fileUrl = Properties.Settings.Default.OutputPath;//D:\ImageOutPut
            //string fileUrl = string.Format(Properties.Settings.Default.OutputPath+"\\{0:d2}\\", cameraId);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNode xRoot = xmlDoc.ChildNodes.Item(1);
            string nodeValue = xRoot.SelectSingleNode(String.Format("/Root/{0}/Value", savaType)).FirstChild.Value.ToString();
            string nodeSettime = xRoot.SelectSingleNode("/Root" + "/" + savaType.ToString() + "/SettedTime").FirstChild.Value.ToString();

            if (Directory.Exists(fileUrl))
            {
                string[] rootFiles = Directory.GetDirectories(fileUrl);
                if (rootFiles.Length > 0)
                {
                    foreach (string rootFile in rootFiles)
                    {
                        string[] files = Directory.GetDirectories(rootFile);//目录 D:\ImageOutPut\摄像机ID --> 1,D:\\ImageOutPut\\02\\2009 2,D:\\ImageOutPut\\02\\NORMAL

                        if (files.Length > 0)
                        {
                            foreach (string strFile in files)
                            {
                                if (strFile.Substring(strFile.Length - 6, 6).Equals("NORMAL"))//删除 video
                                {
                                    DateTime dTime = Convert.ToDateTime(DateTime.Now.ToShortDateString()).AddDays(Convert.ToDouble(-Convert.ToInt32(nodeValue)) + 1).ToUniversalTime();//2009-7-23 16:00:00
                                    DateTime ckTime = Convert.ToDateTime(DateTime.Now.ToShortDateString()).AddDays(Convert.ToDouble(-Convert.ToInt32(nodeValue))).ToUniversalTime();//时间最低设置范围
                                    string[] vidFiles = Directory.GetDirectories(strFile);
                                    if (vidFiles.Length > 0)
                                    {
                                        foreach (string strDayFile in vidFiles)
                                        {
                                            string[] vidHour = Directory.GetDirectories(strDayFile);//D:\ImageOutPut\02\NORMAL\20090630\02 获得日期下面滴子文件夹 是以每小时命名
                                            string date = strDayFile.Substring(strDayFile.Length - 8, 8);
                                            string year = date.Substring(0, 4);
                                            string month = date.Substring(4, 2);
                                            string day = date.Substring(6, 2);

                                            foreach (string strHour in vidHour)
                                            {
                                                string hour = strHour.Substring(strHour.Length - 2, 2);
                                                DateTime timeHour = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day), Convert.ToInt32(hour), 0, 0);
                                                if ((timeHour < ckTime) && (timeHour < dTime))
                                                {
                                                    FileHandle.DeleteFolder(strHour);
                                                }
                                            }
                                            FileHandle.DeleteFolder(strDayFile);
                                        }
                                    }
                                }
                                else //删除 image 
                                {
                                    //没有做判断年的情况
                                    DateTime dTime = Convert.ToDateTime(DateTime.Now.ToShortDateString()).AddDays(Convert.ToDouble(-Convert.ToInt32(nodeValue)));//这是最小日期
                                    string monthUrl = string.Format(strFile + "\\{0:d2}", dTime.Month);
                                    string[] monthFiles = Directory.GetDirectories(monthUrl);
                                    if (monthFiles.Length > 0)
                                    {
                                        foreach (string strMonthFile in monthFiles)
                                        {
                                            DateTime passTime = new DateTime(dTime.Year, dTime.Month, Convert.ToInt32(strMonthFile.Substring(strMonthFile.Length - 2, 2)));
                                            if (passTime < dTime)
                                            {
                                                string imgUrl = string.Format(rootFile + "\\{0:d4}\\{1:d2}\\{2:d2}", passTime.Year, passTime.Month, passTime.Day);
                                                FileHandle.DeleteFolder(imgUrl);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        FileHandle.DeleteFolder(rootFile);
                    }
                }
            }

        }

        //磁盘空间报警
        public void DiskWarn(SaveNodeType savaType)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNode xRoot = xmlDoc.ChildNodes.Item(1);
            string nodeValue = xRoot.SelectSingleNode(String.Format("/Root/{0}/Value", savaType)).FirstChild.Value.ToString();
            string nodeSettime = xRoot.SelectSingleNode("/Root" + "/" + savaType.ToString() + "/SettedTime").FirstChild.Value.ToString();


            string outPutPath = Properties.Settings.Default.OutputPath;
            string upLoadPool = Properties.Settings.Default.ImageUploadPool;
            string diskName = string.Format("\"{0}\"", "" + outPutPath.Substring(0, 2) + "");

            UInt64 allCount = DiskSize(diskName,"Size");//"\"D:\"" -- > "D:"  //当前磁盘总的大小

            UInt64 freeCount = DiskSize(diskName, "FreeSpace");

            if (freeCount < 3600)
            {
                System.Timers.Timer timeCheck = new System.Timers.Timer();
                timeCheck.Elapsed += new ElapsedEventHandler(timeCheck_Elapsed);
                timeCheck.Interval = 360000;
                timeCheck.Enabled = true;
            }
        }

        private void timeCheck_Elapsed(object source, ElapsedEventArgs args)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNode xRoot = xmlDoc.ChildNodes.Item(1);
            string nodeValue = xRoot.SelectSingleNode(String.Format("/Root/{0}/Value", SaveNodeType.WarnDisk)).FirstChild.Value.ToString();

            string diskPath =  Properties.Settings.Default.OutputPath.Substring(0,2);
            string diskName = string.Format("\"{0}\"", "" + diskPath + "");
            UInt64 freeCount = DiskSize(diskName, "FreeSpace");

            if (freeCount < Convert.ToUInt64(nodeValue))
            {
                picIndex = 0;
                msg = "内存空间不足！！";
                ShowResDialog();
            }

        }

        #region 弹出窗口的操作
        bool temp = true;
        private string msg = "";//提示的消息
        private int picIndex = 0;//图片的索引

        private void timer_Elapsed(object source, ElapsedEventArgs args)
        {
            temp = true;
        }

        private void ShowResDialog()
        {
            if (temp)
            {
                AlertSettingRes asr = new AlertSettingRes(msg, picIndex);
                asr.HeightMax = 157;
                asr.WidthMax = 217;
                asr.ScrollShow();
                temp = false;
            }
        }
        #endregion

        //取得disk大小
        public static UInt64 DiskSize(string path,string propertys)
        {
            ManagementObject size = new ManagementObject("win32_logicaldisk.deviceid=" + path);
            size.Get();
            UInt64 b = 1024;
            UInt64 a = (Convert.ToUInt64(size[propertys]) / b) / b;
            return a;
        }

        /// <summary>
        /// 获得磁盘剩余空间大小
        /// </summary>
        /// <param name="disk">'D:' || "D:"</param>
        public static UInt64 GetDiskFreeSpaceSize(string disk)
        {
            WqlObjectQuery woq = new WqlObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DeviceID = " + disk + "");
            ManagementObjectSearcher mos = new ManagementObjectSearcher(woq);
            UInt64 a = 0;
            UInt64 b = 1024;
            foreach (ManagementObject mo in mos.Get())
            {
                //Console.WriteLine("Description: " + mo["Description"]);
                //Console.WriteLine("File system: " + mo["FileSystem"]);
                //Console.WriteLine("Free disk space: " + mo["FreeSpace"]);
                //Console.WriteLine("Size: " + mo["Size"]);
                a += (Convert.ToUInt64(mo["FreeSpace"]) / b) / b;

            }
            return a;
        }

        #region 获得指定文件夹的大小 -- 不用
        public static long GetSize(string path)
        {
            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(path);
            long count = 0;
            foreach (System.IO.FileSystemInfo fi in dir.GetFileSystemInfos())
            {
                if (fi.Attributes.ToString().ToLower().Equals("directory"))
                {
                    count += GetSize(fi.FullName);
                }
                else
                {
                    System.IO.FileInfo finf = new System.IO.FileInfo(fi.FullName);
                    count += finf.Length;
                }
            }
            return count;
        }  //录像一天4320MB 约 4.2GB 一周 30GB

        public static long GetFileSize(string path)
        {
            long fileCount = 0;
            if (Directory.Exists(path))
            {
                string[] filePath = Directory.GetDirectories(path);
                foreach (string file in filePath)
                {
                    fileCount += GetSize(file);
                }
            }
            return fileCount;
        }
        #endregion

        #endregion

        #region xml文件的操作

        //判断XML文件是否存在
        public bool IsXmlExists()
        {
            return System.IO.File.Exists(FileHandle.xmlPath);
        }

        //创建XML文档  暂时不用
        public void CreateXml()
        {
            if (!this.IsXmlExists())
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlNode xNode = xmlDoc.CreateNode(XmlNodeType.XmlDeclaration, "", "");
                xmlDoc.AppendChild(xNode);
                XmlElement xRootElement = xmlDoc.CreateElement("", "Root", "");
                XmlNode xText = xmlDoc.CreateTextNode("");
                xRootElement.AppendChild(xText);
                xmlDoc.AppendChild(xRootElement);
                try
                {
                    xmlDoc.Save(FileHandle.xmlPath);
                }
                catch (Exception ex)
                {
                    Console.Write(ex.StackTrace.ToString());
                }
            }
        }

        //添加节点
        private void AppendXmlNode(XmlDocument xmlDoc, XmlNode xAddNode, SaveNodeType nodeType)
        {
            XmlNode xRoot = xmlDoc.ChildNodes.Item(1);
            XmlNode xNode = xRoot.SelectSingleNode("/Root/" + nodeType.ToString());
            if (xNode != null)
            {
                xRoot.RemoveChild(xNode);
                xRoot.AppendChild(xAddNode);
            }
            else
            {
                xRoot.AppendChild(xAddNode);
            }
            xmlDoc.Save(FileHandle.xmlPath);
        }

        //创建节点元素
        public void CreateElement(SaveNodeType nodeType, string value, string time)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(FileHandle.xmlPath);
            XmlElement xNode = xmlDoc.CreateElement(nodeType.ToString());
            XmlElement xFirNode = xmlDoc.CreateElement("Type");
            XmlText xFirText = xmlDoc.CreateTextNode(nodeType.ToString());
            xFirNode.AppendChild(xFirText);

            XmlElement xSecNode = xmlDoc.CreateElement("Value");
            XmlText xSecrText = xmlDoc.CreateTextNode(value);
            xSecNode.AppendChild(xSecrText);

            XmlElement xThrNode = xmlDoc.CreateElement("SettedTime");
            XmlText xThrText = xmlDoc.CreateTextNode(time);
            xThrNode.AppendChild(xThrText);

            xNode.AppendChild(xFirNode);
            xNode.AppendChild(xSecNode);
            xNode.AppendChild(xThrNode);
            AppendXmlNode(xmlDoc, xNode, nodeType);
        }

        #endregion
    }
    public enum SaveNodeType
    {
        Video, WarnDisk
    }
}