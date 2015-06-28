using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
//using System.Drawing;
using System.Windows.Forms;
using Microsoft.Kinect;

namespace K4W.BasicOverview.UI
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Size fo the RGB pixel in bitmap
        /// </summary>
        private readonly int _bytePerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        /// <summary>
        /// Representation of the Kinect Sensor
        /// </summary>
        private KinectSensor _kinect = null;

        /// <summary>
        /// FrameReader for our coloroutput
        /// </summary>
        private ColorFrameReader _colorReader = null;

        /// <summary>
        /// FrameReader for our depth output
        /// </summary>
        private DepthFrameReader _depthReader = null;

        /// <summary>
        /// FrameReader for our infrared output
        /// </summary>
        private InfraredFrameReader _infraReader = null;

        /// <summary>
        /// FrameReader for our body output
        /// </summary>
        private BodyFrameReader _bodyReader = null;

        /// <summary>
        /// Array of color pixels
        /// </summary>
        private byte[] _colorPixels = null;

        /// <summary>
        /// Array of depth pixels used for the output
        /// </summary>
        private byte[] _depthPixels = null;

        /// <summary>
        /// Array of infrared pixels used for the output
        /// </summary>
        private byte[] _infraPixels = null;

        /// <summary>
        /// Array of depth values
        /// </summary>
        private ushort[] _depthData = null;

        /// <summary>
        /// Array of infrared data
        /// </summary>
        private ushort[] _infraData = null;

        /// <summary>
        /// All tracked bodies
        /// </summary>
        private Body[] _bodies = null;

        /// <summary>
        /// Color WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _colorBitmap = null;

        /// <summary>
        /// Color WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _depthBitmap = null;

        /// <summary>
        /// Infrared WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _infraBitmap = null;

        int numjoints = 25;

        private bool initialized = false;
        private bool recording = false;

        private string outfile = "";
        private StreamWriter outstream;

        private List<String> order = new List<String>();

        /// <summary>
        /// Default CTOR
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Set up output order
            order.Add("Time");
            for (int j = 0; j < numjoints; j++)
            {
                order.Add("Joint:" + Enum.GetName(typeof(JointType), j));
            }

            // Initialize Kinect
            if (InitializeKinect())
            {
                // Setup finished
                Status.Text = "Ready";
                RecordText.Text = "Record";
                initialized = true;
            }
            else
            {
                Status.Text = "Could not find Kinect";
                RecordText.Text = "Initialize Kinect";
            }

            // Close Kinect when closing app
            Closing += OnClosing;
        }

        /// <summary>
        /// Close Kinect & Kinect Service
        /// </summary>
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close Kinect
            if (_kinect != null) _kinect.Close();
        }


        #region INITIALISATION
        /// <summary>
        /// Initialize Kinect Sensor
        /// </summary>
        private bool InitializeKinect()
        {
            // Get first Kinect
            _kinect = KinectSensor.GetDefault();

            if (_kinect == null) return false;

            // Open connection
            _kinect.Open();

            // Initialize Camera
            InitializeCamera();

            // Initialize Body
            IntializeBody();

            return true;
        }

        /// <summary>
        /// Initialize Kinect Camera
        /// </summary>
        private void InitializeCamera()
        {
            if (_kinect == null) return;

            // Get frame description for the color output
            FrameDescription desc = _kinect.ColorFrameSource.FrameDescription;

            // Get the framereader for Color
            _colorReader = _kinect.ColorFrameSource.OpenReader();

            // Allocate pixel array
            _colorPixels = new byte[desc.Width * desc.Height * _bytePerPixel];

            // Create new WriteableBitmap
            _colorBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);

            // Link WBMP to UI
            CameraImage.Source = _colorBitmap;

            // Hook-up event
            _colorReader.FrameArrived += OnColorFrameArrived;
        }

        /// <summary>
        /// Initialize Body Tracking
        /// </summary>
        private void IntializeBody()
        {
            if (_kinect == null) return;

            // Allocate Bodies array
            _bodies = new Body[_kinect.BodyFrameSource.BodyCount];

            // Open reader
            _bodyReader = _kinect.BodyFrameSource.OpenReader();

            // Hook-up event
            _bodyReader.FrameArrived += OnBodyFrameArrived;
        }
        #endregion INITIALISATION


        #region FRAME PROCESSING
        /// <summary>
        /// Process color frames & show in UI
        /// </summary>
        private void OnColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // Get the reference to the color frame
            ColorFrameReference colorRef = e.FrameReference;

            if (colorRef == null) return;

            // Acquire frame for specific reference
            ColorFrame frame = colorRef.AcquireFrame();

            // It's possible that we skipped a frame or it is already gone
            if (frame == null) return;

            using (frame)
            {
                // Get frame description
                FrameDescription frameDesc = frame.FrameDescription;

                // Check if width/height matches
                if (frameDesc.Width == _colorBitmap.PixelWidth && frameDesc.Height == _colorBitmap.PixelHeight)
                {
                    // Copy data to array based on image format
                    if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        frame.CopyRawFrameDataToArray(_colorPixels);
                    }
                    else frame.CopyConvertedFrameDataToArray(_colorPixels, ColorImageFormat.Bgra);

                    // Copy output to bitmap
                    _colorBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _colorPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the depth frames and update UI
        /// </summary>
        private void OnDepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            DepthFrameReference refer = e.FrameReference;

            if (refer == null) return;

            DepthFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            using (frame)
            {
                FrameDescription frameDesc = frame.FrameDescription;

                if (((frameDesc.Width * frameDesc.Height) == _depthData.Length) && (frameDesc.Width == _depthBitmap.PixelWidth) && (frameDesc.Height == _depthBitmap.PixelHeight))
                {
                    // Copy depth frames
                    frame.CopyFrameDataToArray(_depthData);

                    // Get min & max depth
                    ushort minDepth = frame.DepthMinReliableDistance;
                    ushort maxDepth = frame.DepthMaxReliableDistance;

                    // Adjust visualisation
                    int colorPixelIndex = 0;
                    for (int i = 0; i < _depthData.Length; ++i)
                    {
                        // Get depth value
                        ushort depth = _depthData[i];

                        if (depth == 0)
                        {
                            _depthPixels[colorPixelIndex++] = 41;
                            _depthPixels[colorPixelIndex++] = 239;
                            _depthPixels[colorPixelIndex++] = 242;
                        }
                        else if (depth < minDepth || depth > maxDepth)
                        {
                            _depthPixels[colorPixelIndex++] = 25;
                            _depthPixels[colorPixelIndex++] = 0;
                            _depthPixels[colorPixelIndex++] = 255;
                        }
                        else
                        {
                            double gray = (Math.Floor((double)depth / 250) * 12.75);

                            _depthPixels[colorPixelIndex++] = (byte)gray;
                            _depthPixels[colorPixelIndex++] = (byte)gray;
                            _depthPixels[colorPixelIndex++] = (byte)gray;
                        }

                        // Increment
                        ++colorPixelIndex;
                    }

                    // Copy output to bitmap
                    _depthBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _depthPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the infrared frames and update UI
        /// </summary>
        private void OnInfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            // Reference to infrared frame
            InfraredFrameReference refer = e.FrameReference;

            if (refer == null) return;

            // Get infrared frame
            InfraredFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            // Process it
            using (frame)
            {
                // Get the description
                FrameDescription frameDesc = frame.FrameDescription;

                if (((frameDesc.Width * frameDesc.Height) == _infraData.Length) && (frameDesc.Width == _infraBitmap.PixelWidth) && (frameDesc.Height == _infraBitmap.PixelHeight))
                {
                    // Copy data
                    frame.CopyFrameDataToArray(_infraData);

                    int colorPixelIndex = 0;

                    for (int i = 0; i < _infraData.Length; ++i)
                    {
                        // Get infrared value
                        ushort ir = _infraData[i];

                        // Bitshift
                        byte intensity = (byte)(ir >> 8);

                        // Assign infrared intensity
                        _infraPixels[colorPixelIndex++] = intensity;
                        _infraPixels[colorPixelIndex++] = intensity;
                        _infraPixels[colorPixelIndex++] = intensity;

                        ++colorPixelIndex;
                    }

                    // Copy output to bitmap
                    _infraBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _infraPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the body-frames and draw joints
        /// </summary>
        private void OnBodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            // Get frame reference
            BodyFrameReference refer = e.FrameReference;

            if (refer == null) return;

            // Get body frame
            BodyFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            using (frame)
            {
                // Aquire body data
                frame.GetAndRefreshBodyData(_bodies);

                // Clear Skeleton Canvas
                SkeletonCanvas.Children.Clear();

                // Loop all bodies
                foreach (Body body in _bodies)
                {
                    // Only process tracked bodies
                    if (body.IsTracked)
                    {
                        DrawBody(body);
                        if (recording)
                        {
                            UpdateBodyStats(body);
                            RecordBodyData(body);
                        }
                        
                    }
                }
            }
        }

        /// <summary>
        /// Visualize the body
        /// </summary>
        /// <param name="body">Tracked body</param>
        private void DrawBody(Body body)
        {
            // Draw points
            foreach (JointType type in body.Joints.Keys)
            {
                // Draw all the body joints
                switch (type)
                {
                    case JointType.Head:
                    case JointType.FootLeft:
                    case JointType.FootRight:
                        DrawJoint(body.Joints[type], 20, Brushes.Yellow, 2, Brushes.White);
                        break;
                    case JointType.ShoulderLeft:
                    case JointType.ShoulderRight:
                    case JointType.HipLeft:
                    case JointType.HipRight:
                        DrawJoint(body.Joints[type], 20, Brushes.YellowGreen, 2, Brushes.White);
                        break;
                    case JointType.ElbowLeft:
                    case JointType.ElbowRight:
                    case JointType.KneeLeft:
                    case JointType.KneeRight:
                        DrawJoint(body.Joints[type], 15, Brushes.LawnGreen, 2, Brushes.White);
                        break;
                    case JointType.HandLeft:
                        DrawHandJoint(body.Joints[type], body.HandLeftState, 20, 2, Brushes.White);
                        break;
                    case JointType.HandRight:
                        DrawHandJoint(body.Joints[type], body.HandRightState, 20, 2, Brushes.White);
                        break;
                    default:
                        DrawJoint(body.Joints[type], 15, Brushes.RoyalBlue, 2, Brushes.White);
                        break;
                }
            }
        }

        /// <summary>
        /// Draws a body joint
        /// </summary>
        /// <param name="joint">Joint of the body</param>
        /// <param name="radius">Circle radius</param>
        /// <param name="fill">Fill color</param>
        /// <param name="borderWidth">Thickness of the border</param>
        /// <param name="border">Color of the boder</param>
        private void DrawJoint(Joint joint, double radius, SolidColorBrush fill, double borderWidth, SolidColorBrush border)
        {
            if (joint.TrackingState != TrackingState.Tracked) return;
            
            // Map the CameraPoint to ColorSpace so they match
            ColorSpacePoint colorPoint = _kinect.CoordinateMapper.MapCameraPointToColorSpace(joint.Position);

            // Create the UI element based on the parameters
            Ellipse el = new Ellipse();
            el.Fill = fill;
            el.Stroke = border;
            el.StrokeThickness = borderWidth;
            el.Width = el.Height = radius;

            // Add the Ellipse to the canvas
            SkeletonCanvas.Children.Add(el);

            // Avoid exceptions based on bad tracking
            if (float.IsInfinity(colorPoint.X) || float.IsInfinity(colorPoint.X)) return;

            // Allign ellipse on canvas (Divide by 2 because image is only 50% of original size)
            Canvas.SetLeft(el, colorPoint.X / 2);
            Canvas.SetTop(el, colorPoint.Y / 2);
        }

        /// <summary>
        /// Draw a body joint for a hand and assigns a specific color based on the handstate
        /// </summary>
        /// <param name="joint">Joint representing a hand</param>
        /// <param name="handState">State of the hand</param>
        private void DrawHandJoint(Joint joint, HandState handState, double radius, double borderWidth, SolidColorBrush border)
        {
            switch (handState)
            {
                case HandState.Lasso:
                    DrawJoint(joint, radius, Brushes.Cyan, borderWidth, border);
                    break;
                case HandState.Open:
                    DrawJoint(joint, radius, Brushes.Green, borderWidth, border);
                    break;
                case HandState.Closed:
                    DrawJoint(joint, radius, Brushes.Red, borderWidth, border);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Record body data to selected file in csv format
        /// </summary>
        private void RecordBodyData(Body body)
        {
            
            // write to file...
            List<String> data = new List<String>();
            foreach (String item in order)
            {
                String typeDelimiterString = ":";
                char[] typeDelimiter = typeDelimiterString.ToCharArray();
                String[] splitItem = item.Split(typeDelimiter);
                if (item.Equals("Time"))
                {
                    DateTime now = DateTime.Now;
                    data.Add(now.ToString(@"M/d/yyyy hh:mm:ss tt"));
                }
                else if (splitItem[0].Equals("Joint"))
                {
                    JointType type = JointType.Head;
                    switch (splitItem[1]) {
                        case "Head":
                            break;
                        case "AnkleLeft":
                            type = JointType.AnkleLeft;
                            break;
                        case "AnkleRight":
                            type = JointType.AnkleRight;
                            break;
                        case "ElbowLeft":
                            type = JointType.ElbowLeft;
                            break;
                        case "ElbowRight":
                            type = JointType.ElbowRight;
                            break;
                        case "FootLeft":
                            type = JointType.FootLeft;
                            break;
                        case "FootRight":
                            type = JointType.FootRight;
                            break;
                        case "HandLeft":
                            type = JointType.HandLeft;
                            break;
                        case "HandRight":
                            type = JointType.HandRight;
                            break;
                        case "HandTipLeft":
                            type = JointType.HandTipLeft;
                            break;
                        case "HandTipRight":
                            type = JointType.HandTipRight;
                            break;
                        case "HipLeft":
                            type = JointType.HipLeft;
                            break;
                        case "HipRight":
                            type = JointType.HipRight;
                            break;
                        case "KneeLeft":
                            type = JointType.KneeLeft;
                            break;
                        case "KneeRight":
                            type = JointType.KneeRight;
                            break;
                        case "Neck":
                            type = JointType.Neck;
                            break;
                        case "ShoulderLeft":
                            type = JointType.ShoulderLeft;
                            break;
                        case "ShoulderRight":
                            type = JointType.ShoulderRight;
                            break;
                        case "SpineBase":
                            type = JointType.SpineBase;
                            break;
                        case "SpineMid":
                            type = JointType.SpineMid;
                            break;
                        case "SpineShoulder":
                            type = JointType.SpineShoulder;
                            break;
                        case "ThumbLeft":
                            type = JointType.ThumbLeft;
                            break;
                        case "ThumbRight":
                            type = JointType.ThumbRight;
                            break;
                        case "WristLeft":
                            type = JointType.WristLeft;
                            break;
                        case "WristRight":
                            type = JointType.WristRight;
                            break;
                        default:
                            return;
                    }
                    data.Add(body.Joints[type].Position.X + "." + body.Joints[type].Position.Y + "." + body.Joints[type].Position.Z);
                }
            }
            
            outstream.WriteLine(String.Join(",", data));

        }

        #endregion FRAME PROCESSING
        #region UI Methods
        private void OnNA(object sender, RoutedEventArgs e)
        {

        }

        private void OnRecord(object sender, RoutedEventArgs e)
        {
            if (initialized)
            {
                if (recording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }
            else
            {
                if (InitializeKinect())
                {
                    Status.Text = "Ready";
                    RecordText.Text = "Record";
                    initialized = true;
                }
            }
            
        }

        private void OnChooseDataFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DataFile.Text = openFileDialog1.FileName;
                outfile = openFileDialog1.FileName;
            }
        }
        
        /// <summary>
        /// Start recording and change record button to pause.
        /// </summary>
        private void StartRecording()
        {
            if (outfile.Equals(""))
            {
                // Consider throwing an error or displaying the problem - blank file path
            }
            else
            {
                // toggle status bits and change UI elements
                Status.Text = "Recording";
                RecordText.Text = "Pause";
                recording = true;

                // open file, initialize buffers, push data
                outstream = new StreamWriter(outfile, true);
                outstream.WriteLine(String.Join(",", order));
            }


        }

        /// <summary>
        /// Stop recording and change the record button to Record
        /// </summary>
        private void StopRecording()
        {
            // toggle status bits and change UI elements
            Status.Text = "Paused";
            RecordText.Text = "Record";
            recording = false;

            // close file
            outstream.Close();

        }

        /// <summary>
        /// Update body stats on the statistics panel.
        /// </summary>
        private void UpdateBodyStats(Body body)
        {
            // TrackingID, NumberofJoints...

        }
        #endregion UI Methods
    }
}

