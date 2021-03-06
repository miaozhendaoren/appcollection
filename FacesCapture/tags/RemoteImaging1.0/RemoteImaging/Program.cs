﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;


namespace RemoteImaging
{
    using RealtimeDisplay;

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());

            //ImageUploadWatcher watcher = new ImageUploadWatcher();
            //watcher.PathToWatch = @"d:\test";
            //watcher.ImagesUploaded += new ImageUploadHandler(watcher_ImagesUploaded);
            //watcher.Start();

            //while (true)
            //{
            //    System.Threading.Thread.Sleep(500);
            //}

        }

        static void watcher_ImagesUploaded(object Sender, ImageUploadEventArgs args)
        {
            DateTime time = args.Images[0].CaptureTime;
            string msg = string.Format("camID={0} count={1} time={2}", args.CameraID, args.Images.Length, time);
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}
