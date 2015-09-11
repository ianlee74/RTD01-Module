using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;

using Gadgeteer.Modules.BrainardTechnologies;
namespace MAX31865_Example
{
    public partial class Program
    {
        private GT.Timer PollTimer;

        void ProgramStarted()
        {
            byte config = (byte)(
            (byte)MAX31865.ConfigValues.VBIAS_ON |
            (byte)MAX31865.ConfigValues.THREE_WIRE |
            (byte)MAX31865.ConfigValues.FILTER_50Hz);

            MAX31865_Instance.Initialize(GT.Socket.Pin.Three, GT.Socket.Pin.Six, config);

            //MAX31865_Instance.SetConvToAuto();

            MAX31865_Instance.EnableFaultScanner(1000);
            MAX31865_Instance.FaultEvent += MAX31865_Instance_FaultEvent;

            PollTimer = new GT.Timer(500);
            PollTimer.Tick += PollTimer_Tick;
            PollTimer.Start();

            MAX31865_Instance.DataReadyFarEvent += MAX31865_Instance_DataReadyFarEvent;

            Debug.Print("Program Started");
        }

        void MAX31865_Instance_DataReadyFarEvent(MAX31865 sender, double Data)
        {
            byte config = MAX31865_Instance.GetRegister(0x00);
            Debug.Print("Temp: " + Data + "f ");            
        }

        void MAX31865_Instance_FaultEvent(MAX31865 sender, byte FaultByte)
        {
            Debug.Print("Fault: " + FaultByte.ToString("X"));            
            MAX31865_Instance.ClearFaults();        
        }

        void PollTimer_Tick(GT.Timer timer)
        {
            Debug.Print("Fault " + MAX31865_Instance.GetRegister(0x07).ToString("X") + " Config: " + MAX31865_Instance.GetRegister(0x00).ToString("X") + " Temp: " + MAX31865_Instance.GetTempF());
            MAX31865_Instance.ExecuteOneShot();
        }

    }
}
