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
                mainvehicle.Text = $"VEHICLE {data.Vehicle}";
                mainvehiclestatus.Text =
                    "  /  " + (data.Role ?? "UNKNOWN").ToUpperInvariant();

                VehicleIdText.Text =data.Vehicle == "A" ? "VEHICLE B" : "VEHICLE A";
                VehicleRoleText.Text = data.Role.ToUpperInvariant() == "FRONT VEHICLE" ? "REAR VEHICLE" : "FRONT VEHICLE";
                proxmitystatus.Text = data.Role.ToUpperInvariant() == "FRONT VEHICLE" ? "REVERSE PROXIMITY" : "FORWARD PROXIMITY";

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

                //ConnectionText.Text = "JSON CONNECTED";
                //ConnectionText.Foreground = Brushes.LightGreen;
            });
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
            try
            {

                sm.setSerialPort("COM6", 115200, "None", 8, 2);// Connec to Serial Port 
                if (sm.Serialstatus())
                {
                    Enablethread();
                    ConnectionText.Text = "CONNECTED";
                    ConnectionText.Foreground = Brushes.LightGreen;
                }
                else
                {
                    ConnectionText.Text = "DISCONNECTED";
                    ConnectionText.Foreground = Brushes.OrangeRed;
                }
            }
            catch
            {

            }
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
