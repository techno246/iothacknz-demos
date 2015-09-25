using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// This application uses a PIR sensor on GPIO26 to trigger an alarm sound and email alert 

namespace PiSecurityHeaded
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Setup pins
        private const int sensorPin = 26;
        private GpioPin pin;

        // Email client
        EmailClient emailClient;

        public MainPage()
        {
            // This line comes by default
            this.InitializeComponent();

            // Initialise the GPIO pins we will be using
            InitGPIO();

            // Init email client object
            emailClient = new EmailClient();

            // Attached an event handler to the ValueChanged event
            pin.ValueChanged += Pin_ValueChanged;
        }

        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            // Only respond when sensor is going from low to high (rising edge). This is because this event is called when the value changes either way. 
            if (args.Edge == GpioPinEdge.RisingEdge)
            {
                // Setup email client and send email (hardcoded address) 
                emailClient.SendMail("ming.cheuk@studentpartner.com");

                // Try playing audio
                try
                {
                    playRecordedAudio();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private void InitGPIO()
        {
            // Open the pin and set it to input (since we are reading from the device, not writing to) 
            pin = GpioController.GetDefault().OpenPin(sensorPin);
            pin.SetDriveMode(GpioPinDriveMode.Input);
        }

        private async void playRecordedAudio()
        {
            // Load file in assets folder
            string CountriesFile = @"Assets\TornadoSiren.mp3";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = await InstallationFolder.GetFileAsync(CountriesFile);
            var stream = await file.OpenAsync(FileAccessMode.Read);

            // Run this on the UI thread (playRecordedAudio() is called in a background thread. mediaElement must run on the UI thread)
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                if (null != file)
                {
                    mediaElement.SetSource(stream, file.ContentType);
                    mediaElement.Play();
                }
            });
        }
    }
}
