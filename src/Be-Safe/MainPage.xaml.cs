//*********************************************************
//
// Copyright (c) Avikalp Srivastava & Madhav Datt. 
// All rights reserved.
// This code is licensed under the MIT License (MIT).
//
//*********************************************************
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.Devices.Sms;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using System.Threading.Tasks;
using Windows.UI.Core;
using System.Runtime.Serialization.Json;


using Windows.Storage;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.System.Display;

namespace Be_Safe
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage Current;
        private Accelerometer _accelerometer;
        private uint _desiredReportInterval;
        public double acc_x, acc_y, acc_z;
        public bool prev_xg = false, prev_yg = false, prev_zg = false;
        private Timer periodicTimer = null;


        // The variable HighAccEvent is changed to true only if the accelerometer reading goes above the threshold.
        // The value change to true triggers the sending of the SMS to the emergency contact number.
        public bool HighAccEvent = false;
        public bool NumberFed = false;
        public bool AppStarted = false;
        public bool GeoAccess = false;
        private DisplayRequest AppDisplayRequest;
        private long DisplayRequestRefCount = 0;


        /// private MainPage rootPage;
        private SmsDevice2 device;
        public Geoposition pos;

        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        private MainPage rootPage;

        // The string Mnumber stores the emergency contact number provided by the user.
        // Until entered, it is set as "default".
        //public string Mnumber = "default";
        public MobileNumber Mnum = null;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
            rootPage = Current;

            if (!NumberFed)
                this.set_data();

            _accelerometer = Accelerometer.GetDefault();
            if (_accelerometer != null)
            {
                // Select a report interval that is both suitable for the purposes of the app and supported by the sensor.
                // This value will be used later to activate the sensor.
                uint minReportInterval = _accelerometer.MinimumReportInterval;
                // Keeping Minimum Time between readings as 30 ms
                _desiredReportInterval = minReportInterval > 30 ? minReportInterval : 30;
                // Getting Access to geolocation
                AskGeolocatorPermission();
            }
            else
            {
                NotifyUser("No accelerometer found. This app is not compatible with your device.", NotifyType.ErrorMessage);
                SendButton.IsEnabled = false;
            }
            
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Empty
        }

        public static ToastNotification DisplayToast(string content)
        {
            string xml = $@"<toast activationType='foreground'>
                                            <visual>
                                                <binding template='ToastGeneric'>
                                                    <text>Be Safe</text>
                                                </binding>
                                            </visual>
                                        </toast>";

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            var binding = doc.SelectSingleNode("//binding");

            var el = doc.CreateElement("text");
            el.InnerText = content;
            binding.AppendChild(el); //Add content to notification

            var toast = new ToastNotification(doc);

            ToastNotificationManager.CreateToastNotifier().Show(toast); //Show the toast

            return toast;
        }


        public void MakeDisplayAccessRequest()
        {
            if (AppDisplayRequest == null)
            {
                // This call creates an instance of the displayRequest object
                AppDisplayRequest = new DisplayRequest();
            }

            // This call activates a display-required request. If successful, 
            // the screen is guaranteed not to turn off automatically due to user inactivity.	
            if (DisplayRequestRefCount < 10)
            {
                AppDisplayRequest.RequestActive();
                DisplayRequestRefCount++;
            }
            else
            {
                rootPage.NotifyUser("Error: Exceeded maximum display request active instant count (" + DisplayRequestRefCount + ")", NotifyType.ErrorMessage);
            }
        }

        public void ReleaseDisplayAccessRequest()
        {
            if (AppDisplayRequest != null && DisplayRequestRefCount > 0)
            {
                AppDisplayRequest.RequestRelease();
                DisplayRequestRefCount--;
            }
            else
            {
                rootPage.NotifyUser("Error : Display Request Fail.", NotifyType.ErrorMessage);
            }
        }

        // Clicking of the Start App button
        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // If the device does have an accelerometer
            if (_accelerometer != null)
            {
                if (AppStarted)
                {
                    periodicTimer.Dispose();
                    periodicTimer = null;
                    ReleaseDisplayAccessRequest();
                    SendButton.Content = "Start App";
                    AppStarted = false;
                    textBlock3.Text = "";
                    ScenarioOutput_X.Text = "X :";
                    ScenarioOutput_Y.Text = "Y :";
                    ScenarioOutput_Z.Text = "Z :";
                    rootPage.NotifyUser("App has been stopped", NotifyType.ErrorMessage);
                }
                else
                {
                    SendButton.Content = "Stop App";
                    AppStarted = true;
                    MakeDisplayAccessRequest();
                    rootPage.NotifyUser("App is Running. Please don't minimise until you complete your journey. Be Safe!", NotifyType.StatusMessage);
                    periodicTimer = new Timer(OnTimer, DateTime.Now, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(_desiredReportInterval));
                }
          
            }

            // No Accelerometer found. Notify the user.
            else
            {
                NotifyUser("No accelerometer found. This app is not compatible with your device.", NotifyType.ErrorMessage);
            }

        }


        private async void OnTimer(object state)
        {
            var startTime = (DateTime)state;
            var runningTime = Math.Round((DateTime.Now - startTime).TotalSeconds, 0);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                textBlock3.Text = "App Run Time : " + runningTime.ToString();
                await ReadAcc((int)runningTime);
            });


        }


        private async Task ReadAcc(int timerSeconds)
        {
            if (_accelerometer != null)
            {
                AccelerometerReading reading = _accelerometer.GetCurrentReading();
                if (reading != null)
                {
                    acc_x = Math.Abs(reading.AccelerationX);
                    acc_y = Math.Abs(reading.AccelerationY);
                    acc_z = Math.Abs(reading.AccelerationZ);
                    ScenarioOutput_X.Text = "X : " + String.Format("{0,5:0.00}", reading.AccelerationX);
                    ScenarioOutput_Y.Text = "Y : " + String.Format("{0,5:0.00}", reading.AccelerationY);
                    ScenarioOutput_Z.Text = "Z : " + String.Format("{0,5:0.00}", reading.AccelerationZ);


                    // TODO : CHECK CONDITIONS FOR THIS
                    // Critical Step. Any component of acceleration crossing the precalculated limit of 39.2 will trigger 
                    // the clicking of the StartApp button with the parameter HighAccEvent set as true.  
                    if ((acc_x >= 2.0 && !prev_xg) || (acc_y >= 2.0 && !prev_yg) || (acc_z >= 2.0 && !prev_zg))
                    {
                        Send_Click(null, null);
                        HighAccEvent = true;
                        MainPage.DisplayToast("High Acceleration Event Encountered");
                        await Get_GeoPosition();
                        await Send_Message();
                    }

                    else if (timerSeconds % 4 == 0)
                    {
                        if (acc_x > 0.80 && acc_x < 1.20)
                        {
                            prev_xg = true; prev_yg = false; prev_zg = false;
                        }
                        else if (acc_y > 0.80 && acc_y < 1.20)
                        {
                            prev_yg = true; prev_xg = false; prev_zg = false;
                        }
                        else if (acc_z > 0.80 && acc_z < 1.20)
                        {
                            prev_zg = true; prev_xg = false; prev_yg = false;
                        }
                    }

                }
            }
            else
            {
                rootPage.NotifyUser("No Accelerometer found. This app is not compatible with your device.", NotifyType.ErrorMessage);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter != null)
            {
                if (e.Parameter.ToString() == "Enable")
                {
                    Send_Click(null, null);
                }
                else if (e.Parameter.ToString() == "Disable")
                {
                    SendButton.Content = "Start App";
                    if (_accelerometer != null && NumberFed)
                        SendButton.IsEnabled = true;
                    AppStarted = false;
                    textBlock3.Text = "";
                }
            }

            if(NumberFed)
            {
                MessagetoUser.Text = "Your Emergency Contact number has been saved.";
                button.Content = "Change Number";
            }
        }

        private async void AskGeolocatorPermission()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            if(accessStatus != GeolocationAccessStatus.Allowed)
            {
                rootPage.NotifyUser("Please provide geolocation access for this app.", NotifyType.ErrorMessage);
                GeoAccess = false;
            }
            else
            {
                GeoAccess = true;
            }
        }


        private async Task Get_GeoPosition()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracyInMeters = 500;
            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                try
                {
                    pos = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), timeout: TimeSpan.FromSeconds(15));
                    // textBlock1.Text = pos.Coordinate.Point.Position.Latitude.ToString();
                    // textBlock1.Text = String.Format("{0,5:0.000}", pos.Coordinate.Point.Position.Latitude);
                    // textBlock2.Text = String.Format("{0,5:0.000}", pos.Coordinate.Point.Position.Longitude);

                }
                catch (System.UnauthorizedAccessException)
                {
                    rootPage.NotifyUser("Geolocation Disabled", NotifyType.StatusMessage);
                }
                catch (TaskCanceledException)
                {
                    rootPage.NotifyUser("Canceled", NotifyType.StatusMessage);
                }
                catch (Exception)
                {
                    rootPage.NotifyUser("Other Exception", NotifyType.ErrorMessage);
                }
            }
        }

        private async Task Send_Message(bool Emergency = true)
        {
            if (device == null)
            {
                try
                {
                    device = SmsDevice2.GetDefault();
                }
                catch (Exception ex)
                {
                    NotifyUser(ex.Message, NotifyType.ErrorMessage);
                    return;
                }
            }

            string msgStr = "";

            if (device != null)
            {
                try
                {
                    // Create a text message - set the entered destination number and message text.
                    SmsTextMessage2 msg = new SmsTextMessage2();

                    // TODO : What to do below?
                    msg.To = Mnum.countryCode + Mnum.tenDigits;
                    // msg.To = Mnum.tenDigits;

                    try
                    {
                        if (Emergency)
                            msg.Body = "The Be-Safe app on this user's device has encountered a high acceleration event. Since your number has been listed as the emergency contact by the user, it is highly recommended that an immediate call is made to assure their safety. User's Location Coordinates : " + pos.Coordinate.Latitude.ToString("0.00") + ", " + pos.Coordinate.Longitude.ToString("0.00");
                        else
                        {
                            msg.Body = "The Be-Safe app is extremely sorry. The user of this number is completely safe, the previous message was a false alarm!";
                        }
                    }
                    catch (Exception)
                    {
                        msg.Body = "Emergency alert! I have been in a possible accident detected automatically by a high acceleration event. Please help.";
                    }
                    // Send the message asynchronously
                    if (Emergency)
                        NotifyUser("Sending Message to emergency contact.", NotifyType.StatusMessage);

                    SmsSendMessageResult result = await device.SendMessageAndGetResultAsync(msg);

                    if (result.IsSuccessful)
                    {
                        msgStr = "";
                        msgStr += "Message sent to: " + Mnum.countryCode + Mnum.tenDigits + " (Predefined Emergency Number)" + System.Environment.NewLine;
                        try
                        {
                            if (Emergency)
                                msgStr += "The Be-Safe app on this user's device has encountered a high acceleration event. Since your number has been listed as the emergency contact by the user, it is highly recommended that an immediate call is made to assure their safety. User's Location Coordinates : " + pos.Coordinate.Latitude.ToString("0.00") + ", " + pos.Coordinate.Longitude.ToString("0.00") + System.Environment.NewLine;
                            else
                            {
                                msgStr += "The Be-Safe app is extremely sorry. The user of this number is completely safe, the previous message was a false alarm!";
                            }
                        }
                        catch (Exception)
                        {
                            msgStr += "Emergency alert! I have been in a possible accident detected automatically by a high acceleration event. Please help.";
                        }
                        if (Emergency)
                            MainPage.DisplayToast("Emergency Text Sent to " + Mnum.countryCode + Mnum.tenDigits);
                        else
                            MainPage.DisplayToast("False Alarm Message Sent");

                        IReadOnlyList<Int32> messageReferenceNumbers = result.MessageReferenceNumbers;
                        NotifyUser(msgStr, NotifyType.StatusMessage);
                        if (!Emergency)
                        {
                            FalseAlarmButton.Visibility = Visibility.Collapsed;
                            textBlock3.Text = "False Alarm Message Sent. Sorry for the inconvenience.";
                        }
                        else
                        {
                            FalseAlarmButton.Visibility = Visibility.Visible;
                        }
                    }

                    else
                    {
                        msgStr = "";
                        msgStr += "ModemErrorCode: " + result.ModemErrorCode.ToString();
                        msgStr += "\nIsErrorTransient: " + result.IsErrorTransient.ToString();
                        if (result.ModemErrorCode == SmsModemErrorCode.MessagingNetworkError)
                        {
                            msgStr += "\n\tNetworkCauseCode: " + result.NetworkCauseCode.ToString();

                            if (result.CellularClass == CellularClass.Cdma)
                            {
                                msgStr += "\n\tTransportFailureCause: " + result.TransportFailureCause.ToString();
                            }
                            NotifyUser(msgStr, NotifyType.ErrorMessage);
                        }
                    }

                }
                catch (Exception ex)
                {
                    NotifyUser(ex.Message, NotifyType.ErrorMessage);
                }
            }
            else
            {
                if (Mnum != null)
                    NotifyUser("Could not connect to network. SMS could not be sent", NotifyType.ErrorMessage);
                else
                    NotifyUser("You have not yet entered your emergency contact number!", NotifyType.ErrorMessage);
            }

        }


        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (AppStarted)
            {
                Send_Click(null, null);
            }
            Frame.Navigate(typeof(WelcomePage));
        }

        public async void set_data()
        {
            await deserializeJsonAsync();
        }

        private const string JSONFILENAME = "temp6.json";

        private async Task deserializeJsonAsync()
        {
            try
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(MobileNumber));
                var myStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync(JSONFILENAME);
                Mnum = (MobileNumber)jsonSerializer.ReadObject(myStream);
                MessagetoUser.Text = "Your Emergency Contact number has been saved.";
                // Emergency number has been fed by user
                NumberFed = true;
                button.Content = "Change Number";
                if (_accelerometer != null)
                {
                    SendButton.IsEnabled = true;
                    rootPage.NotifyUser("", NotifyType.StatusMessage);
                }
            }
            catch (System.Exception)
            {
                MessagetoUser.Text = "Please enter your emergency contact number to get started!";
                SendButton.IsEnabled = false;
                NumberFed = false;
                Mnum = null;
                button.Content = "Enter Number";
                if (_accelerometer != null)
                    rootPage.NotifyUser("Please enter your emergency contact number to get started.", NotifyType.ErrorMessage);
            }

        }

        private async void FalseAlarmButton_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser("Sending False Alarm Message!", NotifyType.StatusMessage);
            await Send_Message(false);
        }


        /// <summary>
        /// Used to display messages to the user
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    ErrorBorder.Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    ErrorBorder.Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            ErrorBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBlock.Visibility = Visibility.Visible;
                ErrorBorder.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBlock.Visibility = Visibility.Collapsed;
                ErrorBorder.Visibility = Visibility.Collapsed;
            }
        }

        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };

    }

}

