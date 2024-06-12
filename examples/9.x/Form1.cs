using System;
using System.Drawing;
using System.Windows.Forms;
using Dynamsoft;
using Dynamsoft.DBR;
using System.Diagnostics;

using DirectShowLib;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Drawing.Imaging;

namespace BarcodeReaderApp
{
    public partial class Form1 : Form, ISampleGrabberCB
    {
        private Dynamsoft.DBR.BarcodeReader _barcodeReader;
        private int _previewWidth = 640;
        private int _previewHeight = 480;
        private int _previewStride = 0;
        private int _previewFPS = 30;
        private volatile bool isFinished = true;
        private bool isCameraMode = false;

        IVideoWindow videoWindow = null;
        IMediaControl mediaControl = null;
        IFilterGraph2 graphBuilder = null;
        ICaptureGraphBuilder2 captureGraphBuilder = null;

        DsROTEntry rot = null;

        public Form1()
        {
            InitializeComponent();

            // Initialize Dynamsoft Barcode Reader
            string errorMsg;
            EnumErrorCode errorCode = Dynamsoft.DBR.BarcodeReader.InitLicense("DLS2eyJoYW5kc2hha2VDb2RlIjoiMjAwMDAxLTE2NDk4Mjk3OTI2MzUiLCJvcmdhbml6YXRpb25JRCI6IjIwMDAwMSIsInNlc3Npb25QYXNzd29yZCI6IndTcGR6Vm05WDJrcEQ5YUoifQ==", out errorMsg);
            if (errorCode != EnumErrorCode.DBR_SUCCESS)
            {
                Console.WriteLine(errorMsg);
            }
            _barcodeReader = new Dynamsoft.DBR.BarcodeReader();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            isCameraMode = false;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Open Image";
                //dlg.Filter = "bmp files (*.bmp)|*.bmp";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bitmap = null;
                    
                    try
                    {
                        bitmap =  new Bitmap(dlg.FileName);
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show("File not supported.");
                        return;
                    }

                    pictureBox1.Image = new Bitmap(dlg.FileName);
                    ReadBarcode((Bitmap)pictureBox1.Image);
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            isCameraMode = true;
            string button_text = button3.Text;
            if (button_text.Equals("Start Webcam"))
            {
                button3.Text = "Stop Webcam";
                StartCamera();
            }
            else
            {
                button3.Text = "Start Webcam";
                StopCamera();
            }  
        }

        private void StartCamera()
        {
            // Get connected devices
            DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            if (devices.Length == 0)
            {
                MessageBox.Show("No USB webcam connected");
                button3.Text = "Start Webcam";
                return;
            }
            else
            {
                //DsDevice dev = devices[0] as DsDevice;
                //MessageBox.Show("Device: " + dev.Name);
                //CaptureVideo();
                CaptureVideo(devices[0]);
            }
        }

        private void StopCamera()
        {
            button3.Text = "Start Webcam";
            CloseInterfaces();
        }

        private Bitmap EnsureNonIndexedFormat(Bitmap original)
        {
            if (original.PixelFormat == PixelFormat.Format24bppRgb ||
                original.PixelFormat == PixelFormat.Format32bppArgb ||
                original.PixelFormat == PixelFormat.Format32bppPArgb)
            {
                return original;
            }

            // Convert to 24bppRgb format
            Bitmap newBitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(original, 0, 0);
            }

            return newBitmap;
        }

        private void ReadBarcode(Bitmap bitmap)
        {
            Bitmap nonIndexedBitmap = EnsureNonIndexedFormat(bitmap);

            TextResult[] results = _barcodeReader.DecodeBitmap(nonIndexedBitmap, "");
            textBox1.Clear();

            if (results != null)
            {
                using (Graphics g = Graphics.FromImage(nonIndexedBitmap))
                {
                    foreach (TextResult result in results)
                    {
                        string output = "Text: " + result.BarcodeText + Environment.NewLine + "Format: " + result.BarcodeFormatString + Environment.NewLine;
                        Point[] points = result.LocalizationResult.ResultPoints;

                        // Draw lines based on four points
                        for (int i = 0; i < points.Length; i++)
                        {
                            Point p1 = points[i];
                            Point p2 = points[(i + 1) % points.Length];
                            g.DrawLine(new Pen(Color.Red, 3), p1, p2);
                        }

                        textBox1.AppendText(output);
                    }
                }

                if (!isCameraMode)
                {
                    pictureBox1.Image = nonIndexedBitmap;
                }
            }
            else
            {
                textBox1.AppendText("No barcode detected!" + Environment.NewLine);
            }
        }

        public void CaptureVideo(DsDevice device)
        {
            pictureBox1.Image = null;
            int hr = 0;
            IBaseFilter sourceFilter = null;
            ISampleGrabber sampleGrabber = null;

            try
            {
                // Get DirectShow interfaces
                GetInterfaces();

                // Attach the filter graph to the capture graph
                hr = this.captureGraphBuilder.SetFiltergraph(this.graphBuilder);
                DsError.ThrowExceptionForHR(hr);

                // Use the system device enumerator and class enumerator to find
                // a video capture/preview device, such as a desktop USB video camera.
                sourceFilter = SelectCaptureDevice(device);
                // Add Capture filter to graph.
                hr = this.graphBuilder.AddFilter(sourceFilter, "Video Capture");
                DsError.ThrowExceptionForHR(hr);

                // Initialize SampleGrabber.
                sampleGrabber = new SampleGrabber() as ISampleGrabber;
                // Configure SampleGrabber. Add preview callback.
                ConfigureSampleGrabber(sampleGrabber);
                // Add SampleGrabber to graph.
                hr = this.graphBuilder.AddFilter(sampleGrabber as IBaseFilter, "Frame Callback");
                DsError.ThrowExceptionForHR(hr);

                // Configure preview settings.
                SetConfigParams(this.captureGraphBuilder, sourceFilter, _previewFPS, _previewWidth, _previewHeight);

                // Render the preview
                hr = this.captureGraphBuilder.RenderStream(PinCategory.Preview, MediaType.Video, sourceFilter, (sampleGrabber as IBaseFilter), null);
                DsError.ThrowExceptionForHR(hr);

                SaveSizeInfo(sampleGrabber);

                // Set video window style and position
                SetupVideoWindow();

                // Add our graph to the running object table, which will allow
                // the GraphEdit application to "spy" on our graph
                rot = new DsROTEntry(this.graphBuilder);

                // Start previewing video data
                hr = this.mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);
            }
            catch
            {
                MessageBox.Show("An unrecoverable error has occurred.");
            }
            finally
            {
                if (sourceFilter != null)
                {
                    Marshal.ReleaseComObject(sourceFilter);
                    sourceFilter = null;
                }

                if (sampleGrabber != null)
                {
                    Marshal.ReleaseComObject(sampleGrabber);
                    sampleGrabber = null;
                }
            }
        }

        public void CaptureVideo()
        {
            pictureBox1.Image = null;
            int hr = 0;
            IBaseFilter sourceFilter = null;
            ISampleGrabber sampleGrabber = null;

            try
            {
                // Get DirectShow interfaces
                GetInterfaces();

                // Attach the filter graph to the capture graph
                hr = this.captureGraphBuilder.SetFiltergraph(this.graphBuilder);
                DsError.ThrowExceptionForHR(hr);

                // Use the system device enumerator and class enumerator to find
                // a video capture/preview device, such as a desktop USB video camera.
                sourceFilter = FindCaptureDevice();
                // Add Capture filter to graph.
                hr = this.graphBuilder.AddFilter(sourceFilter, "Video Capture");
                DsError.ThrowExceptionForHR(hr);

                // Initialize SampleGrabber.
                sampleGrabber = new SampleGrabber() as ISampleGrabber;
                // Configure SampleGrabber. Add preview callback.
                ConfigureSampleGrabber(sampleGrabber);
                // Add SampleGrabber to graph.
                hr = this.graphBuilder.AddFilter(sampleGrabber as IBaseFilter, "Frame Callback");
                DsError.ThrowExceptionForHR(hr);

                // Configure preview settings.
                SetConfigParams(this.captureGraphBuilder, sourceFilter, _previewFPS, _previewWidth, _previewHeight);

                // Render the preview
                hr = this.captureGraphBuilder.RenderStream(PinCategory.Preview, MediaType.Video, sourceFilter, (sampleGrabber as IBaseFilter), null);
                DsError.ThrowExceptionForHR(hr);

                SaveSizeInfo(sampleGrabber);

                // Set video window style and position
                SetupVideoWindow();

                // Add our graph to the running object table, which will allow
                // the GraphEdit application to "spy" on our graph
                rot = new DsROTEntry(this.graphBuilder);

                // Start previewing video data
                hr = this.mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);
            }
            catch
            {
                MessageBox.Show("An unrecoverable error has occurred.");
            }
            finally
            {
                if (sourceFilter != null)
                {
                    Marshal.ReleaseComObject(sourceFilter);
                    sourceFilter = null;
                }

                if (sampleGrabber != null)
                {
                    Marshal.ReleaseComObject(sampleGrabber);
                    sampleGrabber = null;
                }
            }
        }

        public IBaseFilter SelectCaptureDevice(DsDevice device)
        {
            object source = null;
            Guid iid = typeof(IBaseFilter).GUID;
            device.Mon.BindToObject(null, null, ref iid, out source);
            return (IBaseFilter)source;
        }

        public IBaseFilter FindCaptureDevice()
        {
            int hr = 0;
#if USING_NET11
      UCOMIEnumMoniker classEnum = null;
      UCOMIMoniker[] moniker = new UCOMIMoniker[1];
#else
            IEnumMoniker classEnum = null;
            IMoniker[] moniker = new IMoniker[1];
#endif
            object source = null;

            // Create the system device enumerator
            ICreateDevEnum devEnum = (ICreateDevEnum)new CreateDevEnum();

            // Create an enumerator for the video capture devices
            hr = devEnum.CreateClassEnumerator(FilterCategory.VideoInputDevice, out classEnum, 0);
            DsError.ThrowExceptionForHR(hr);

            // The device enumerator is no more needed
            Marshal.ReleaseComObject(devEnum);

            // If there are no enumerators for the requested type, then 
            // CreateClassEnumerator will succeed, but classEnum will be NULL.
            if (classEnum == null)
            {
                throw new ApplicationException("No video capture device was detected.\r\n\r\n" +
                                               "This sample requires a video capture device, such as a USB WebCam,\r\n" +
                                               "to be installed and working properly.  The sample will now close.");
            }

            // Use the first video capture device on the device list.
            // Note that if the Next() call succeeds but there are no monikers,
            // it will return 1 (S_FALSE) (which is not a failure).  Therefore, we
            // check that the return code is 0 (S_OK).
#if USING_NET11
      int i;
      if (classEnum.Next (moniker.Length, moniker, IntPtr.Zero) == 0)
#else
            if (classEnum.Next(moniker.Length, moniker, IntPtr.Zero) == 0)
#endif
            {
                // Bind Moniker to a filter object
                Guid iid = typeof(IBaseFilter).GUID;
                moniker[0].BindToObject(null, null, ref iid, out source);
            }
            else
            {
                throw new ApplicationException("Unable to access video capture device!");
            }

            // Release COM objects
            Marshal.ReleaseComObject(moniker[0]);
            Marshal.ReleaseComObject(classEnum);

            // An exception is thrown if cast fail
            return (IBaseFilter)source;
        }

        public void GetInterfaces()
        {
            int hr = 0;

            // An exception is thrown if cast fail
            this.graphBuilder = (IFilterGraph2)new FilterGraph();
            this.captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            this.mediaControl = (IMediaControl)this.graphBuilder;
            this.videoWindow = (IVideoWindow)this.graphBuilder;
            DsError.ThrowExceptionForHR(hr);
        }

        public void CloseInterfaces()
        {
            if (mediaControl != null)
            {
                int hr = mediaControl.StopWhenReady();
                DsError.ThrowExceptionForHR(hr);
            }

            if (videoWindow != null)
            {
                videoWindow.put_Visible(OABool.False);
                videoWindow.put_Owner(IntPtr.Zero);
            }

            // Remove filter graph from the running object table.
            if (rot != null)
            {
                rot.Dispose();
                rot = null;
            }

            // Release DirectShow interfaces.
            Marshal.ReleaseComObject(this.mediaControl); this.mediaControl = null;
            Marshal.ReleaseComObject(this.videoWindow); this.videoWindow = null;
            Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;
            Marshal.ReleaseComObject(this.captureGraphBuilder); this.captureGraphBuilder = null;
        }

        public void SetupVideoWindow()
        {
            int hr = 0;

            // Set the video window to be a child of the PictureBox
            hr = this.videoWindow.put_Owner(pictureBox1.Handle);
            DsError.ThrowExceptionForHR(hr);

            hr = this.videoWindow.put_WindowStyle(WindowStyle.Child);
            DsError.ThrowExceptionForHR(hr);

            // Make the video window visible, now that it is properly positioned
            hr = this.videoWindow.put_Visible(OABool.True);
            DsError.ThrowExceptionForHR(hr);

            // Set the video position
            Rectangle rc = pictureBox1.ClientRectangle;
            hr = videoWindow.SetWindowPosition(0, 0, _previewWidth, _previewHeight);
            DsError.ThrowExceptionForHR(hr);
        }

        public int SampleCB(double SampleTime, IMediaSample pSample)
        {
            throw new NotImplementedException();
        }

        private void SetConfigParams(ICaptureGraphBuilder2 capGraph, IBaseFilter capFilter, int iFrameRate, int iWidth, int iHeight)
        {
            int hr;
            object config;
            AMMediaType mediaType;
            // Find the stream config interface
            hr = capGraph.FindInterface(
                PinCategory.Capture, MediaType.Video, capFilter, typeof(IAMStreamConfig).GUID, out config);

            IAMStreamConfig videoStreamConfig = config as IAMStreamConfig;
            if (videoStreamConfig == null)
            {
                throw new Exception("Failed to get IAMStreamConfig");
            }

            // Get the existing format block
            hr = videoStreamConfig.GetFormat(out mediaType);
            DsError.ThrowExceptionForHR(hr);

            // copy out the videoinfoheader
            VideoInfoHeader videoInfoHeader = new VideoInfoHeader();
            Marshal.PtrToStructure(mediaType.formatPtr, videoInfoHeader);

            // if overriding the framerate, set the frame rate
            if (iFrameRate > 0)
            {
                videoInfoHeader.AvgTimePerFrame = 10000000 / iFrameRate;
            }

            // if overriding the width, set the width
            if (iWidth > 0)
            {
                videoInfoHeader.BmiHeader.Width = iWidth;
            }

            // if overriding the Height, set the Height
            if (iHeight > 0)
            {
                videoInfoHeader.BmiHeader.Height = iHeight;
            }

            // Copy the media structure back
            Marshal.StructureToPtr(videoInfoHeader, mediaType.formatPtr, false);

            // Set the new format
            hr = videoStreamConfig.SetFormat(mediaType);
            DsError.ThrowExceptionForHR(hr);

            DsUtils.FreeAMMediaType(mediaType);
            mediaType = null;
        }

        private void SaveSizeInfo(ISampleGrabber sampleGrabber)
        {
            int hr;

            // Get the media type from the SampleGrabber
            AMMediaType media = new AMMediaType();
            hr = sampleGrabber.GetConnectedMediaType(media);
            DsError.ThrowExceptionForHR(hr);

            if ((media.formatType != FormatType.VideoInfo) || (media.formatPtr == IntPtr.Zero))
            {
                throw new NotSupportedException("Unknown Grabber Media Format");
            }

            // Grab the size info
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));
            _previewStride = _previewWidth * (videoInfoHeader.BmiHeader.BitCount / 8);

            DsUtils.FreeAMMediaType(media);
            media = null;
        }

        private void ConfigureSampleGrabber(ISampleGrabber sampleGrabber)
        {
            AMMediaType media;
            int hr;

            // Set the media type to Video/RBG24
            media = new AMMediaType();
            media.majorType = MediaType.Video;
            media.subType = MediaSubType.RGB24;
            media.formatType = FormatType.VideoInfo;

            hr = sampleGrabber.SetMediaType(media);
            DsError.ThrowExceptionForHR(hr);

            DsUtils.FreeAMMediaType(media);
            media = null;

            hr = sampleGrabber.SetCallback(this, 1);
            DsError.ThrowExceptionForHR(hr);
        }

        public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            Bitmap v = new Bitmap(_previewWidth, _previewHeight, _previewStride,
                PixelFormat.Format24bppRgb, pBuffer);
            v.RotateFlip(RotateFlipType.Rotate180FlipX);
            if (isFinished)
            {    
                this.BeginInvoke((MethodInvoker)delegate
                {
                    isFinished = false;
                    ReadBarcode(v);
                    isFinished = true;
                });
            }
            return 0;
        }
    }
}
