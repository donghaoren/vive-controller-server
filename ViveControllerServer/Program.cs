using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;
using System.Runtime.InteropServices;

using System.Net;
using System.Net.Sockets;

namespace ViveControllerServer
{
    struct ControllerState
    {
        public uint i, t;
        public bool is_ctrl;
        // public double time;
        public string pressed;
        public string touched;
        public float[] a0;
        public float[] a1;
        public bool connected;
        public bool valid;
        public float[] v;
        public float[] w;
        public float[] p;

        public static string ButtonIDToString(ulong bid)
        {
            string r = "";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_System)) != 0) r += "System,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_ApplicationMenu)) != 0) r += "ApplicationMenu,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Grip)) != 0) r += "Grip,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_DPad_Left)) != 0) r += "DPad_Left,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_DPad_Up)) != 0) r += "DPad_Up,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_DPad_Right)) != 0) r += "DPad_Right,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_DPad_Down)) != 0) r += "DPad_Down,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_A)) != 0) r += "A,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_ProximitySensor)) != 0) r += "ProximitySensor,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Axis0)) != 0) r += "Axis0,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Axis1)) != 0) r += "Axis1,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Axis2)) != 0) r += "Axis2,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Axis3)) != 0) r += "Axis3,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Axis4)) != 0) r += "Axis4,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_SteamVR_Touchpad)) != 0) r += "SteamVR_Touchpad,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_SteamVR_Trigger)) != 0) r += "SteamVR_Trigger,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Dashboard_Back)) != 0) r += "Dashboard_Back,";
            if ((bid & (1L << (int)EVRButtonId.k_EButton_Max)) != 0) r += "Max,";
            if (r.Length > 0) return r.Substring(0, r.Length - 1);
            return "";
        }

        public static ControllerState Construct(uint index, TrackedDevicePose_t pose, VRControllerState_t state)
        {
            ControllerState r = new ControllerState();
            r.i = index;
            r.t = state.unPacketNum;
            r.pressed = ButtonIDToString(state.ulButtonPressed);
            r.touched = ButtonIDToString(state.ulButtonTouched);
            r.a0 = new float[] { state.rAxis0.x, state.rAxis0.y };
            r.a1 = new float[] { state.rAxis1.x, state.rAxis1.y };
            r.connected = pose.bDeviceIsConnected;
            r.valid = pose.bPoseIsValid;
            r.v = new float[] { pose.vVelocity.v0, pose.vVelocity.v1, pose.vVelocity.v2 };
            r.w = new float[] { pose.vAngularVelocity.v0, pose.vAngularVelocity.v1, pose.vAngularVelocity.v2 };
            r.p = new float[] {
                pose.mDeviceToAbsoluteTracking.m0, pose.mDeviceToAbsoluteTracking.m1, pose.mDeviceToAbsoluteTracking.m2, pose.mDeviceToAbsoluteTracking.m3,
                pose.mDeviceToAbsoluteTracking.m4, pose.mDeviceToAbsoluteTracking.m5, pose.mDeviceToAbsoluteTracking.m6, pose.mDeviceToAbsoluteTracking.m7,
                pose.mDeviceToAbsoluteTracking.m8, pose.mDeviceToAbsoluteTracking.m9, pose.mDeviceToAbsoluteTracking.m10, pose.mDeviceToAbsoluteTracking.m11
            };
            // r.time = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
            return r;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var JSON = new System.Web.Script.Serialization.JavaScriptSerializer();


            EVRInitError error = 0;
            var system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Utility);
            var state = new VRControllerState_t();
            var pose = new TrackedDevicePose_t();
            var poses = new TrackedDevicePose_t[100];

            var controllers = new System.Collections.ArrayList();

            Console.WriteLine("Vive Controller Data Forwarder");

            Console.WriteLine("List of devices:");

            for (uint index = 0; index < 10; index++)
            {
                if (system.GetTrackedDeviceClass(index) == ETrackedDeviceClass.Controller || system.GetTrackedDeviceClass(index) == ETrackedDeviceClass.GenericTracker)
                {
                    controllers.Add(index);
                    StringBuilder builder = new StringBuilder(100);
                    ETrackedPropertyError err = ETrackedPropertyError.TrackedProp_Success;
                    system.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_ControllerType_String, builder, 100, ref err);
                    Console.WriteLine("  Tracked Device " + index.ToString() + ": " + builder.ToString());
                }
            }

            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            String ipAddress = "192.168.1.30";
            if(args.Length == 1)
            {
                ipAddress = args[0];
            }
            IPAddress broadcast = IPAddress.Parse(ipAddress);
            System.Console.WriteLine("Forwarding VIVE Messages to " + ipAddress);
            while (true)
            {
                ControllerState[] states = new ControllerState[controllers.Count];
                int i = 0;
                system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0.005f, poses);
                foreach (uint controllerIndex in controllers)
                {
                    system.GetControllerStateWithPose(ETrackingUniverseOrigin.TrackingUniverseStanding, controllerIndex, ref state, (uint)Marshal.SizeOf(state), ref pose);
                    pose = poses[controllerIndex];
                    ControllerState cc = ControllerState.Construct(controllerIndex, pose, state);
                    cc.is_ctrl = system.GetTrackedDeviceClass(controllerIndex) == ETrackedDeviceClass.Controller;
                    states[i++] = cc;
                }

                byte[] sendbuf = Encoding.UTF8.GetBytes(JSON.Serialize(states));
                IPEndPoint ep = new IPEndPoint(broadcast, 11000);

                s.SendTo(sendbuf, ep);

                System.Threading.Thread.Sleep(1);
            }
        }
    }
}
