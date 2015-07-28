using Microsoft.Maker.Firmata;
using System;
using System.Threading;
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
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace wra_neopixel_control
{
    struct Acceleration
    {
        public double X;
        public double Y;
        public double Z;
    };

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int NEOPIXEL_SET_COMMAND = 0x42;
        private const int NEOPIXEL_SHOW_COMMAND = 0x44;
        private const int NUMBER_OF_PIXELS = 30;

        private UwpFirmata firmata;
        private DispatcherTimer timer;
        private double _lightDelay = 100;
        private string _lightColor = "Blue";
        private int ledsValue = 0;

        //accelerometer
        private const byte ACCEL_REG_POWER_CONTROL = 0x2D;  /* Address of the Power Control register                */
        private const byte ACCEL_REG_DATA_FORMAT = 0x31;    /* Address of the Data Format register                  */
        private const byte ACCEL_REG_X = 0x32;              /* Address of the X Axis data register                  */
        private const byte ACCEL_REG_Y = 0x34;              /* Address of the Y Axis data register                  */
        private const byte ACCEL_REG_Z = 0x36;              /* Address of the Z Axis data register                  */

        private const byte SPI_CHIP_SELECT_LINE = 0;        /* Chip select line to use                              */
        private const byte ACCEL_SPI_RW_BIT = 0x80;         /* Bit used in SPI transactions to indicate read/write  */
        private const byte ACCEL_SPI_MB_BIT = 0x40;         /* Bit used to indicate multi-byte SPI transactions     */

        private SpiDevice SPIAccel;
        private Timer periodicTimer;


        /// <summary>
        /// This page uses advanced features of the Windows Remote Arduino library to carry out custom commands which are
        /// defined in the NeoPixel_StandardFirmata.ino sketch. This is a customization of the StandardFirmata sketch which
        /// implements the Firmata protocol. The customization defines the behaviors of the custom commands invoked by this page.
        /// 
        /// To learn more about Windows Remote Arduino, refer to the GitHub page at: https://github.com/ms-iot/remote-wiring/
        /// To learn more about advanced behaviors of WRA and how to define your own custom commands, refer to the
        /// advanced documentation here: https://github.com/ms-iot/remote-wiring/blob/develop/advanced.md
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            firmata = App.Firmata;

            //timer for delay
            timer = new DispatcherTimer();
            timer.Tick += FlipLights;

            Unloaded += MainPage_Unloaded;

            //accelerometer
            InitAccel();
        }

        private async void InitAccel()
        {
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 5000000;                              /* 5MHz is the rated speed of the ADXL345 accelerometer                     */
                settings.Mode = SpiMode.Mode3;                                  /* The accelerometer expects an idle-high clock polarity, we use Mode3    
                                                                                 * to set the clock polarity and phase to: CPOL = 1, CPHA = 1         
                                                                                 */

                string aqs = SpiDevice.GetDeviceSelector();                     /* Get a selector string that will return all SPI controllers on the system */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the SPI bus controller devices with our selector string             */
                SPIAccel = await SpiDevice.FromIdAsync(dis[0].Id, settings);    /* Create an SpiDevice with our bus controller and SPI settings             */
                if (SPIAccel == null)
                {
                    Text_Status.Text = string.Format(
                        "SPI Controller {0} is currently in use by " +
                        "another application. Please ensure that no other applications are using SPI.",
                        dis[0].Id);
                    return;
                }
            }
            catch (Exception ex)
            {
                Text_Status.Text = "SPI Initialization failed. Exception: " + ex.Message;
                return;
            }

            /* 
             * Initialize the accelerometer:
             *
             * For this device, we create 2-byte write buffers:
             * The first byte is the register address we want to write to.
             * The second byte is the contents that we want to write to the register. 
             */
            byte[] WriteBuf_DataFormat = new byte[] { ACCEL_REG_DATA_FORMAT, 0x01 };        /* 0x01 sets range to +- 4Gs                         */
            byte[] WriteBuf_PowerControl = new byte[] { ACCEL_REG_POWER_CONTROL, 0x08 };    /* 0x08 puts the accelerometer into measurement mode */

            /* Write the register settings */
            try
            {
                SPIAccel.Write(WriteBuf_DataFormat);
                SPIAccel.Write(WriteBuf_PowerControl);
            }
            /* If the write fails display the error and stop running */
            catch (Exception ex)
            {
                Text_Status.Text = "Failed to communicate with device: " + ex.Message;
                return;
            }

            /* Now that everything is initialized, create a timer so we read data every 100mS */
            periodicTimer = new Timer(this.TimerCallback, null, 0, 100);
        }

        private void MainPage_Unloaded(object sender, object args)
        {
             SPIAccel.Dispose();
        }

        private void FlipLights(object sender, object e)
        {
            if(ledsValue == 0)
            {
                SetAllPixelsAndUpdate(0, 0, 0);
                ledsValue = 1;
            }
            else
            {
                switch (_lightColor)
                {
                    case "Red":
                        SetAllPixelsAndUpdate(255, 0, 0);
                        break;

                    case "Green":
                        SetAllPixelsAndUpdate(0, 255, 0);
                        break;

                    case "Blue":
                        SetAllPixelsAndUpdate(0, 0, 255);
                        break;

                    case "Yellow":
                        SetAllPixelsAndUpdate(255, 255, 0);
                        break;

                    case "Cyan":
                        SetAllPixelsAndUpdate(0, 255, 255);
                        break;

                    case "Magenta":
                        SetAllPixelsAndUpdate(255, 0, 255);
                        break;
                }
                ledsValue = 0;
            }
        }

        private void Lights_On(object sender, RoutedEventArgs e)
        {
            timer.Interval = TimeSpan.FromMilliseconds(_lightDelay);
            timer.Start();
        }

        /// <summary>
        /// This button callback is invoked when the buttons are pressed on the UI. It determines which
        /// button is pressed and sets the LEDs appropriately
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Color_Click( object sender, RoutedEventArgs e )
        {
            var button = sender as Button;
            switch( button.Name )
            {
                case "Red":
                    _lightColor="Red";
                    break;
                
                case "Green":
                    _lightColor = "Green";
                    break;

                case "Blue":
                    _lightColor = "Blue";
                    break;

                case "Yellow":
                    _lightColor = "Yellow";
                    break;

                case "Cyan":
                    _lightColor = "Cyan";
                    break;

                case "Magenta":
                    _lightColor = "Magenta";
                    break;
            }
        }

        private void OnDelayValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
                _lightDelay=e.NewValue*100;
        }

        /// <summary>
        /// Sets all the pixels to the given color values and calls UpdateStrip() to tell the NeoPixel library to show the set colors.
        /// </summary>
        /// <param name="red"></param>
        /// <param name="green"></param>
        /// <param name="blue"></param>
        private void SetAllPixelsAndUpdate( byte red, byte green, byte blue )
        {
            SetAllPixels( red, green, blue );
            UpdateStrip();
        }

        /// <summary>
        /// Sets all the pixels to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetAllPixels( byte red, byte green, byte blue )
        {
            for( byte i = 0; i < NUMBER_OF_PIXELS; ++i )
            {
                SetPixel( i, red, green, blue );
            }
        }

        /// <summary>
        /// Sets a single pixel to the given color values
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void SetPixel( byte pixel, byte red, byte green, byte blue )
        {
            firmata.beginSysex( NEOPIXEL_SET_COMMAND );
            firmata.appendSysex( pixel );
            firmata.appendSysex( red );
            firmata.appendSysex( green );
            firmata.appendSysex( blue );
            firmata.endSysex();
        }

        /// <summary>
        /// Tells the NeoPixel strip to update its displayed colors.
        /// This function must be called before any colors set to pixels will be displayed.
        /// </summary>
        /// <param name="red">The amount of red to set</param>
        /// <param name="green">The amount of green to set</param>
        /// <param name="blue">The amount of blue to set</param>
        private void UpdateStrip()
        {
            firmata.beginSysex( NEOPIXEL_SHOW_COMMAND );
            firmata.endSysex();
        }

        private void TimerCallback(object state)
        {
            string xText, yText, zText;
            string statusText;

            /* Read and format accelerometer data */
            try
            {
                Acceleration accel = ReadAccel();
                xText = String.Format("X Axis: {0:F3}G", accel.X);
                yText = String.Format("Y Axis: {0:F3}G", accel.Y);
                zText = String.Format("Z Axis: {0:F3}G", accel.Z);
                if (CheckIntruder(accel.Z))
                {
                    statusText = "INTRUDER!!";
                }
                else
                {
                    statusText = "Status: Running";
                }
            }
            catch (Exception ex)
            {
                xText = "X Axis: Error";
                yText = "Y Axis: Error";
                zText = "Z Axis: Error";
                statusText = "Failed to read from Accelerometer: " + ex.Message;
            }

            /* UI updates must be invoked on the UI thread */
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Text_X_Axis.Text = xText;
                Text_Y_Axis.Text = yText;
                Text_Z_Axis.Text = zText;
                Text_Status.Text = statusText;
            });
        }

        private bool CheckIntruder(double zAxis)
        {
            if (zAxis < 0.5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private Acceleration ReadAccel()
        {
            const int ACCEL_RES = 1024;         /* The ADXL345 has 10 bit resolution giving 1024 unique values                     */
            const int ACCEL_DYN_RANGE_G = 8;    /* The ADXL345 had a total dynamic range of 8G, since we're configuring it to +-4G */
            const int UNITS_PER_G = ACCEL_RES / ACCEL_DYN_RANGE_G;  /* Ratio of raw int values to G units                          */

            byte[] ReadBuf;
            byte[] RegAddrBuf;

            /* 
             * Read from the accelerometer 
             * We first write the address of the X-Axis register, then read all 3 axes into ReadBuf
             */
 
             ReadBuf = new byte[6 + 1];      /* Read buffer of size 6 bytes (2 bytes * 3 axes) + 1 byte padding */
             RegAddrBuf = new byte[1 + 6];   /* Register address buffer of size 1 byte + 6 bytes padding        */
             /* Register address we want to read from with read and multi-byte bit set                          */
             RegAddrBuf[0] = ACCEL_REG_X | ACCEL_SPI_RW_BIT | ACCEL_SPI_MB_BIT;
             SPIAccel.TransferFullDuplex(RegAddrBuf, ReadBuf);
             Array.Copy(ReadBuf, 1, ReadBuf, 0, 6);  /* Discard first dummy byte from read                      */

            /* Check the endianness of the system and flip the bytes if necessary */
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(ReadBuf, 0, 2);
                Array.Reverse(ReadBuf, 2, 2);
                Array.Reverse(ReadBuf, 4, 2);
            }

            /* In order to get the raw 16-bit data values, we need to concatenate two 8-bit bytes for each axis */
            short AccelerationRawX = BitConverter.ToInt16(ReadBuf, 0);
            short AccelerationRawY = BitConverter.ToInt16(ReadBuf, 2);
            short AccelerationRawZ = BitConverter.ToInt16(ReadBuf, 4);

            /* Convert raw values to G's */
            Acceleration accel;
            accel.X = (double)AccelerationRawX / UNITS_PER_G;
            accel.Y = (double)AccelerationRawY / UNITS_PER_G;
            accel.Z = (double)AccelerationRawZ / UNITS_PER_G;

            return accel;
        }
    }
}
