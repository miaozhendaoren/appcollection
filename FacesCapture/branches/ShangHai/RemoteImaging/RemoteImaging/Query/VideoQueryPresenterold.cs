﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Damany.Imaging.Common;
using Damany.PortraitCapturer.DAL;
using Damany.RemoteImaging.Common;
using Damany.Util;
using RemoteControlService;
using Video = RemoteImaging.Core.Video;
using Damany.Util.Extensions;

namespace RemoteImaging.Query
{
    public class VideoQueryPresenter : IVideoQueryPresenter
    {
        private readonly IVideoQueryScreen _screen;
        private readonly IRepository _portraitRepository;
        private readonly ConfigurationManager _manager;
        public int PageSize = 100;
        public int TotalPage = 0;
        public int TotalCount = 0;
        public int currentPage = 0;
        private Core.Video[] TotalVideos = null;
        private DateTimeRange DTRange;
        private SearchScope typeSearchScope;
        public VideoQueryPresenter(IVideoQueryScreen screen,
                                   IRepository portraitRepository,
                                   ConfigurationManager manager)
        {
            _screen = screen;
            _portraitRepository = portraitRepository;
            _manager = manager;
        }

        public void Start()
        {
            _screen.AttachPresenter(this);
            _screen.Cameras = _manager.GetCameras().ToArray();
            _screen.Show();

        }
        public void SetPageSize(int _pagesize)
        {
            PageSize = _pagesize;
        }
        public int GetTotalPage()
        {
            return TotalPage;
        }
        public int GetCurrentPage()
        {
            return currentPage;
        }


        public void NavigateToPrev()
        {
            if (TotalPage == 0)
            {
                return;
            }
            currentPage = currentPage - 1;
            if (currentPage <0)
            {
                currentPage = 0;
            }
            else
            {
                LoadVideo();
            }
        }
        public void NavigateToNext()
        {
            if (TotalPage == 0)
            {
                return;
            }
            currentPage = currentPage + 1;
            if (currentPage > (TotalPage - 1))
            {
                currentPage = TotalPage - 1;
            }
            else
            {
                LoadVideo();
            }

        }
        public void NavigateToLast()
        {
            if (TotalPage == 0)
            {
                return;
            }
            currentPage = TotalPage-1;
            
            LoadVideo();
             
        }
        public void LoadVideo()
        {
            var selectedCamera = this._screen.SelectedCamera;
            if (selectedCamera == null)
            {
                return;
            }

            var range = this._screen.TimeRange;
            var type = this._screen.SearchScope;
            var frameQuery = _portraitRepository.GetFrames(selectedCamera.Id, range).ToArray();
            var portraitQuery = _portraitRepository.GetPortraits(selectedCamera.Id, range).ToArray();
            Core.Video v;
            this._screen.ClearAll();

            for (int i = currentPage * PageSize; i < currentPage * PageSize + PageSize; i++)
            {
                if (i >= TotalCount)
                {
                    break;
                }
                v = TotalVideos[i];
                var queryTime = new DateTimeRange(v.CapturedAt, v.CapturedAt);
                v.HasMotionDetected = frameQuery.FirstOrDefault(f => f.CapturedAt.RoundToMinute() == v.CapturedAt.RoundToMinute()) != null;
                v.HasFaceCaptured = portraitQuery.FirstOrDefault(p => p.CapturedAt.RoundToMinute() == v.CapturedAt.RoundToMinute()) != null;
                if ((type & SearchScope.FaceCapturedVideo)
                      == SearchScope.FaceCapturedVideo)
                {
                    if (v.HasFaceCaptured)
                    {
                        _screen.AddVideo(v);
                    }
                }

                if ((type & SearchScope.MotionWithoutFaceVideo)
                     == SearchScope.MotionWithoutFaceVideo)
                {
                    if (v.HasMotionDetected && !v.HasFaceCaptured)
                    {
                        _screen.AddVideo(v);
                    }
                }

                if ((type & SearchScope.MotionLessVideo)
                      == SearchScope.MotionLessVideo)
                {
                    if (!v.HasFaceCaptured && !v.HasMotionDetected)
                    {
                        _screen.AddVideo(v);
                    }
                }

            }
        }
        public void NavigateToFirst()
        {
            if (TotalPage == 0)
            {
                return;
            }
            currentPage = 0;
            LoadVideo();
          
        }
        public void Search()
        {

            var selectedCamera = this._screen.SelectedCamera;
            if (selectedCamera == null)
            {
                return;
            }

            var range = this._screen.TimeRange;
            var type = this._screen.SearchScope;
            DTRange = range;
            typeSearchScope = type;

            Core.Video[] videos =
                new FileSystemStorage(Properties.Settings.Default.OutputPath).VideoFilesBetween(selectedCamera.Id, range.From, range.To);
            TotalVideos = videos;
            if (videos.Length == 0) return;
            TotalCount = TotalVideos.Length;
            if ((TotalCount % PageSize) > 0)
            {
                TotalPage =((int )(TotalCount / PageSize))+1;
            }
            else
            {
                TotalPage = ((int)(TotalCount / PageSize)) ;
            }
            currentPage = 0;
            NavigateToFirst();
        }

        public void PlayVideo()
        {
            var p = this._screen.SelectedVideoFile;
            if (p == null) return;
            if (string.IsNullOrEmpty(p.Path))
                return;
            if (!System.IO.File.Exists(p.Path))
                return;

            this._screen.PlayVideoInPlace(p.Path);
        }

        public void ShowRelatedFaces()
        {
            var video = _screen.SelectedVideoFile;

            var from = Damany.Util.Extensions.MiscHelper.RoundToMinute(video.CapturedAt);
            var to = from.AddMinutes(1);

            var range = new DateTimeRange(from, to);

            var p = _portraitRepository.GetPortraits(_screen.SelectedCamera.Id, range);
            _screen.ClearFacesList();

            foreach (var portrait in p)
            {
                 _screen.AddFace(portrait);
            }
           
        }
    }
}
