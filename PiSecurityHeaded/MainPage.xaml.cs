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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PiSecurityHeaded
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private GpioPinValue value = GpioPinValue.Low;
        private const int sensorPin = 26;
        private GpioPin pin;

        public MainPage()
        {
            this.InitializeComponent();
            InitGPIO();
            pin.ValueChanged += Pin_ValueChanged;
        }

        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge == GpioPinEdge.RisingEdge)
            {
                var client = new EmailClient();
                client.SendMail();

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
            pin = GpioController.GetDefault().OpenPin(sensorPin);
            pin.SetDriveMode(GpioPinDriveMode.Input);
        }

        private async void playRecordedAudio()
        {
            string CountriesFile = @"Assets\TornadoSiren.mp3";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = await InstallationFolder.GetFileAsync(CountriesFile);
            var stream = await file.OpenAsync(FileAccessMode.Read);

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
