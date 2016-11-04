//*********************************************************
//
// Copyright (c) Avikalp Srivastava & Madhav Datt. 
// All rights reserved.
// This code is licensed under the MIT License (MIT).
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using Windows.Storage;
using System.Text.RegularExpressions;

namespace Be_Safe
{
    /// <summary>
    /// This page displays the welcome message to the user,
    /// and urges the user to provide an emergency contact nummber if one has not yet been provided.
    /// </summary>
    public sealed partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Whenever this page is navigated to, the page tries to retrieve the user provided emergency contact number
        /// and display it, providing the user with an option to edit/change
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            EnterNumberButton.IsEnabled = false;
            MobileNumber Number = null;
            if (MainPage.Current.Mnum == null)
                await deserializeJsonAsync(Number);
            // EnterButtonAction(Number);
            else
            {
                Number = MainPage.Current.Mnum;
                CountryCodeTextBox.Text = Number.countryCode;
                WelcometextBox.Text = Number.tenDigits;
                InvalidNumberMessageTextBlock.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
                InvalidNumberMessageTextBlock.Text = "The above number is your current emergency contact.";
            }
        }

        private void EnterButtonAction(MobileNumber Number)
        {
            MobileNumber num = MainPage.Current.Mnum;
            if (num == null)
            {
                if (isMobileNumberValid(Number.countryCode, Number.tenDigits))
                {
                    EnterNumberButton.IsEnabled = true;
                }
                else
                {
                    EnterNumberButton.IsEnabled = false;
                }
                return;
            }

            if (Number == null || num.countryCode != CountryCodeTextBox.Text || num.tenDigits != WelcometextBox.Text)
            {
                InvalidNumberMessageTextBlock.Text = "";
                if (Number != null && isMobileNumberValid(Number.countryCode, Number.tenDigits))
                {
                    EnterNumberButton.IsEnabled = true;
                }
                else
                {
                    EnterNumberButton.IsEnabled = false;
                }
            }
            else
            {
                InvalidNumberMessageTextBlock.Text = "The above number is your current emergency contact.";
                EnterNumberButton.IsEnabled = false;
            }
        }

        // Enter Number Button Click
        private async void WelcomeButton_click(object sender, RoutedEventArgs e)
        {

            // Check the validity of the formt of the number entered by the user, and pass it as an object to SendTexyMessage Page.
            bool error = false;
            // Getting the number entered by the user
            string mNumber = WelcometextBox.Text;
            // Getting Country Code
            string cCode = CountryCodeTextBox.Text;
            // Appending to get user's full number
            string userNumber = cCode + mNumber;

            error = !isMobileNumberValid(cCode, mNumber);

            // Erroneous Number entered by user
            if (error)
            {
                InvalidNumberMessageTextBlock.Text = "You have not entered a valid mobile number! Please check and try again!";
            }
            // Else the entered number has a valid format, serialize it.
            else
            {
                MobileNumber Number = new MobileNumber()
                {
                    countryCode = cCode,
                    tenDigits = mNumber
                };

                MainPage.Current.Mnum = Number;
                MainPage.Current.NumberFed = true;

                // Serialising the entered number
                await writeJsonAsync(Number);

                // Number saved, now moving back to MainPage
                Frame.Navigate(typeof(MainPage), "Disable");
            }

        }

        private void WelcometextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isMobileNumberValid(CountryCodeTextBox.Text, WelcometextBox.Text))
            {
                GreenTick.Visibility = Windows.UI.Xaml.Visibility.Visible;
                RedCross.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                GreenTick.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                RedCross.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            MobileNumber temp = new MobileNumber() { countryCode = CountryCodeTextBox.Text, tenDigits = WelcometextBox.Text };
            EnterButtonAction(temp);
        }

        public bool isMobileNumberValid(string cCode, string mNumber)
        {
            // Regex to match valid Internation Mobile Number formats
            Regex mNumberRegex = new Regex(@"^[0-9]{3,11}$");
            Regex cCodeRegex = new Regex(@"^\+[1-9]{1,3}$");

            if(cCode == "+86")
            {
                return false;
            }

            if (!mNumberRegex.IsMatch(mNumber) || !cCodeRegex.IsMatch(cCode))
            {
                return false;
            }
            else
            {
                if (cCode == "+91" && mNumber.Length != 10)
                    return false;
                else
                    return true;
            }
        }

        private const string JSONFILENAME = "temp6.json";

        private async Task writeJsonAsync(MobileNumber Number)
        {
            var serializer = new DataContractJsonSerializer(typeof(MobileNumber));
            using (var stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(
                          JSONFILENAME,
                          CreationCollisionOption.ReplaceExisting))
            {
                serializer.WriteObject(stream, Number);
            }
            InvalidNumberMessageTextBlock.Text = "Number Successfully Saved!";
        }

        private async Task deserializeJsonAsync(MobileNumber Number)
        {
            try
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(MobileNumber));
                var myStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync(JSONFILENAME);
                Number = (MobileNumber)jsonSerializer.ReadObject(myStream);
                CountryCodeTextBox.Text = Number.countryCode;
                WelcometextBox.Text = Number.tenDigits;
                InvalidNumberMessageTextBlock.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Green);
                InvalidNumberMessageTextBlock.Text = "The above number is your current emergency contact.";
            }
            catch (System.Exception)
            {
                InvalidNumberMessageTextBlock.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Red);
                InvalidNumberMessageTextBlock.Text = "No number saved as of now.";
            }

        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainPage.Current.AppStarted)
            {
                Frame.Navigate(typeof(MainPage), "Enable");
            }
            else
            {
                Frame.Navigate(typeof(MainPage), "Disable");
            }
        }
    }
}
