﻿using System;
using System.Threading;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;

namespace ILI9341Driver
{
    public partial class Ili9341EighteenBit
    {
        // Any faster than 33MHz causes an exception
        const int MaxSPIFrequency = 33000000;

        private readonly GpioPin _dataCommandPin;
        private readonly GpioPin _resetPin;
        private readonly GpioPin _backlightPin;
        private readonly GpioPin _chipSelectPin;

        private readonly SpiDevice _spi;

        private byte[] _orientationFlags;

        private bool _backlightOn;
        public bool BacklightOn
        {
            get
            {
                return _backlightOn;
            }
            set
            {
                if (_backlightPin != null)
                {
                    var pinValue = value ? GpioPinValue.High : GpioPinValue.Low;
                    _backlightPin.Write(pinValue);
                    _backlightOn = value;
                }
            }
        }

        

        private int _width;
        public int Width
        {
            get
            {
                return _width;
            }
            private set
            {
                _width = value;
            }
        }

        private int _height;
        public int Height
        {
            get
            {
                return _height;
            }
            set
            {
                _height = value;
            }
        }

        private ushort _cursorX;

        public ushort CursorX
        {
            get { return _cursorX; }
            set { _cursorX = value; }
        }

        private ushort _cursorY;

        public ushort CursorY
        {
            get { return _cursorY; }
            set { _cursorY = value; }
        }

        private UInt32 _textColor;

        public UInt32 TextColor
        {
            get { return _textColor; }
            set { _textColor = value; }
        }

        private Font _textFont;

        public Font TextFont
        {
            get { return _textFont; }
            set { _textFont = value; }
        }

        private bool _textWrap = true;

        public bool TextWrap
        {
            get { return _textWrap; }
            set { _textWrap = value; }
        }

        private int _dataBitLength;
        public int DataBitLength
        {
            get
            {
                return _dataBitLength;
            }
            set
            {
                _dataBitLength = value;
                _spi.ConnectionSettings.DataBitLength = value;                
            }
        }


        #region Constructors

        public Ili9341EighteenBit(
            GpioPin chipSelectPin = null,
            GpioPin dataCommandPin = null,
            GpioPin resetPin = null,
            GpioPin backlightPin = null,            
            int spiClockFrequency = MaxSPIFrequency,
            SpiMode spiMode = SpiMode.Mode0,
            string spiBus = "SPI1",
            Orientations rotation = Orientations.Landscape,
            byte[] orientationFlags = null,
            Font textFont = null)
        {
            if (chipSelectPin != null)
            {
                _chipSelectPin = chipSelectPin;
                _chipSelectPin.SetDriveMode(GpioPinDriveMode.Output); 
            }
            else
            {
                throw new ArgumentNullException("chipSelectPin");
            }

            if (dataCommandPin != null)
            {
                _dataCommandPin = dataCommandPin;
                _dataCommandPin.SetDriveMode(GpioPinDriveMode.Output); 
            }
            else
            {
                throw new ArgumentNullException("dataCommandPin");
            }

            if (resetPin != null)
            {
                _resetPin = resetPin;
                _resetPin.SetDriveMode(GpioPinDriveMode.Output); 
            }

            if (backlightPin != null)
            {
                _backlightPin = backlightPin;
                _backlightPin.SetDriveMode(GpioPinDriveMode.Output); 
            }

            if (orientationFlags == null)
            {
                _orientationFlags = new byte[]
                {
                    (byte)MemoryAccessControl.MX,
                    (byte)MemoryAccessControl.MV,
                    (byte)(MemoryAccessControl.MY | MemoryAccessControl.ML),
                    (byte)(MemoryAccessControl.ML | MemoryAccessControl.MV | MemoryAccessControl.MX | MemoryAccessControl.MY)
                };
            }
            else if (orientationFlags.Length == 4)
            {
                _orientationFlags = orientationFlags;
            }
            else 
            {
                throw new ArgumentOutOfRangeException("orientationFlags");
            }

            if (textFont == null)
            {
                _textFont = new StandardFixedWidthFont();
            }


            var connectionSettings = new SpiConnectionSettings(chipSelectPin.PinNumber)
            {
                DataBitLength = DataBitLength,
                ClockFrequency = spiClockFrequency,
                Mode = spiMode,                
            };

            _spi = SpiDevice.FromId(spiBus, connectionSettings);

            InitializeScreen();
            SetRotation(rotation);
        }

        #endregion Constructors

        #region Communication Methods

        protected virtual void WriteData(byte[] data)
        {
            _spi.Write(data);
        }

        protected virtual void WriteData(ushort[] data)
        {
            _spi.Write(data);
        }

        protected virtual void SendCommand(Commands command)
        {
            _dataCommandPin.Write(GpioPinValue.Low);
            WriteData(new[] { (byte)command });
        }

        protected virtual void SendData(params byte[] data)
        {
            _dataCommandPin.Write(GpioPinValue.High);
            WriteData(data);
        }        

        protected virtual void WriteReset(GpioPinValue value)
        {
            lock (this)
            {
                if (_resetPin != null)
                {

                    _resetPin.Write(value);
                }
            }
        }

        #endregion Communication Methods

        #region Public Methods        

        /// <summary>
        /// Sets a pixel to <paramref name="color"/>
        /// </summary>
        /// <param name="x">The x (horizontal) coordinate</param>
        /// <param name="y">The y (vertical) coordinate</param>
        /// <param name="color">The color in 18-bit (666) format</param>
        public void SetPixel(int x, int y, UInt32 color)
        {
            lock (this)
            {
                SetWindow(x, x, y, y);
                
                SendData(ColorHelper.HexTo666ByteArray(color));
            }
        }

        

        public void SetRotation(Orientations orientation)
        {
            SendCommand(Commands.MemoryAccessControl);
            switch (orientation)
            {
                default:
                case Orientations.Portrait:                    
                    SendData(_orientationFlags[(int)Orientations.Portrait]);
                    Width = 240;
                    Height = 320;
                    break;
                case Orientations.Landscape:
                    SendData(_orientationFlags[(int)Orientations.Landscape]);
                    Width = 320;
                    Height = 240;
                    break;
                case Orientations.PortraitFlip:
                    SendData(_orientationFlags[(int)Orientations.PortraitFlip]);
                    Width = 240;
                    Height = 320;
                    break;
                case Orientations.LandscapeFlip:
                    SendData(_orientationFlags[(int)Orientations.LandscapeFlip]);
                    Width = 320;
                    Height = 240;
                    break;                
            }

        }

        public void ScrollUp(uint pixels)
        {
            lock (this)
            {
                SendCommand(Commands.VerticalScrollingStartAddress);
                SendData(Int32ToByteArray(pixels));

                SendCommand(Commands.MemoryWrite);
            }
        }

        public void SetCursor(ushort x, ushort y)
        {
            _cursorX = x;
            _cursorY = y;
        }

        #endregion Public Methods

        private byte[] Int32ToByteArray(uint number)
        {
            var bytes = new byte[4];

            bytes[0] = (byte)((number >> 24) & 0xFF);
            bytes[1] = (byte)((number >> 16) & 0xFF);
            bytes[2] = (byte)((number >> 8) & 0xFF);
            bytes[3] = (byte)(number & 0xFF);

            return bytes;
        }



        protected virtual void InitializeScreen()
        {
            lock (this)
            {
                WriteReset(GpioPinValue.Low);
                Thread.Sleep(10);
                WriteReset(GpioPinValue.High);
                SendCommand(Commands.SoftwareReset);
                Thread.Sleep(10);
                SendCommand(Commands.DisplayOff);

                SendCommand(Commands.MemoryAccessControl);
                var initializeMAC = MemoryAccessControl.MX;
                SendData((byte)initializeMAC);

                SendCommand(Commands.PixelFormatSet);
                SendData((byte)PixelFormat.EighteenBit); //18-bits per pixel

                SendCommand(Commands.FrameControlNormal);
                SendData(0x00, 0x1B);

                SendCommand(Commands.GammaSet);
                SendData(0x01);

                SendCommand(Commands.ColumnAddressSet); //width of the screen
                SendData(0x00, 0x00, 0x00, 0xEF);

                SendCommand(Commands.PageAddressSet); //height of the screen
                SendData(0x00, 0x00, 0x01, 0x3F);

                SendCommand(Commands.EntryModeSet);
                SendData(0x07);

                SendCommand(Commands.DisplayFunctionControl);
                SendData(0x0A, 0x82, 0x27, 0x00);

                SendCommand(Commands.SleepOut);
                Thread.Sleep(120);

                SendCommand(Commands.DisplayOn);
                Thread.Sleep(100);

                SendCommand(Commands.MemoryWrite);
            }
        }

        public static ushort ColorFromRgb(byte r, byte g, byte b)
        {
            return (ushort)((r << 11) | (g << 5) | b);
        }

        public void SetWindow(int left, int right, int top, int bottom)
        {
            lock (this)
            {
                SendCommand(Commands.ColumnAddressSet);
                SendData((byte)((left >> 8) & 0xFF),
                         (byte)(left & 0xFF),
                         (byte)((right >> 8) & 0xFF),
                         (byte)(right & 0xFF));
                SendCommand(Commands.PageAddressSet);
                SendData((byte)((top >> 8) & 0xFF),
                         (byte)(top & 0xFF),
                         (byte)((bottom >> 8) & 0xFF),
                         (byte)(bottom & 0xFF));
                SendCommand(Commands.MemoryWrite);
            }
        }
    }
}

