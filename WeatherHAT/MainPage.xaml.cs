﻿using System;
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

using Emmellsoft.IoT.Rpi.SenseHat;
using System.Threading.Tasks;
using Emmellsoft.IoT.Rpi.SenseHat.Fonts.BW;
using Windows.UI;
using System.Net.Http;
using Newtonsoft.Json;
using MSAWeather;
using SenseHatDemo;
using System.Threading;
using Emmellsoft.IoT.Rpi.SenseHat.Fonts;
using Windows.System.Threading;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SenseHatDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Periodic routine timers
        ThreadPoolTimer joystickPollTimer;
        ThreadPoolTimer updateSensorsTimer;
        ThreadPoolTimer forecastUpdateTimer;

        // Variable that contains current forecasted weather
        WeatherData.Rootobject weatherObject = null;
        string currentCity = "Auckland";

        // Sensehat objects
        ISenseHat senseHat;
        ISenseHatDisplay display;
        TinyFont tinyFont;
        TextScroller<BwCharacter> textScroller;

        public MainPage()
        {
            this.InitializeComponent();

            Task.Run(async () =>
            {
                await initSenseHat();
            });
        }

        public async Task initSenseHat()
        {

            // Create sensehat object
            senseHat = await SenseHatFactory.Singleton.Create();

            // Init font
            tinyFont = new TinyFont();
            // Init display
            display = senseHat.Display;
            // Get a copy of the rainbow colors.
            senseHat.Display.Reset();
            // Recreate the font from the serialized bytes.
            BwFont font = BwFont.Deserialize(FontBytes);
            // Get the characters to scroll.
            IEnumerable<BwCharacter> characters = font.GetChars("Error");
            // Create the character renderer.
            BwCharacterRenderer characterRenderer = new BwCharacterRenderer(GetCharacterColor);
            // Create the text scroller.
            textScroller = new TextScroller<BwCharacter>(senseHat.Display, characterRenderer, characters);


            // Update forecast for first time
            getWeather(currentCity);

            // Check joystick every 100 ms
            joystickPollTimer = ThreadPoolTimer.CreatePeriodicTimer(pollJoystick, TimeSpan.FromMilliseconds(50));
            // Check sensor every 2 s
            updateSensorsTimer = ThreadPoolTimer.CreatePeriodicTimer(updateSensors, TimeSpan.FromSeconds(2));
            // Get updated weather every 1 minute
            forecastUpdateTimer = ThreadPoolTimer.CreatePeriodicTimer(updateForecast, TimeSpan.FromMinutes(1));
        }

        // Check state of joystick
        private void pollJoystick(ThreadPoolTimer timer)
        {
            try
            {
                if (senseHat.Joystick.Update() && senseHat.Joystick.EnterKey == KeyState.Pressed) // Has any of the buttons on the joystick changed?
                {
                    SwitchToNextScrollMode();
                    updateScreen();
                }
            }
            catch (Exception e)
            {

            }
        }

        // Try get weather forecast
        private async void updateForecast(ThreadPoolTimer timer)
        {
            await getWeather(currentCity);
            updateScreen();
        }

        // Check onboard sensors and update display 
        private void updateSensors(ThreadPoolTimer timer)
        {
            // Update sensors on board
            senseHat.Sensors.HumiditySensor.Update();
            senseHat.Sensors.PressureSensor.Update();

            updateScreen();
        }

        private void updateScreen()
        {
            if (senseHat.Sensors.Temperature.HasValue | senseHat.Sensors.Humidity.HasValue | senseHat.Sensors.Pressure.HasValue)
            {
                try
                {
                    switch (_currentMode)
                    {
                        case Selector.Local_Temp:
                            int temperature = (int)Math.Round(senseHat.Sensors.Temperature.Value);
                            textB = temperature.ToString();
                            col = Colors.White;
                            break;

                        case Selector.API_Temp:
                            float temperaturefl = weatherObject.main.temp;// ;// + " degrees C";
                            temperature = (int)Math.Round(temperaturefl);
                            textB = temperature.ToString();
                            col = Colors.Blue;
                            break;

                        case Selector.Local_Humidity:
                            int humid = (int)Math.Round(senseHat.Sensors.Humidity.Value);
                            textB = humid.ToString();
                            col = Colors.Green;
                            break;

                        case Selector.API_Humidity:
                            float humid1 = weatherObject.main.humidity;// ;// + " degrees C";
                            temperature = (int)Math.Round(humid1);
                            textB = temperature.ToString();
                            col = Colors.Yellow;
                            break;

                        // This case will not happen so catch executes
                        case Selector.Local_Pressure:
                            float pres = weatherObject.main.pressure;// ;// + " degrees C";
                            temperature = (int)Math.Round(pres);
                            textB = temperature.ToString();

                            col = Colors.Purple;
                            break;

                        default:
                            textB = "**";
                            col = Colors.Red;
                            break;
                    }

                    // Refresh display
                    display.Clear();
                    tinyFont.Write(display, textB, col);

                }

                catch (Exception e)
                {
                    // Step the scroller.
                    if (!textScroller.Step())
                    {
                        // Reset the scroller when reaching the end.
                        textScroller.Reset();
                    }

                    //FillDisplay(textScroller.ScrollPixelOffset);
                    senseHat.Display.Fill(Colors.Black);

                    // Draw the scroll text.
                    textScroller.Render();
                }

                display.Update();
            }
        }

        private async void getWeatherPressed(object sender, RoutedEventArgs e)
        {
            currentCity = textBoxCity.Text;
            progressBar.Visibility = Visibility.Visible;
            await getWeather(currentCity);
            progressBar.Visibility = Visibility.Collapsed;

            if (weatherObject == null)
            {
                var messageDialog = new Windows.UI.Popups.MessageDialog("An error occured");
                await messageDialog.ShowAsync();
            }

            // Assign textboxes in UI to values retrieved from our weather API
            textBlockCity.Text = weatherObject.name;
            textBlockTemperature.Text = weatherObject.main.temp + " °C";
            textBlockHumidity.Text = weatherObject.main.humidity + " %";
            textBlockPressure.Text = weatherObject.main.pressure + " hPa";
            textBlockConditions.Text = weatherObject.weather.First().description;

            // Assign textboxes in UI to values retrieved from sensor
            temperature.Text = Math.Round(senseHat.Sensors.Temperature.Value).ToString() + " °C";
            humidity.Text = Math.Round(senseHat.Sensors.Humidity.Value).ToString() + " %";
            pressure.Text = Math.Round(senseHat.Sensors.Pressure.Value).ToString() + " hPa";

            // Update physical display
            updateScreen();

        }

        private async Task getWeather(string city)
        {

            WeatherData.Rootobject rootObject = null;
            try
            {
                // Initialise HttpClient for accessing RESTful APIs
                HttpClient client = new HttpClient();

                // Calling our weather API, passing the string 'city' so we're getting the correct weather returned.
                // The 'await' tag tells the computer to wait for the results to be returned before continuing with
                // the rest of the code. The results are then assigned to string 'x' to be used later in the code.
                string x = await client.GetStringAsync("http://api.openweathermap.org/data/2.5/weather?q=" + city + "&units=metric");

                // Convert the JSON string response (stored in x) into an object with the structure defined in the class WeatherData.Rootobject
                rootObject = JsonConvert.DeserializeObject<WeatherData.Rootobject>(x);
            }
            catch (Exception e)
            {

            }

            weatherObject = rootObject;
        }

        private void switch_Click(object sender, RoutedEventArgs e)
        {
            SwitchToNextScrollMode();
        }

        string textB = "";
        Color col;

        private enum Selector
        {
            Local_Temp,
            API_Temp,
            Local_Humidity,
            API_Humidity,
            // Local_Pressure does not happen
            Local_Pressure,
        }

        private Selector _currentMode;

        private void SwitchToNextScrollMode()
        {
            _currentMode++;
            if (_currentMode > Selector.Local_Pressure)
            {
                _currentMode = Selector.Local_Temp;
            }
        }


        private Color GetCharacterColor(BwCharacterRendererPixelMap pixelMap)
        {
            return Colors.Red;
        }

        private readonly ManualResetEventSlim _waitEvent = new ManualResetEventSlim(false);

        private void Sleep(TimeSpan duration)
        {
            _waitEvent.Wait(duration);
        }

        private static IEnumerable<byte> FontBytes
        {
            get
            {
                return new byte[]
                {
                    0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0x00, 0x41, 0x00, 0x00, 0x7c, 0x7e, 0x0b, 0x0b, 0x7e, 0x7c,
                    0x00, 0xff, 0x00, 0x42, 0x00, 0x00, 0x7f, 0x7f, 0x49, 0x49, 0x7f, 0x36, 0x00, 0xff, 0x00, 0x43, 0x00, 0x00, 0x3e,
                    0x7f, 0x41, 0x41, 0x63, 0x22, 0x00, 0xff, 0x00, 0x44, 0x00, 0x00, 0x7f, 0x7f, 0x41, 0x63, 0x3e, 0x1c, 0x00, 0xff,
                    0x00, 0x45, 0x00, 0x00, 0x7f, 0x7f, 0x49, 0x49, 0x41, 0x41, 0x00, 0xff, 0x00, 0x46, 0x00, 0x00, 0x7f, 0x7f, 0x09,
                    0x09, 0x01, 0x01, 0x00, 0xff, 0x00, 0x47, 0x00, 0x00, 0x3e, 0x7f, 0x41, 0x49, 0x7b, 0x3a, 0x00, 0xff, 0x00, 0x48,
                    0x00, 0x00, 0x7f, 0x7f, 0x08, 0x08, 0x7f, 0x7f, 0x00, 0xff, 0x00, 0x49, 0x00, 0x00, 0x41, 0x7f, 0x7f, 0x41, 0x00,
                    0xff, 0x00, 0x4a, 0x00, 0x00, 0x20, 0x60, 0x41, 0x7f, 0x3f, 0x01, 0x00, 0xff, 0x00, 0x4b, 0x00, 0x00, 0x7f, 0x7f,
                    0x1c, 0x36, 0x63, 0x41, 0x00, 0xff, 0x00, 0x4c, 0x00, 0x00, 0x7f, 0x7f, 0x40, 0x40, 0x40, 0x40, 0x00, 0xff, 0x00,
                    0x4d, 0x00, 0x00, 0x7f, 0x7f, 0x06, 0x0c, 0x06, 0x7f, 0x7f, 0x00, 0xff, 0x00, 0x4e, 0x00, 0x00, 0x7f, 0x7f, 0x0e,
                    0x1c, 0x7f, 0x7f, 0x00, 0xff, 0x00, 0x4f, 0x00, 0x00, 0x3e, 0x7f, 0x41, 0x41, 0x7f, 0x3e, 0x00, 0xff, 0x00, 0x50,
                    0x00, 0x00, 0x7f, 0x7f, 0x09, 0x09, 0x0f, 0x06, 0x00, 0xff, 0x00, 0x51, 0x00, 0x00, 0x1e, 0x3f, 0x21, 0x61, 0x7f,
                    0x5e, 0x00, 0xff, 0x00, 0x52, 0x00, 0x00, 0x7f, 0x7f, 0x19, 0x39, 0x6f, 0x46, 0x00, 0xff, 0x00, 0x53, 0x00, 0x00,
                    0x26, 0x6f, 0x49, 0x49, 0x7b, 0x32, 0x00, 0xff, 0x00, 0x54, 0x00, 0x00, 0x01, 0x01, 0x7f, 0x7f, 0x01, 0x01, 0x00,
                    0xff, 0x00, 0x55, 0x00, 0x00, 0x3f, 0x7f, 0x40, 0x40, 0x7f, 0x3f, 0x00, 0xff, 0x00, 0x56, 0x00, 0x00, 0x1f, 0x3f,
                    0x60, 0x60, 0x3f, 0x1f, 0x00, 0xff, 0x00, 0x57, 0x00, 0x00, 0x7f, 0x7f, 0x30, 0x18, 0x30, 0x7f, 0x7f, 0x00, 0xff,
                    0x00, 0x58, 0x00, 0x00, 0x63, 0x77, 0x1c, 0x1c, 0x77, 0x63, 0x00, 0xff, 0x00, 0x59, 0x00, 0x00, 0x07, 0x0f, 0x78,
                    0x78, 0x0f, 0x07, 0x00, 0xff, 0x00, 0x5a, 0x00, 0x00, 0x61, 0x71, 0x59, 0x4d, 0x47, 0x43, 0x00, 0xff, 0x00, 0xc5,
                    0x00, 0x00, 0x70, 0x7a, 0x2d, 0x2d, 0x7a, 0x70, 0x00, 0xff, 0x00, 0xc4, 0x00, 0x00, 0x71, 0x79, 0x2c, 0x2c, 0x79,
                    0x71, 0x00, 0xff, 0x00, 0xd6, 0x00, 0x00, 0x39, 0x7d, 0x44, 0x44, 0x7d, 0x39, 0x00, 0xff, 0x00, 0xc9, 0x00, 0x00,
                    0x7c, 0x7c, 0x54, 0x56, 0x45, 0x45, 0x00, 0xff, 0x00, 0xdc, 0x00, 0x00, 0x3d, 0x7d, 0x40, 0x40, 0x7d, 0x3d, 0x00,
                    0xff, 0x00, 0x61, 0x00, 0x20, 0x74, 0x54, 0x54, 0x7c, 0x78, 0x00, 0xff, 0x00, 0x62, 0x00, 0x00, 0x7f, 0x7f, 0x48,
                    0x48, 0x78, 0x30, 0x00, 0xff, 0x00, 0x63, 0x00, 0x00, 0x38, 0x7c, 0x44, 0x44, 0x44, 0x00, 0xff, 0x00, 0x64, 0x00,
                    0x00, 0x38, 0x7c, 0x44, 0x44, 0x7f, 0x7f, 0x00, 0xff, 0x00, 0x65, 0x00, 0x00, 0x38, 0x7c, 0x54, 0x54, 0x5c, 0x18,
                    0x00, 0xff, 0x00, 0x66, 0x00, 0x00, 0x04, 0x7e, 0x7f, 0x05, 0x05, 0x00, 0xff, 0x00, 0x67, 0x00, 0x00, 0x98, 0xbc,
                    0xa4, 0xa4, 0xfc, 0x7c, 0x00, 0xff, 0x00, 0x68, 0x00, 0x00, 0x7f, 0x7f, 0x08, 0x08, 0x78, 0x70, 0x00, 0xff, 0x00,
                    0x69, 0x00, 0x00, 0x48, 0x7a, 0x7a, 0x40, 0x00, 0xff, 0x00, 0x6a, 0x00, 0x80, 0x80, 0x80, 0xfa, 0x7a, 0x00, 0xff,
                    0x00, 0x6b, 0x00, 0x00, 0x7f, 0x7f, 0x10, 0x38, 0x68, 0x40, 0x00, 0xff, 0x00, 0x6c, 0x00, 0x00, 0x41, 0x7f, 0x7f,
                    0x40, 0x00, 0xff, 0x00, 0x6d, 0x00, 0x00, 0x7c, 0x7c, 0x18, 0x38, 0x1c, 0x7c, 0x78, 0x00, 0xff, 0x00, 0x6e, 0x00,
                    0x00, 0x7c, 0x7c, 0x04, 0x04, 0x7c, 0x78, 0x00, 0xff, 0x00, 0x6f, 0x00, 0x00, 0x38, 0x7c, 0x44, 0x44, 0x7c, 0x38,
                    0x00, 0xff, 0x00, 0x70, 0x00, 0x00, 0xfc, 0xfc, 0x24, 0x24, 0x3c, 0x18, 0x00, 0xff, 0x00, 0x71, 0x00, 0x00, 0x18,
                    0x3c, 0x24, 0x24, 0xfc, 0xfc, 0x00, 0xff, 0x00, 0x72, 0x00, 0x00, 0x7c, 0x7c, 0x04, 0x04, 0x0c, 0x08, 0x00, 0xff,
                    0x00, 0x73, 0x00, 0x00, 0x48, 0x5c, 0x54, 0x54, 0x74, 0x24, 0x00, 0xff, 0x00, 0x74, 0x00, 0x00, 0x04, 0x04, 0x3f,
                    0x7f, 0x44, 0x44, 0x00, 0xff, 0x00, 0x75, 0x00, 0x00, 0x3c, 0x7c, 0x40, 0x40, 0x7c, 0x7c, 0x00, 0xff, 0x00, 0x76,
                    0x00, 0x00, 0x1c, 0x3c, 0x60, 0x60, 0x3c, 0x1c, 0x00, 0xff, 0x00, 0x77, 0x00, 0x00, 0x1c, 0x7c, 0x70, 0x38, 0x70,
                    0x7c, 0x1c, 0x00, 0xff, 0x00, 0x78, 0x00, 0x00, 0x44, 0x6c, 0x38, 0x38, 0x6c, 0x44, 0x00, 0xff, 0x00, 0x79, 0x00,
                    0x00, 0x9c, 0xbc, 0xa0, 0xe0, 0x7c, 0x3c, 0x00, 0xff, 0x00, 0x7a, 0x00, 0x00, 0x44, 0x64, 0x74, 0x5c, 0x4c, 0x44,
                    0x00, 0xff, 0x00, 0xe5, 0x00, 0x20, 0x74, 0x55, 0x55, 0x7c, 0x78, 0x00, 0xff, 0x00, 0xe4, 0x00, 0x20, 0x75, 0x54,
                    0x54, 0x7d, 0x78, 0x00, 0xff, 0x00, 0xf6, 0x00, 0x00, 0x30, 0x7a, 0x48, 0x48, 0x7a, 0x30, 0x00, 0xff, 0x00, 0xe9,
                    0x00, 0x00, 0x38, 0x7c, 0x54, 0x56, 0x5d, 0x19, 0x00, 0xff, 0x00, 0xfc, 0x00, 0x00, 0x3a, 0x7a, 0x40, 0x40, 0x7a,
                    0x7a, 0x00, 0xff, 0x00, 0x30, 0x00, 0x00, 0x3e, 0x7f, 0x49, 0x45, 0x7f, 0x3e, 0x00, 0xff, 0x00, 0x31, 0x00, 0x00,
                    0x40, 0x44, 0x7f, 0x7f, 0x40, 0x40, 0x00, 0xff, 0x00, 0x32, 0x00, 0x00, 0x62, 0x73, 0x51, 0x49, 0x4f, 0x46, 0x00,
                    0xff, 0x00, 0x33, 0x00, 0x00, 0x22, 0x63, 0x49, 0x49, 0x7f, 0x36, 0x00, 0xff, 0x00, 0x34, 0x00, 0x00, 0x18, 0x18,
                    0x14, 0x16, 0x7f, 0x7f, 0x10, 0xff, 0x00, 0x35, 0x00, 0x00, 0x27, 0x67, 0x45, 0x45, 0x7d, 0x39, 0x00, 0xff, 0x00,
                    0x36, 0x00, 0x00, 0x3e, 0x7f, 0x49, 0x49, 0x7b, 0x32, 0x00, 0xff, 0x00, 0x37, 0x00, 0x00, 0x03, 0x03, 0x79, 0x7d,
                    0x07, 0x03, 0x00, 0xff, 0x00, 0x38, 0x00, 0x00, 0x36, 0x7f, 0x49, 0x49, 0x7f, 0x36, 0x00, 0xff, 0x00, 0x39, 0x00,
                    0x00, 0x26, 0x6f, 0x49, 0x49, 0x7f, 0x3e, 0x00, 0xff, 0x00, 0x2e, 0x00, 0x00, 0x60, 0x60, 0x00, 0xff, 0x00, 0x2c,
                    0x00, 0x00, 0x80, 0xe0, 0x60, 0x00, 0xff, 0x00, 0x3f, 0x00, 0x00, 0x02, 0x03, 0x51, 0x59, 0x0f, 0x06, 0x00, 0xff,
                    0x00, 0x21, 0x00, 0x00, 0x4f, 0x4f, 0x00, 0xff, 0x00, 0x22, 0x00, 0x00, 0x07, 0x07, 0x00, 0x00, 0x07, 0x07, 0x00,
                    0xff, 0x00, 0x23, 0x00, 0x00, 0x14, 0x7f, 0x7f, 0x14, 0x14, 0x7f, 0x7f, 0x14, 0x00, 0xff, 0x00, 0x24, 0x00, 0x00,
                    0x24, 0x2e, 0x6b, 0x6b, 0x3a, 0x12, 0x00, 0xff, 0x00, 0x25, 0x00, 0x00, 0x63, 0x33, 0x18, 0x0c, 0x66, 0x63, 0x00,
                    0xff, 0x00, 0x26, 0x00, 0x00, 0x32, 0x7f, 0x4d, 0x4d, 0x77, 0x72, 0x50, 0x00, 0xff, 0x00, 0x2d, 0x00, 0x00, 0x08,
                    0x08, 0x08, 0x08, 0x08, 0x08, 0x00, 0xff, 0x00, 0x2b, 0x00, 0x00, 0x08, 0x08, 0x3e, 0x3e, 0x08, 0x08, 0x00, 0xff,
                    0x00, 0x2a, 0x00, 0x00, 0x08, 0x2a, 0x3e, 0x1c, 0x1c, 0x3e, 0x2a, 0x08, 0x00, 0xff, 0x00, 0x3a, 0x00, 0x00, 0x66,
                    0x66, 0x00, 0xff, 0x00, 0x3b, 0x00, 0x00, 0x80, 0xe6, 0x66, 0x00, 0xff, 0x00, 0x2f, 0x00, 0x00, 0x40, 0x60, 0x30,
                    0x18, 0x0c, 0x06, 0x02, 0x00, 0xff, 0x00, 0x5c, 0x00, 0x00, 0x02, 0x06, 0x0c, 0x18, 0x30, 0x60, 0x40, 0x00, 0xff,
                    0x00, 0x3c, 0x00, 0x00, 0x08, 0x1c, 0x36, 0x63, 0x41, 0x41, 0x00, 0xff, 0x00, 0x3e, 0x00, 0x00, 0x41, 0x41, 0x63,
                    0x36, 0x1c, 0x08, 0x00, 0xff, 0x00, 0x28, 0x00, 0x00, 0x1c, 0x3e, 0x63, 0x41, 0x00, 0xff, 0x00, 0x29, 0x00, 0x00,
                    0x41, 0x63, 0x3e, 0x1c, 0x00, 0xff, 0x00, 0x27, 0x00, 0x00, 0x04, 0x06, 0x03, 0x01, 0x00, 0xff, 0x00, 0x60, 0x00,
                    0x00, 0x01, 0x03, 0x06, 0x04, 0x00, 0xff, 0x00, 0x3d, 0x00, 0x00, 0x14, 0x14, 0x14, 0x14, 0x14, 0x14, 0x00
                };
            }
        }
    }
}
