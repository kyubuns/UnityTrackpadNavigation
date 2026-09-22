using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TrackpadNavigation
{
    internal enum GestureKind
    {
        Scroll = 1, Magnify = 2, Rotate = 3, SmartZoom = 4
    }
    [Flags]
    internal enum GesturePhase
    {
        None = 0, Began = 1, Stationary = 2, Changed = 4, Ended = 8, Cancelled = 16, MayBegin = 32
    }
    [Flags]
    internal enum GestureModifiers
    {
        None = 0, Shift = 1, Control = 2, Option = 4, Command = 8
    }
    [Flags]
    internal enum GestureFlags
    {
        None = 0, Precise = 1, Natural = 2, Captured = 4
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct TrackpadEvent
    {
        public double Timestamp, ScreenX, ScreenY, DeltaX, DeltaY, Magnification, Rotation;
        public ulong Sequence;
        public GestureKind Kind;
        public GesturePhase Phase, MomentumPhase;
        public GestureModifiers Modifiers;
        public GestureFlags Flags;
        public int WindowNumber, Target, Reserved;
        public Vector2 ScreenPosition => new Vector2((float)ScreenX, (float)ScreenY);
        public bool IsMomentum => MomentumPhase != GesturePhase.None;
        public bool IsCaptured => (Flags & GestureFlags.Captured) != 0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NativeCapture
    {
        public double X, Y, Width, Height;
        public int Target, WindowNumber, Kinds, Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NativePointer
    {
        public double X, Y;
        public int WindowNumber, Active;
        public Vector2 Position => new Vector2((float)X, (float)Y);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NativeStats
    {
        public ulong Received, Captured, Overflow;
        public int Queued, Installed;
    }

    internal static class TrackpadInput
    {
        const string Library = "TrackpadBridge";
        public static bool Running
        {
            get; private set;
        }
        public static string Status {
            get; private set;
        } = "Not started";
        public static NativeStats Stats
        {
            get
            {
                if (!Running)
                {
                    return default;
                }

                TN_GetStats(out var value);
                return value;
            }
        }

        public static void Start()
        {
            if (Running)
            {
                return;
            }

            if (Application.platform != RuntimePlatform.OSXEditor || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
            {
                Status = "macOS Editor running natively on Apple Silicon is required.";
                return;
            }
            try
            {
                if (TN_ApiVersion() != 1 || TN_EventSize() != Marshal.SizeOf<TrackpadEvent>())
                {
                    throw new InvalidOperationException("Native ABI mismatch. Rebuild TrackpadBridge and restart Unity.");
                }

                Running = TN_Start() != 0;
                if (Running)
                {
                    UnityGestureGuard.Install();
                }

                Status = Running ? "Native monitor running (arm64 / AppKit)" : "AppKit main thread is unavailable.";
            }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException || exception is InvalidOperationException)
            {
                Stop();
                Status = exception.Message;
                Debug.LogWarning("Trackpad Navigation: " + Status);
            }
        }

        public static void Stop()
        {
            UnityGestureGuard.Remove();
            if (Running)
            {
                TN_Stop();
                Status = "Native monitor stopped";
            }
            Running = false;
        }
        public static bool Poll(out TrackpadEvent value)
        {
            value = default;
            return Running && TN_Poll(out value) != 0;
        }
        public static bool GetPointer(out NativePointer value)
        {
            value = default;
            return Running && TN_GetPointer(out value) != 0;
        }
        public static void SetCapture(NativeCapture value, bool observe)
        {
            if (Running)
            {
                TN_SetCapture(ref value, observe ? 1 : 0);
            }
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int TN_ApiVersion();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int TN_EventSize();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int TN_Start();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void TN_Stop();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int TN_Poll(out TrackpadEvent value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void TN_SetCapture(ref NativeCapture value, int observe);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int TN_GetPointer(out NativePointer value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void TN_GetStats(out NativeStats value);
    }
}
