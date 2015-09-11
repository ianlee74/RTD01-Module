/*
using System;
using Microsoft.SPOT;

using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GTS = Gadgeteer.Socket;
using GTI = Gadgeteer.SocketInterfaces;

namespace Gadgeteer.Modules.BrainardTechnologies
{
    /// <summary>
    /// A MAX31865 module for Microsoft .NET Gadgeteer
    /// </summary>
    public class MAX31865_backup : GTM.Module
    {
        private GT.Socket _socket;
        private GTI.DigitalOutput _csPin;
        private bool _initialized;
        private GTI.InterruptInput _irqPin;
        private GTI.Spi _spi;
        private GTI.SpiConfiguration _spiConfig;
        private GT.Timer FaultScanner;

        public delegate void FaultEventHandler(MAX31865 sender, byte DataByte);
        public event FaultEventHandler FaultEvent;
        public delegate void DataReadyEventHandler(MAX31865 sender, double Data);
        public event DataReadyEventHandler DataReadyFarEvent;
        public event DataReadyEventHandler DataReadyCelEvent;

        private byte _config;
        enum Command
        {
            READ = 0x00,
            WRITE = 0x80
        }

        ///<Summary>
        /// Config Bits
        ///</Summary>
        public enum ConfigValues
        {
            VBIAS_ON = 0x80,
            VBIAS_OFF = 0x00,
            CONV_MODE_AUTO = 0x40,
            CONV_MODE_OFF = 0x00,
            ONE_SHOT_ON = 0x20,
            ONE_SHOT_OFF = 0x00,
            THREE_WIRE = 0x10,
            FLT_DETECT_AUTO_DLY = 0x04,
            FLT_DETECT_RUN_MAN = 0x08,
            FLT_DETECT_FINISH_MAN = 0x0C,
            TWO_WIRE = 0x00,
            FOUR_WIRE = 0x00,
            FAULT_CLR = 0x02,
            FILTER_50Hz = 0x01,
            FILTER_60Hz = 0x00
        }

        public enum ConfigSettings
        {
            VBIAS = 0x80,
            CONV_MODE = 0x40,
            ONE_SHOT = 0x20,
            WIRE_TYPE = 0x10,
            FLT_DETECT = 0x0C,
            FAULT_CLR = 0x02,
            FILTER = 0x01
        }

        enum FaultBits
        {
            RTD_HI_THRESH = 0x80,
            RTD_LO_THRESH = 0x40,
            REF_IN_HI = 0x20,
            FORCE_OPEN_REFIN = 0x10,
            FORCE_OPEN_RTDIN = 0x08,
            UNDERVOLT = 0x04
        }

        public enum Register
        {
            CONFIG = 0x00,
            RTD_MSB = 0x01,
            RTD_LSB = 0x02,
            HI_FLT_THRESH_MSB = 0x03,
            HI_FLT_THRESH_LSB = 0x04,
            LO_FLT_THRESH_MSB = 0x05,
            LO_FLT_THRESH_LSB = 0x06,
            FLT_STATUS = 0x07
        }

        // This example implements a driver in managed code for a simple Gadgeteer module.  This module uses a 
        // single GTI.InterruptInput to interact with a button that can be in either of two states: pressed or released.
        // The example code shows the recommended code pattern for exposing a property (IsPressed). 
        // The example also uses the recommended code pattern for exposing two events: Pressed and Released. 
        // The triple-slash "///" comments shown will be used in the build process to create an XML file named
        // GTM.BrainardTechnologies.MAX31865. This file will provide IntelliSense and documentation for the
        // interface and make it easier for developers to use the MAX31865 module.        

        // -- CHANGE FOR MICRO FRAMEWORK 4.2 and higher --
        // If you want to use Serial, SPI, or DaisyLink (which includes GTI.SoftwareI2C), you must do a few more steps
        // since these have been moved to separate assemblies for NETMF 4.2 (to reduce the minimum memory footprint of Gadgeteer)
        // 1) add a reference to the assembly (named Gadgeteer.[interfacename])
        // 2) in GadgeteerHardware.xml, uncomment the lines under <Assemblies> so that end user apps using this module also add a reference.

        // Note: A constructor summary is auto-generated by the doc builder.
        /// <summary></summary>
        /// <param name="socketNumber">The socket that this module is plugged in to.</param>
        /// <param name="socketNumberTwo">The second socket that this module is plugged in to.</param>
        public MAX31865_backup(int socketNumber)
        {
            // This finds the Socket instance from the user-specified socket number.  
            // This will generate user-friendly error messages if the socket is invalid.
            // If there is more than one socket on this module, then instead of "null" for the last parameter, 
            // put text that identifies the socket to the user (e.g. "S" if there is a socket type S)
            _socket = Socket.GetSocket(socketNumber, true, this, null);
            _socket.EnsureTypeIsSupported('S', this);
        }

        /// <summary>
        ///   Initializes SPI connection and control pins
        ///   <param name="irqPin"> IRQ pin as a Socket.Pin
        ///   <param name="cePin"> Chip Enable(CE) pin as a Socket.Pin
        ///   <param name="irqPin"> Chip Select Not(CSN or CS\) pin as a Socket.Pin
        ///   <param name="spiClockRateKHZ"> Clock rate in KHz (i.e. 1000 = 1MHz)
        /// </summary>
        public void Initialize(Socket.Pin irqPin, Socket.Pin csPin, byte config, uint spiClockRateKHZ = 1000)
        {
            _spiConfig = new GTI.SpiConfiguration(true, 0, 0, false, true, spiClockRateKHZ);

            // Chip Select : Active Low
            // Clock : Active High, Data clocked in on rising edge
            //_socket = GTS.GetSocket(6, false, this, null);
            _spi = GTI.SpiFactory.Create(_socket, _spiConfig, GTI.SpiSharing.Shared, _socket, csPin, this);

            // Initialize Chip Enable Port
            _csPin = GTI.DigitalOutputFactory.Create(_socket, Socket.Pin.Four, true, this);

            _irqPin = GTI.InterruptInputFactory.Create(_socket, Socket.Pin.Three, GTI.GlitchFilterMode.On, GTI.ResistorMode.PullUp, GTI.InterruptMode.FallingEdge, this);
            _irqPin.Interrupt += _irqPin_Interrupt;
            _initialized = true;

            _config = config;

            ResetConfig();
        }

        void _irqPin_Interrupt(GTI.InterruptInput sender, bool value)
        {
            if (DataReadyFarEvent != null)
                DataReadyFarEvent(this, GetTempF());
            if (DataReadyCelEvent != null)
                DataReadyCelEvent(this, GetTempC());
        }

        public void ResetConfig()
        {
            ClearFaults();
            Debug.Print("Reset Config: From:" + GetRegister(0x00).ToString("X") + " To:" + _config.ToString("X"));
            SetRegister(0x00, _config);
        }

        public void ClearFaults()
        {
            Debug.Print("Clear Faults");
            byte OldValue = GetRegister(0x00);
            byte NewValue = (byte)((OldValue & 0xD3) | 0x02); //Everything by D5,D3 and D2...plus the falut clear bit
            Debug.Print("Clear Faults: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));
            SetRegister(0x00, NewValue);
            //SetConfigBit(MAX31865.ConfigSettings.FAULT_CLR, MAX31865.ConfigValues.FAULT_CLR);
        }

        void FaultScanner_Tick(Timer timer)
        {
            byte FaultByte = GetRegister((byte)Command.READ | (byte)Register.FLT_STATUS);
            if (FaultByte > 0)
                if (FaultEvent != null)
                    FaultEvent(this, FaultByte);
        }

        private void CheckIsInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Initialize method needs to be called before this call");
            }
        }

        public void SetConfigBit(MAX31865.ConfigSettings Setting, MAX31865.ConfigValues Value)
        {
            byte OldValue = (byte)GetRegister(0x00);
            byte NewValue = (byte)((~(byte)Setting & OldValue) | (byte)Value);
            Debug.Print("Set Config Bit: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));
            SetRegister(0x00, (byte)NewValue);
        }

        public void EnableFaultScanner(int interval)
        {
            FaultScanner = new GT.Timer(interval);
            FaultScanner.Tick += FaultScanner_Tick;
            FaultScanner.Behavior = GT.Timer.BehaviorType.RunContinuously;
            FaultScanner.Start();
        }

        public void DisableFaultScanner()
        {
            FaultScanner.Tick -= FaultScanner_Tick;
            FaultScanner.Stop();
        }

        public long GetTempRaw()
        {
            //Shift MSB to the left 8 bits)
            long RTDVala = (long)(GetRegister(0x01) << 8);
            long RTDValb = (long)(GetRegister(0x02));
            //if (((Convert.ToByte(RTDValb.ToString("X")) & 0x01)) > 0) FaultEvent(this, 0x00);
            //Merge bytes
            return RTDVala | RTDValb;
        }

        public double GetTempC()
        {
            //(Celc Hi - Celc Low)/(Raw Hi - Raw Lo)
            double ConvFactor = (200.0 - (-250.0)) / (15901.0 - 1517.0);
            //((RawVal - Raw Lo) * ConvFactor) - Celc Lo)
            double EngVal = ((GetTempRaw() - 1517.0) * ConvFactor) - 200.0;
            return EngVal;
        }

        public double GetTempF()
        {
            //Convert C to F
            return (GetTempC() * (9 / 5)) + 32;
        }

        /// <summary>
        ///   Executes a command
        /// </summary>
        /// <param name = "command">Command</param>
        /// <param name = "address">Register to write to</param>
        /// <param name = "data">Data to write</param>
        /// <returns>Response byte array. First byte is the status register</returns>
        public byte[] WriteBlock(byte command, byte address, byte[] data)
        {
            CheckIsInitialized();

            _csPin.Write(false);

            // Create SPI Buffers with Size of Data + 1 (For Command)
            var writeBuffer = new byte[data.Length + 1];
            var readBuffer = new byte[data.Length + 1];

            // Add command and address to SPI buffer
            writeBuffer[0] = (byte)(command | address);

            // Add data to SPI buffer
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Do SPI Read/Write
            _spi.WriteRead(writeBuffer, readBuffer);

            _csPin.Write(true);

            // Return ReadBuffer
            return readBuffer;
        }

        /// <summary>
        ///   Write an entire Register
        /// </summary>
        /// <param name = "register">Register to write to</param>
        /// <param name = "value">Value to be set</param>
        /// <returns>Response byte. Register value after write</returns>
        public byte SetRegister(byte register, byte value)
        {
            CheckIsInitialized();
            Execute((byte)Command.WRITE, register, new byte[] { value });
            return value;// GetRegister(register);
        }

        /// <summary>
        ///   Get an entire Register
        /// </summary>
        /// <param name = "register">Register to read</param>
        /// <returns>Response byte. Register value</returns>
        public byte GetRegister(byte register)
        {
            CheckIsInitialized();
            var read = Execute((byte)Command.READ, register, new byte[1]);
            var result = new byte[read.Length - 1];
            Array.Copy(read, 1, result, 0, result.Length);
            return read[1];
        }


        /// <summary>
        ///   Executes a command (for details see module datasheet)
        /// </summary>
        /// <param name = "command">Command</param>
        /// <param name = "address">Register to write to</param>
        /// <param name = "data">Data to write</param>
        /// <returns>Response byte array. First byte is the status register</returns>
        public byte[] Execute(byte command, byte address, byte[] data)
        {
            CheckIsInitialized();

            _csPin.Write(false);

            // Create SPI Buffers with Size of Data + 1 (For Command)
            var writeBuffer = new byte[data.Length + 1];
            var readBuffer = new byte[data.Length + 1];

            // Add command and address to SPI buffer
            writeBuffer[0] = (byte)(command | address);

            // Add data to SPI buffer
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Do SPI Read/Write
            _spi.WriteRead(writeBuffer, readBuffer);

            _csPin.Write(true);


            // Return ReadBuffer
            return readBuffer;
        }
    }
}
*/