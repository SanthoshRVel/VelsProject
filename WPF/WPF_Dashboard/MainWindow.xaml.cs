using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WPF_Dashboard.Serial_manager;

namespace WPF_Dashboard
{
    public partial class MainWindow : Window
    {
        private const double MaxSpeed = 220;
        private double _speed;
        SerialManager sm = new SerialManager();  // Declare a SerialManager Class File
        private CancellationTokenSource serialCts;
        Brush safeBody = new SolidColorBrush(Color.FromRgb(16, 44, 66));
        Brush safeAccent = new SolidColorBrush(Color.FromRgb(0, 217, 255));
        Brush safeWindow = new SolidColorBrush(Color.FromRgb(27, 102, 129));
        Brush safeWindowStroke = new SolidColorBrush(Color.FromRgb(101, 232, 255));

        Brush warningBody = new SolidColorBrush(Color.FromRgb(65, 42, 15));
        Brush warningAccent = new SolidColorBrush(Color.FromRgb(255, 159, 28));
        Brush warningWindow = new SolidColorBrush(Color.FromRgb(125, 77, 27));

        Brush dangerBody = new SolidColorBrush(Color.FromRgb(65, 25, 35));
        Brush dangerAccent = new SolidColorBrush(Color.FromRgb(255, 64, 87));
        Brush dangerWindow = new SolidColorBrush(Color.FromRgb(123, 45, 55));

        Brush body;
        Brush accent;
        Brush window;
        bool isconnected = false;


        public MainWindow()
        {
            InitializeComponent();

            DrawGauge();
            SetSpeed(0);
        }

        private void DrawGauge()
        {
            //GaugeCanvas.Children.Clear();

            double cx = 250;
            double cy = 250;
            double radius = 205;

            for (int speed = 0; speed <= MaxSpeed; speed += 10)
            {
                double angle =
                    (-130 + (speed / MaxSpeed) * 260)
                    * Math.PI / 180.0;

                double innerRadius =
                    speed % 20 == 0 ? radius - 28 : radius - 17;

                double x1 = cx + innerRadius * Math.Sin(angle);
                double y1 = cy - innerRadius * Math.Cos(angle);
                double x2 = cx + radius * Math.Sin(angle);
                double y2 = cy - radius * Math.Cos(angle);

                //GaugeCanvas.Children.Add(new System.Windows.Shapes.Line
                //{
                //    X1 = x1,
                //    Y1 = y1,
                //    X2 = x2,
                //    Y2 = y2,
                //    Stroke = speed > 180
                //        ? Brushes.OrangeRed
                //        : Brushes.LightCyan,
                //    StrokeThickness = speed % 20 == 0 ? 3 : 1.5
                //});

                if (speed % 20 == 0)
                {
                    double labelRadius = radius - 52;
                    double lx = cx + labelRadius * Math.Sin(angle);
                    double ly = cy - labelRadius * Math.Cos(angle);

                    var label = new TextBlock
                    {
                        Text = speed.ToString(),
                        Foreground = Brushes.White,
                        FontSize = 15,
                        Width = 40,
                        TextAlignment = TextAlignment.Center
                    };

                    Canvas.SetLeft(label, lx - 20);
                    Canvas.SetTop(label, ly - 10);

                    //GaugeCanvas.Children.Add(label);
                }
            }
        }

        private void SetSpeed(double targetSpeed)
        {
            targetSpeed = Math.Max(0, Math.Min(MaxSpeed, targetSpeed));
            _speed = targetSpeed;

            double targetAngle =
                -130 + (targetSpeed / MaxSpeed) * 260;

            var animation = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            //NeedleRotation.BeginAnimation(
            //    System.Windows.Media.RotateTransform.AngleProperty,
            //    animation);

            //SpeedText.Text = ((int)Math.Round(targetSpeed)).ToString();
        }

        private void AccelerateButton_Click(
            object sender, RoutedEventArgs e)
        {
            SetSpeed(_speed + 20);
        }

        private void BrakeButton_Click(
            object sender, RoutedEventArgs e)
        {
            SetSpeed(_speed - 20);
        }

        private void StopButton_Click(
            object sender, RoutedEventArgs e)
        {
            SetSpeed(0);
        }

        // Call this method whenever new vehicle JSON is received.
        public void UpdateFromJson(VehicleMessage data)
        {
            if (data?.Local == null || data.Peer == null)
                return;

            // UI elements must be updated on the WPF UI thread.
            Dispatcher.Invoke(() =>
            {
                VehicleIdText.Text = $"VEHICLE {data.Vehicle}";
                VehicleRoleText.Text =
                    (data.Role ?? "UNKNOWN").ToUpperInvariant();

                mainvehicle.Text =data.Vehicle == "A" ? "VEHICLE B" : "VEHICLE A";
                mainvehiclestatus.Text = data.Role.ToUpperInvariant() == "FRONT VEHICLE" ? " / REAR VEHICLE" : " / FRONT VEHICLE";
                proxmitystatus.Text = data.Role.ToUpperInvariant() != "FRONT VEHICLE" ? "FORWARD PROXIMITY" : "REVERSE PROXIMITY";

                LocalSpeedText.Text =
                    $"{data.Local.SpeedKmh:0.0} km/h";

                LocalDistanceText.Text =
                    $"{data.Local.DistanceM:0.0} m";

                LocalTtcText.Text =
                    $"{data.Local.TtcS:0.00} s";

                PeerSpeedText.Text =
                    $"{data.Peer.SpeedKmh:0.0} km/h";

                PeerDistanceText.Text =
                    $"{data.Peer.DistanceM:0.0} m";

                PeerTtcText.Text =
                    $"{data.Peer.TtcS:0.00} s";

                SetSpeed(data.Local.SpeedKmh);
                UpdateClusterIndicators(data);
                UpdateVehicleColors(data);

                //ConnectionText.Text = "JSON CONNECTED";
                //ConnectionText.Foreground = Brushes.LightGreen;
            });
        }


        //private void UpdateVehicleColors(VehicleMessage data)
        //{
        //    // Peer vehicle
        //    bool peerWarning =
        //        data.Peer.BrakeActive ||
        //        data.Peer.ObstacleDetected;

        //    Brush peerBody = new SolidColorBrush(
        //        peerWarning
        //            ? Color.FromRgb(75, 25, 35)
        //            : Color.FromRgb(53, 37, 42));

        //    Brush peerAccent = new SolidColorBrush(
        //        peerWarning
        //            ? Color.FromRgb(255, 64, 87)
        //            : Color.FromRgb(255, 83, 103));

        //    PeerVehicleVisual.Background = peerBody;
        //    PeerVehicleVisual.BorderBrush = peerAccent;
        //    PeerWindow.Fill = new SolidColorBrush(
        //        peerWarning
        //            ? Color.FromRgb(123, 45, 55)
        //            : Color.FromRgb(123, 65, 74));
        //    PeerWindow.Stroke = new SolidColorBrush(
        //        peerWarning
        //            ? Color.FromRgb(255, 135, 147)
        //            : Color.FromRgb(255, 135, 147));
        //    PeerFrontLight.Fill = peerAccent;
        //    PeerRearLight.Fill = peerAccent;

        //    // Local vehicle
        //    bool localWarning =
        //        data.Local.BrakeActive ||
        //        data.Local.ObstacleDetected;

        //    Brush localBody = new SolidColorBrush(
        //        localWarning
        //            ? Color.FromRgb(65, 42, 15)
        //            : Color.FromRgb(16, 44, 66));

        //    Brush localAccent = new SolidColorBrush(
        //        localWarning
        //            ? Color.FromRgb(255, 159, 28)
        //            : Color.FromRgb(0, 217, 255));

        //    LocalVehicleVisual.Background = localBody;
        //    LocalVehicleVisual.BorderBrush = localAccent;
        //    LocalWindow.Fill = new SolidColorBrush(
        //        localWarning
        //            ? Color.FromRgb(125, 77, 27)
        //            : Color.FromRgb(27, 102, 129));
        //    LocalWindow.Stroke = localAccent;
        //    LocalFrontLight.Fill = localAccent;
        //    LocalRearLight.Fill = localAccent;
        //}

        private void UpdateVehicleColors(VehicleMessage data)
        {
            bool isFrontVehicle = string.Equals(
                data.Role,
                "FRONT VEHICLE",
                StringComparison.OrdinalIgnoreCase);

            if (!isFrontVehicle)
            {
                // Our vehicle is the front vehicle: change PeerVehicleVisual
                UpdatePeerVehicleColor(data);
            }
            else
            {
                // Our vehicle is not the front vehicle: change LocalVehicleVisual
                UpdateLocalVehicleColor(data);
            }
        }

        private void UpdatePeerVehicleColor(VehicleMessage data)
        {
            //bool warning = data.Local.BrakeActive ||
            //               data.Local.ObstacleDetected;

            //PeerVehicleVisual.Background = new SolidColorBrush(
            //    warning ? Color.FromRgb(75, 25, 35)
            //            : Color.FromRgb(53, 37, 42));

            //Brush accent = new SolidColorBrush(
            //    warning ? Color.FromRgb(255, 64, 87)
            //            : Color.FromRgb(255, 83, 103));

            //PeerVehicleVisual.BorderBrush = accent;
            //PeerWindow.Fill = new SolidColorBrush(
            //    warning ? Color.FromRgb(123, 45, 55)
            //            : Color.FromRgb(123, 65, 74));
            //PeerFrontLight.Fill = accent;
            //PeerRearLight.Fill = accent;

            

            switch (data.Status.ToUpperInvariant())
            {
                case "WARNING":
                    body = warningBody;
                    accent = warningAccent;
                    window = warningWindow;
                    break;
                case "DANGER":
                    body = dangerBody;
                    accent = dangerAccent;
                    window = dangerWindow;
                    break;
                case "SAFE":
                default:
                    body = safeBody;
                    accent = safeAccent;
                    window = safeWindow;
                    break;
            }
            PeerVehicleVisual.Background = body;
            PeerVehicleVisual.BorderBrush = accent;
            PeerWindow.Fill = window;
            PeerWindow.Stroke = data.Status.ToUpperInvariant() == "SAFE"
                ? safeWindowStroke
                : accent;
            PeerFrontLight.Fill = accent;
            PeerRearLight.Fill = accent;


            // Local vehicle: always safe cyan
            //Brush safeBody = new SolidColorBrush(Color.FromRgb(16, 44, 66));
            //Brush safeAccent = new SolidColorBrush(Color.FromRgb(0, 217, 255));

            LocalVehicleVisual.Background = safeBody;
            LocalVehicleVisual.BorderBrush = safeAccent;
            LocalWindow.Fill = new SolidColorBrush(Color.FromRgb(27, 102, 129));
            LocalWindow.Stroke = new SolidColorBrush(Color.FromRgb(101, 232, 255));
            LocalFrontLight.Fill = safeAccent;
            LocalRearLight.Fill = safeAccent;
        }

        private void UpdateLocalVehicleColor(VehicleMessage data)
        {
            //bool warning = data.Local.BrakeActive ||
            //               data.Local.ObstacleDetected;

            //LocalVehicleVisual.Background = new SolidColorBrush(
            //    warning ? Color.FromRgb(65, 42, 15)
            //            : Color.FromRgb(16, 44, 66));

            //Brush accent = new SolidColorBrush(
            //    warning ? Color.FromRgb(255, 159, 28)
            //            : Color.FromRgb(0, 217, 255));

            //LocalVehicleVisual.BorderBrush = accent;
            //LocalWindow.Fill = new SolidColorBrush(
            //    warning ? Color.FromRgb(125, 77, 27)
            //            : Color.FromRgb(27, 102, 129));
            //LocalWindow.Stroke = accent;
            //LocalFrontLight.Fill = accent;
            //LocalRearLight.Fill = accent;

            //// Local vehicle: always safe cyan
            //Brush safeBody = new SolidColorBrush(Color.FromRgb(16, 44, 66));
            //Brush safeAccent = new SolidColorBrush(Color.FromRgb(0, 217, 255));

            switch (data.Status.ToUpperInvariant())
            {
                case "WARNING":
                    body = warningBody;
                    accent = warningAccent;
                    window = warningWindow;
                    break;
                case "DANGER":
                    body = dangerBody;
                    accent = dangerAccent;
                    window = dangerWindow;
                    break;
                case "SAFE":
                default:
                    body = safeBody;
                    accent = safeAccent;
                    window = safeWindow;
                    break;
            }
            LocalVehicleVisual.Background = body;
            LocalVehicleVisual.BorderBrush = accent;
            LocalWindow.Fill = window;
            LocalWindow.Stroke = data.Status.ToUpperInvariant() == "SAFE"
                ? safeWindowStroke
                : accent;
            LocalFrontLight.Fill = accent;
            LocalRearLight.Fill = accent;

            PeerVehicleVisual.Background = safeBody;
            PeerVehicleVisual.BorderBrush = safeAccent;
            PeerWindow.Fill = new SolidColorBrush(Color.FromRgb(27, 102, 129));
            PeerWindow.Stroke = new SolidColorBrush(Color.FromRgb(101, 232, 255));
            PeerFrontLight.Fill = safeAccent;
            PeerRearLight.Fill = safeAccent;
        }

        private void UpdateClusterIndicators(VehicleMessage data)
        {
            bool danger = string.Equals(
                data.Status,
                "DANGER",
                StringComparison.OrdinalIgnoreCase);

            StatusText.Text =
                (data.Status ?? "UNKNOWN").ToUpperInvariant();

            StatusText.Foreground = danger
                ? Brushes.OrangeRed
                : Brushes.LightGreen;

            CollisionTelltale.Opacity = danger ? 1.0 : 0.30;

            LocalBrakeText.Text = data.Local.BrakeActive
                ? "BRAKE ACTIVE"
                : "BRAKE INACTIVE";

            LocalBrakeText.Foreground = data.Local.BrakeActive
                ? Brushes.OrangeRed
                : Brushes.LightGreen;

            LocalBrakeBadge.Opacity =
                data.Local.BrakeActive ? 1.0 : 0.35;

            LocalObstacleText.Text = data.Local.ObstacleDetected
                ? "OBSTACLE DETECTED"
                : "OBSTACLE CLEAR";

            LocalObstacleText.Foreground =
                data.Local.ObstacleDetected
                    ? Brushes.Orange
                    : Brushes.LightGreen;

            LocalObstacleBadge.Opacity =
                data.Local.ObstacleDetected ? 1.0 : 0.35;

            SteeringTelltale.Opacity =
                data.Local.SteerAdvisory != 0 ? 1.0 : 0.35;

            SteerText.Text = data.Local.SteerAdvisory == 0 ? "Straight" : data.Local.SteerAdvisory == 1 ? "Left" : "Right";

            //PeerBrakeText.Text = data.Peer.BrakeActive
            //    ? "PEER BRAKING"
            //    : "PEER TRACKING";

            PeerBrakeBadge.Opacity =
                data.Peer.BrakeActive ? 1.0 : 0.65;

            SpeedDifferenceText.Text =
                $"{data.Local.SpeedKmh - data.Peer.SpeedKmh:+0.0;-0.0;0.0} km/h";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //try
            //{

            //    sm.setSerialPort("COM6", 115200, "None", 8, 2);// Connec to Serial Port 
            //    if (sm.Serialstatus())
            //    {
            //        Enablethread();
            //        ConnectionText.Text = "CONNECTED";
            //        ConnectionText.Foreground = Brushes.LightGreen;
            //    }
            //    else
            //    {
            //        ConnectionText.Text = "DISCONNECTED";
            //        ConnectionText.Foreground = Brushes.OrangeRed;
            //    }
            //}
            //catch
            //{

            //}
        }


        private async void Enablethread()
        {

            serialCts = new CancellationTokenSource();

            await Task.Run(() => SerialReadLoop(serialCts.Token));
        }

        private void SerialReadLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string json = sm.Read();

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Read_serial(json);
                        }));
                    }
                }
                catch (TimeoutException)
                {
                    // No complete line yet; keep waiting
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Console.WriteLine("UART error: " + ex.Message);
                    }));
                    break;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            serialCts?.Cancel();

            //if (sm != null)
            //{
            //    if (sm.IsOpen)
            //        sm.Close();

            //    sm.Dispose();
            //}

            serialCts?.Dispose();

            base.OnClosed(e);
        }

        private void OnSerialDataReceived(string json)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Read_serial(json);
            }));
        }

        public void Read_serial(string msg)
        {
            VehicleMessage data = new VehicleMessage();
            try
            {
                data =
          JsonSerializer.Deserialize<VehicleMessage>(msg);
                if (data != null)
                    UpdateFromJson(data);
            }
            catch
            { }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    if (combo1.SelectedIndex != -1 && !isconnected)
                    {
                        string PortName = combo1.Text;// Get the Com Port Name 
                        sm.setSerialPort("COM6", 115200, "None", 8, 2);// Connec to Serial Port 
                        if (sm.Serialstatus())
                        {
                            Enablethread();
                            ConnectionText.Text = "CONNECTED";
                            ConnectionText.Foreground = Brushes.LightGreen;
                            isconnected = true;
                        }
                        else
                        {
                            ConnectionText.Text = "DISCONNECTED";
                            ConnectionText.Foreground = Brushes.OrangeRed;
                            isconnected = false;
                        }
                    }
                    else if (isconnected == true)
                    {
                        sm.closeSerialPort();
                        serialCts?.Cancel();
                        isconnected = false;
                        serialCts?.Dispose();
                        ConnectionText.Text = "DISCONNECTED";
                        ConnectionText.Foreground = Brushes.OrangeRed;
                    }
                }
                catch
                {

                }
                finally
                {
                    ConnectButton.Content = isconnected ? "DISCONNECT" : "CONNECT";
                }
            }
            catch { }
        }

        private void dropdownevent(object sender, EventArgs e)
        {
            try
            {
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                combo1.Items.Clear();

                foreach (string port in ports)
                {
                    combo1.Items.Add(port);
                }
            }
            catch { }
        }
    }

    public class VehicleMessage
    {
        [JsonPropertyName("vehicle")]
        public string Vehicle { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("local")]
        public LocalVehicle Local { get; set; }

        [JsonPropertyName("peer")]
        public PeerVehicle Peer { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }

    public class LocalVehicle
    {
        [JsonPropertyName("speed_kmh")]
        public double SpeedKmh { get; set; }

        [JsonPropertyName("distance_m")]
        public double DistanceM { get; set; }

        [JsonPropertyName("brake_active")]
        public bool BrakeActive { get; set; }

        [JsonPropertyName("obstacle_detected")]
        public bool ObstacleDetected { get; set; }

        [JsonPropertyName("ttc_s")]
        public double TtcS { get; set; }

        [JsonPropertyName("steer_advisory")]
        public int SteerAdvisory { get; set; }
    }

    public class PeerVehicle
    {
        [JsonPropertyName("speed_kmh")]
        public double SpeedKmh { get; set; }

        [JsonPropertyName("distance_m")]
        public double DistanceM { get; set; }

        [JsonPropertyName("brake_active")]
        public bool BrakeActive { get; set; }

        [JsonPropertyName("obstacle_detected")]
        public bool ObstacleDetected { get; set; }

        [JsonPropertyName("ttc_s")]
        public double TtcS { get; set; }
    }
}
